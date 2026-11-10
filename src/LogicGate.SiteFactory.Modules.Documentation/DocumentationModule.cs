using System.Text;
using System.Text.Encodings.Web;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using LogicGate.SiteFactory.Modules.Documentation.Model;
using LogicGate.SiteFactory.Modules.Documentation.Rendering;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Modules.Documentation;

/// <summary>Explicit embedded or model-backed contribution; no destination access or deployment.</summary>
public sealed class DocumentationModule : ISiteFactoryModule, ITemplateCatalog, ITemplateContentProvider
{
    private const string Version = "0.2.0";
    private const string EmbeddedTemplateVersion = "0.1.0";
    private static readonly TemplateId Template = new("documentation-minimal");
    private readonly ITemplateManifestReader manifestReader;

    public DocumentationModule(ITemplateManifestReader manifestReader)
    {
        ArgumentNullException.ThrowIfNull(manifestReader);
        this.manifestReader = manifestReader;
    }

    public ModuleDescriptor Descriptor { get; } = new(new("documentation"), new(Version), [new("documentation.generation")]);

    public GenerationContribution CreateContribution() => new(Descriptor.Id, Descriptor.Version, Template, this, this);

    public GenerationContribution CreateContribution(DocumentationSite site)
    {
        ArgumentNullException.ThrowIfNull(site);
        DocumentationSiteTemplate template = new(site);
        return new(Descriptor.Id, Descriptor.Version, Template, template, template);
    }

    public async Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string? document = await ReadResourceAsync("LogicGate.SiteFactory.Templates.Documentation.Manifest", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return document is null ? TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest) : manifestReader.Read(document);
        }
        catch (DecoderFallbackException)
        {
            return TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest);
        }
    }

    public async Task<string?> ReadAsync(TemplateManifest manifest, ClientConfigurationV1 configuration,
        string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (manifest.TemplateId != Template || manifest.TemplateVersion.Value != EmbeddedTemplateVersion) return null;

        (string Resource, string Title)? page = source switch
        {
            "pages/index" => ("LogicGate.SiteFactory.Templates.Documentation.Index", "Accueil"),
            "pages/guide" => ("LogicGate.SiteFactory.Templates.Documentation.Guide", "Guide"),
            _ => null,
        };
        if (page is null) return null;
        string markdown = await ReadResourceAsync(page.Value.Resource, cancellationToken)
            ?? throw new InvalidOperationException("A required documentation resource is missing.");
        cancellationToken.ThrowIfCancellationRequested();
        string title = HtmlEncoder.Default.Encode(configuration.Client.DisplayName);
        string locale = HtmlEncoder.Default.Encode(configuration.Site.DefaultLocale);
        // All attributes/links are fixed or encoded; raw HTML, scripts and Markdown URLs are never interpreted.
        return $"<!DOCTYPE html>\n<html lang=\"{locale}\">\n<head>\n<meta charset=\"utf-8\">\n" +
            $"<title>{title} — {page.Value.Title}</title>\n</head>\n<body>\n<header><h1>{title}</h1></header>\n" +
            "<nav aria-label=\"Documentation\"><a href=\"index.html\">Accueil</a> | <a href=\"guide.html\">Guide</a></nav>\n" +
            "<main>\n" + RenderMarkdown(markdown) + "</main>\n</body>\n</html>\n";
    }

    private static async Task<string?> ReadResourceAsync(string name, CancellationToken cancellationToken)
    {
        using Stream? stream = typeof(DocumentationModule).Assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using StreamReader reader = new(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    // Intentionally limited grammar: #/## headings and paragraphs. All other syntax remains escaped text.
    private static string RenderMarkdown(string markdown)
    {
        StringBuilder html = new();
        List<string> paragraph = [];
        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            html.Append("<p>").Append(HtmlEncoder.Default.Encode(string.Join(' ', paragraph))).Append("</p>\n");
            paragraph.Clear();
        }

        using StringReader reader = new(markdown);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) { FlushParagraph(); continue; }
            int level = line.StartsWith("## ", StringComparison.Ordinal) ? 2 : line.StartsWith("# ", StringComparison.Ordinal) ? 1 : 0;
            if (level == 0) { paragraph.Add(line.Trim()); continue; }
            FlushParagraph();
            string tag = level == 1 ? "h1" : "h2";
            html.Append('<').Append(tag).Append('>').Append(HtmlEncoder.Default.Encode(line[(level + 1)..].Trim()))
                .Append("</").Append(tag).Append(">\n");
        }
        FlushParagraph();
        return html.ToString();
    }
}
