using System.Text;
using LogicGate.SiteFactory.Modules.Documentation.Scanning;

namespace LogicGate.SiteFactory.Modules.Documentation.Model;

/// <summary>Pure transformation: no repository access, rendering or generation.</summary>
public static class DocumentationModelBuilder
{
    public const int MaximumLinksPerPage = 1024;
    public const int MaximumTotalLinks = 16384;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static DocumentationModelResult Build(IEnumerable<DocumentationSource> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        cancellationToken.ThrowIfCancellationRequested();
        List<DocumentationSource> inputs = [];
        foreach (DocumentationSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(source);
            if (inputs.Count == RepositoryDocumentationScanner.MaximumDocuments) return LimitExceeded();
            inputs.Add(source);
        }
        inputs.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Document.RelativePath, right.Document.RelativePath));
        int totalBytes = 0;
        foreach (DocumentationSource source in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DocumentationSourcePath.IsValid(source.Document))
                return DocumentationModelResult.Failure("SF-DOC-MODEL-001", "A document path or logical identity is invalid.");
            int bytes;
            try
            {
                _ = StrictUtf8.GetByteCount(source.Document.RelativePath);
                bytes = StrictUtf8.GetByteCount(source.Markdown);
            }
            catch (EncoderFallbackException)
            {
                return DocumentationModelResult.Failure("SF-DOC-MODEL-003", "A document contains invalid Unicode.");
            }
            if (bytes > RepositoryDocumentationScanner.MaximumDocumentBytes
                || bytes > RepositoryDocumentationScanner.MaximumTotalBytes - totalBytes) return LimitExceeded();
            totalBytes += bytes;
        }
        Dictionary<string, RepositoryDocument> documents = new(StringComparer.Ordinal);
        HashSet<string> routes = new(StringComparer.OrdinalIgnoreCase);
        foreach (DocumentationSource input in inputs)
        {
            if (!documents.TryAdd(input.Document.RelativePath, input.Document) || !routes.Add(DocumentationSourcePath.Route(input.Document.RelativePath)))
                return DocumentationModelResult.Failure("SF-DOC-MODEL-002", "Multiple documents have the same logical identity or route.");
        }

        List<DocumentationPage> pages = [];
        int totalLinks = 0;
        foreach (DocumentationSource input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string markdown = input.Markdown.TrimStart('\uFEFF').ReplaceLineEndings("\n");
            var outline = MarkdownOutline.Read(markdown, cancellationToken);
            if (outline is null || outline.Value.Links.Count > MaximumTotalLinks - totalLinks) return LimitExceeded();
            totalLinks += outline.Value.Links.Count;
            string path = input.Document.RelativePath;
            string title = outline.Value.Title ?? DocumentationSourcePath.FallbackTitle(path.Split('/')[^1][..^3]);
            pages.Add(new(input.Document, DocumentationSourcePath.Route(path), title, markdown,
                outline.Value.Links.Select(target => ResolveLink(path, target, documents))));
        }
        // Home first, then exact source path order. No title-dependent ordering or current-culture comparison.
        DocumentationPage[] ordered = pages.OrderBy(static page => page.Route == "/" ? 0 : 1)
            .ThenBy(static page => page.RelativePath, StringComparer.Ordinal).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(new(ordered, Navigation(ordered, "")));
    }

    private static DocumentationLink ResolveLink(string source, string destination, Dictionary<string, RepositoryDocument> documents)
    {
        int hash = destination.IndexOf('#', StringComparison.Ordinal);
        string path = hash < 0 ? destination : destination[..hash];
        string? fragment = hash < 0 ? null : destination[(hash + 1)..];
        string? target = DocumentationSourcePath.ResolveLinkPath(source, path);
        RepositoryDocument? page = target is not null ? documents.GetValueOrDefault(target) : null;
        return new(destination, page?.LogicalId, page is null ? null : DocumentationSourcePath.Route(page.RelativePath), fragment);
    }

    private static DocumentationNavigation Navigation(DocumentationPage[] pages, string prefix)
    {
        DocumentationPage[] direct = pages.Where(page => !page.Route[(prefix.Length + 1)..].Contains('/', StringComparison.Ordinal)).ToArray();
        DocumentationSection[] sections = pages.Except(direct)
            .GroupBy(page => page.Route[(prefix.Length + 1)..].Split('/')[0], StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group => new DocumentationSection(prefix + "/" + group.Key,
                DocumentationSourcePath.FallbackTitle(Uri.UnescapeDataString(group.Key)), Navigation(group.ToArray(), prefix + "/" + group.Key)))
            .ToArray();
        return new(direct, sections);
    }

    internal static DocumentationModelResult LimitExceeded() => DocumentationModelResult.Failure("SF-DOC-MODEL-004", "The documentation model limits were exceeded.");
}
