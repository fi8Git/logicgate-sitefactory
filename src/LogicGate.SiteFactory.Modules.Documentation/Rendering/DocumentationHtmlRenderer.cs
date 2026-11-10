using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Modules.Documentation.Model;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LogicGate.SiteFactory.Modules.Documentation.Rendering;

internal static class DocumentationHtmlRenderer
{
    // No advanced extensions, user attributes, includes, self-pipeline, scripts, diagrams or external resources.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).Build();

    internal static string Render(DocumentationSite site, DocumentationPage page, IReadOnlyDictionary<string, string> paths,
        ClientConfigurationV1 configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MarkdownDocument document = Markdown.Parse(page.Markdown, Pipeline);
        cancellationToken.ThrowIfCancellationRequested();
        using StringWriter body = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
        HtmlRenderer renderer = new(body);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Remove(renderer.ObjectRenderers.FindExact<LinkInlineRenderer>()!);
        renderer.ObjectRenderers.Remove(renderer.ObjectRenderers.FindExact<AutolinkInlineRenderer>()!);
        renderer.ObjectRenderers.Add(new InternalLinkRenderer(page, paths));
        renderer.ObjectRenderers.Add(new PlainAutolinkRenderer());
        renderer.Render(document);
        cancellationToken.ThrowIfCancellationRequested();

        string path = paths[page.LogicalId];
        StringBuilder html = new();
        html.Append("<!DOCTYPE html>\n<html lang=\"").Append(Encode(configuration.Site.DefaultLocale)).Append("\">\n<head>\n")
            .Append("<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'self'; base-uri 'none'; form-action 'none'\">\n")
            .Append("<title>").Append(Encode(page.Title)).Append(" — ").Append(Encode(configuration.Client.DisplayName)).Append("</title>\n")
            .Append("<link rel=\"stylesheet\" href=\"").Append(Encode(RelativeHref(path, "documentation.css"))).Append("\">\n</head>\n<body>\n")
            .Append("<a href=\"#content\">Skip to content</a>\n<header>").Append(Encode(configuration.Client.DisplayName)).Append("</header>\n")
            .Append("<div class=\"layout\">\n<nav aria-label=\"Documentation\">\n");
        Navigation(html, site.Navigation, page, paths, cancellationToken);
        html.Append("</nav>\n<main id=\"content\">\n");
        if (!document.Descendants<HeadingBlock>().Any()) html.Append("<h1>").Append(Encode(page.Title)).Append("</h1>\n");
        html.Append(body.ToString().ReplaceLineEndings("\n")).Append("</main>\n</div>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static void Navigation(StringBuilder html, DocumentationNavigation navigation, DocumentationPage current,
        IReadOnlyDictionary<string, string> paths, CancellationToken cancellationToken)
    {
        html.Append("<ul>\n");
        foreach (DocumentationPage page in navigation.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            html.Append("<li><a href=\"").Append(Encode(RelativeHref(paths[current.LogicalId], paths[page.LogicalId]))).Append('"');
            if (page.LogicalId == current.LogicalId) html.Append(" aria-current=\"page\"");
            html.Append('>').Append(Encode(page.Title)).Append("</a></li>\n");
        }
        foreach (DocumentationSection section in navigation.Sections)
        {
            html.Append("<li><span>").Append(Encode(section.Title)).Append("</span>\n");
            Navigation(html, section.Navigation, current, paths, cancellationToken);
            html.Append("</li>\n");
        }
        html.Append("</ul>\n");
    }

    private static string RelativeHref(string from, string to) =>
        string.Concat(Enumerable.Repeat("../", from.Count(static character => character == '/')))
        + "./" + string.Join('/', to.Split('/').Select(Uri.EscapeDataString));

    private static string Encode(string text) => HtmlEncoder.Default.Encode(text);

    private sealed class InternalLinkRenderer(DocumentationPage page, IReadOnlyDictionary<string, string> paths) : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            DocumentationLink? target = !link.IsImage && link.Reference is null
                ? page.InternalLinks.FirstOrDefault(item => item.IsResolved && item.Destination == link.Url) : null;
            if (target is null || !paths.TryGetValue(target.TargetPageId!, out string? path))
            {
                renderer.WriteChildren(link); // Label only: never copy an untrusted URL into an attribute.
                return;
            }
            string href = RelativeHref(paths[page.LogicalId], path);
            if (target.Fragment is not null) href += "#" + Uri.EscapeDataString(Uri.UnescapeDataString(target.Fragment));
            renderer.Write("<a href=\"").WriteEscape(href).Write("\">");
            renderer.WriteChildren(link);
            renderer.Write("</a>");
        }
    }

    private sealed class PlainAutolinkRenderer : HtmlObjectRenderer<AutolinkInline>
    {
        protected override void Write(HtmlRenderer renderer, AutolinkInline link) => renderer.WriteEscape(link.Url);
    }
}
