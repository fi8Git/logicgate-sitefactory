using LogicGate.SiteFactory.Modules.Documentation.Scanning;

namespace LogicGate.SiteFactory.Modules.Documentation.Model;

/// <summary>In-memory input. Markdown remains untrusted content, not rendered output.</summary>
public sealed class DocumentationSource(RepositoryDocument document, string markdown)
{
    public RepositoryDocument Document { get; } = document ?? throw new ArgumentNullException(nameof(document));
    public string Markdown { get; } = markdown ?? throw new ArgumentNullException(nameof(markdown));
}

public sealed class DocumentationPage
{
    internal DocumentationPage(RepositoryDocument document, string route, string title, string markdown,
        IEnumerable<DocumentationLink> links)
    {
        LogicalId = document.LogicalId;
        RelativePath = document.RelativePath;
        SourceType = document.SourceType;
        Route = route;
        Title = title;
        Markdown = markdown;
        InternalLinks = Array.AsReadOnly(links.ToArray());
    }

    public string LogicalId { get; }
    public string RelativePath { get; }
    public DocumentationSourceType SourceType { get; }
    public string Route { get; }
    public string Title { get; }
    public string Markdown { get; }
    public IReadOnlyList<DocumentationLink> InternalLinks { get; }
}

/// <summary>Only a supplied page can be resolved. Fragments are retained, not validated.</summary>
public sealed record DocumentationLink(string Destination, string? TargetPageId, string? TargetRoute, string? Fragment)
{
    public bool IsResolved => TargetPageId is not null;
}

public sealed class DocumentationNavigation
{
    internal DocumentationNavigation(IEnumerable<DocumentationPage> pages, IEnumerable<DocumentationSection> sections)
    {
        Pages = Array.AsReadOnly(pages.ToArray());
        Sections = Array.AsReadOnly(sections.ToArray());
    }

    public IReadOnlyList<DocumentationPage> Pages { get; }
    public IReadOnlyList<DocumentationSection> Sections { get; }
}

/// <summary>A logical directory grouping, not a physical output directory.</summary>
public sealed record DocumentationSection(string LogicalPath, string Title, DocumentationNavigation Navigation);

public sealed class DocumentationSite
{
    internal DocumentationSite(IEnumerable<DocumentationPage> pages, DocumentationNavigation navigation)
    {
        Pages = Array.AsReadOnly(pages.ToArray());
        Home = Pages.SingleOrDefault(static page => page.Route == "/");
        Navigation = navigation;
    }

    public DocumentationPage? Home { get; }
    public IReadOnlyList<DocumentationPage> Pages { get; }
    public DocumentationNavigation Navigation { get; }
}

public sealed record DocumentationModelDiagnostic(string Code, string Message);

public sealed class DocumentationModelResult
{
    internal DocumentationModelResult(DocumentationSite? site, DocumentationModelDiagnostic? diagnostic = null)
    {
        Site = site;
        Diagnostic = diagnostic;
    }

    public DocumentationSite? Site { get; }
    public DocumentationModelDiagnostic? Diagnostic { get; }
    public bool IsSuccess => Site is not null;

    internal static DocumentationModelResult Failure(string code, string message) => new(null, new(code, message));
}
