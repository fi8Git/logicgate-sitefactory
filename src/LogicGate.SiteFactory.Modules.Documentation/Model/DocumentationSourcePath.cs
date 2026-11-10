using LogicGate.SiteFactory.Modules.Documentation.Scanning;

namespace LogicGate.SiteFactory.Modules.Documentation.Model;

internal static class DocumentationSourcePath
{
    internal static bool IsValid(RepositoryDocument document)
    {
        string path = document.RelativePath;
        if (!IsSafe(path) || document.LogicalId != path) return false;
        return document.SourceType switch
        {
            DocumentationSourceType.RootReadme => !path.Contains('/', StringComparison.Ordinal)
                && RepositoryDocumentationScanner.IsReadmeName(path),
            DocumentationSourceType.DocumentationPage => path.StartsWith("docs/", StringComparison.Ordinal)
                && path.EndsWith(".md", StringComparison.Ordinal) && path.Split('/')[^1].Length > 3,
            _ => false
        };
    }

    private static bool IsSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.StartsWith('~')
            || path.Any(static character => char.IsControl(character) || "<>:\"\\|?*$%#".Contains(character, StringComparison.Ordinal)))
            return false;
        string[] segments = path.Split('/');
        if (segments.Length > RepositoryDocumentationScanner.MaximumDepth + 1) return false;
        return segments.All(static segment => segment.Length is > 0 and <= 255 && segment is not "." and not ".."
            && !segment.EndsWith(' ') && !segment.EndsWith('.') && !RepositoryDocumentationScanner.IsExcluded(segment)
            && !IsDeviceName(segment));
    }

    private static bool IsDeviceName(string segment)
    {
        string stem = segment.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && "123456789¹²³".Contains(stem[3], StringComparison.Ordinal));
    }

    internal static string Route(string path)
    {
        if (path == "README.md") return "/";
        string stem = path.StartsWith("docs/", StringComparison.Ordinal) ? path[5..^3] : path[..^3];
        return "/" + string.Join('/', stem.Split('/').Select(static segment => Uri.EscapeDataString(segment.ToLowerInvariant())));
    }

    internal static string FallbackTitle(string name)
    {
        string[] words = name.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        string title = string.Join(' ', words.Select(static word => char.ToUpperInvariant(word[0]) + word[1..]));
        return MarkdownOutline.BoundTitle(title.Length == 0 ? "Page" : title);
    }

    internal static string? ResolveLinkPath(string source, string target)
    {
        if (target.Length == 0) return source;
        // Root-relative URLs are not repository-relative Markdown links in V1.
        if (target.StartsWith('/') || target.Contains('\\', StringComparison.Ordinal) || target.Contains('?', StringComparison.Ordinal)) return null;
        string decoded = Uri.UnescapeDataString(target);
        if (decoded.StartsWith('/') || decoded.Contains('\\', StringComparison.Ordinal)) return null;
        List<string> segments = source.Split('/')[..^1].ToList();
        foreach (string segment in decoded.Split('/'))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) return null;
                segments.RemoveAt(segments.Count - 1);
            }
            else segments.Add(segment);
        }
        string path = string.Join('/', segments);
        return IsSafe(path) ? path : null;
    }
}
