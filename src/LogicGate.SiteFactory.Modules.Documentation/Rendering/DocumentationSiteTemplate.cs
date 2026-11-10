using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using LogicGate.SiteFactory.Modules.Documentation.Model;

namespace LogicGate.SiteFactory.Modules.Documentation.Rendering;

/// <summary>Only declares intentions and supplies strings. All writes belong to the existing executor.</summary>
internal sealed class DocumentationSiteTemplate : ITemplateCatalog, ITemplateContentProvider
{
    // Conservative V1 bounds also keep the existing 256 KiB ownership-state document readable on regeneration.
    private const int MaximumPages = 128;
    private const int MaximumOutputPathLength = 200;
    private const string StyleSource = "site/style";
    private readonly DocumentationSite site;
    private readonly Dictionary<string, DocumentationPage> pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> paths = new(StringComparer.Ordinal);
    private readonly TemplateCatalogReadResult catalog;

    internal DocumentationSiteTemplate(DocumentationSite site)
    {
        this.site = site;
        if (site.Home is null)
        {
            catalog = Failure("SF-DOC-SITE-001", "The documentation site requires a main README home page.");
            return;
        }
        if (site.Pages.Count > MaximumPages)
        {
            catalog = LimitExceeded();
            return;
        }
        List<TemplateFile> files = [];
        foreach (DocumentationPage page in site.Pages)
        {
            string path = page.Route == "/" ? "index.html" : "pages/" + Uri.UnescapeDataString(page.Route[1..]) + ".html";
            if (path.Length > MaximumOutputPathLength)
            {
                catalog = LimitExceeded();
                return;
            }
            string id = "documentation-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(page.LogicalId)));
            string source = "pages/" + id;
            paths.Add(page.LogicalId, path);
            pages.Add(source, page);
            files.Add(new(new(id), path, source, FileGenerationStrategy.Generated));
        }
        files.Add(new(new("documentation-style"), "documentation.css", StyleSource, FileGenerationStrategy.Generated));
        TemplateManifest manifest = new(new("documentation-minimal"), new("0.2.0"), [new ProductId("documentation")], files);
        IReadOnlyList<GenerationPlanDiagnostic> diagnostics = GenerationPlanPathValidator.Validate(files.Select(file =>
            new PlannedFile(file.Id, file.RelativePath, new(PlannedFileOriginKind.Template, manifest.TemplateId.Value),
                file.Strategy, PlannedFileAction.Create)));
        catalog = diagnostics.Count == 0 ? TemplateCatalogReadResult.Success([manifest]) : TemplateCatalogReadResult.Failure(diagnostics[0]);
    }

    public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(catalog);
    }

    public Task<string?> ReadAsync(TemplateManifest manifest, ClientConfigurationV1 configuration, string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!catalog.IsSuccess || !ReferenceEquals(manifest, catalog.Manifests[0])) return Task.FromResult<string?>(null);
        if (source == StyleSource) return Task.FromResult<string?>(Style);
        return Task.FromResult(pages.TryGetValue(source, out DocumentationPage? page)
            ? DocumentationHtmlRenderer.Render(site, page, paths, configuration, cancellationToken) : null);
    }

    private static TemplateCatalogReadResult LimitExceeded() => Failure("SF-DOC-SITE-002", "The documentation site exceeds its page or output-path limits.");
    private static TemplateCatalogReadResult Failure(string code, string message) =>
        TemplateCatalogReadResult.Failure(new(new(code), DiagnosticSeverity.Error, message));

    private const string Style = """
        :root { color-scheme: light dark; font-family: system-ui, sans-serif; line-height: 1.6; }
        body { max-width: 76rem; margin: auto; padding: 1rem; }
        header { border-bottom: 1px solid; padding-block: 1rem; font-size: 1.4rem; }
        .layout { display: grid; grid-template-columns: minmax(12rem, 1fr) minmax(0, 3fr); gap: 2rem; }
        nav ul { padding-inline-start: 1.2rem; }
        a:focus-visible { outline: 3px solid; outline-offset: 3px; }
        [aria-current="page"] { font-weight: bold; }
        pre { overflow: auto; padding: 1rem; border: 1px solid; }
        code { overflow-wrap: anywhere; }
        @media (max-width: 45rem) { .layout { display: block; } }

        """;
}
