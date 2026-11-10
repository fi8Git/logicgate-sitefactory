using System.Text;
using System.Text.RegularExpressions;
using LogicGate.SiteFactory.Application.Repositories;

namespace LogicGate.SiteFactory.Modules.Documentation.Scanning;

/// <summary>V1 discovery policy only: no rendering, includes, URLs, Git commands or writes.</summary>
public sealed partial class RepositoryDocumentationScanner
{
    public const int MaximumDocuments = 1024;
    public const int MaximumEntries = 10000;
    public const int MaximumDepth = 32;
    public const int MaximumDocumentBytes = 1024 * 1024;
    public const int MaximumTotalBytes = 64 * 1024 * 1024;
    private readonly ILocalRepositoryReader reader;

    public RepositoryDocumentationScanner(ILocalRepositoryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        this.reader = reader;
    }

    public async Task<DocumentationScanResult> ScanAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyRepository repository;
        try { repository = await reader.OpenAsync(repositoryPath, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return Failure("SF-DOC-SCAN-001", "A supported local Git repository root is required.");
        }

        using (repository)
        {
            try
            {
                List<RepositoryDocument> documents = [];
                Stack<(string Path, int Depth)> directories = new();
                directories.Push(("", 0));
                int entriesRead = 0;
                int bytesRead = 0;
                while (directories.TryPop(out (string Path, int Depth) directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (directory.Depth > MaximumDepth || entriesRead >= MaximumEntries) return LimitExceeded();
                    IReadOnlyList<RepositoryEntry> entries = repository.List(directory.Path, MaximumEntries - entriesRead, cancellationToken);
                    entriesRead += entries.Count;
                    foreach (RepositoryEntry entry in entries.OrderBy(static entry => entry.Name, StringComparer.Ordinal))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (IsExcluded(entry.Name)) continue;
                        bool root = directory.Path.Length == 0;
                        bool source = root ? IsReadmeName(entry.Name) : entry.Name.EndsWith(".md", StringComparison.Ordinal);
                        bool descend = root ? entry.Name == "docs" : entry.Kind == RepositoryEntryKind.Directory;
                        // Refuse any link/special entry below docs, or matching a root source. Never follow it to classify its target.
                        if (entry.Kind == RepositoryEntryKind.Unsupported && (!root || source || descend))
                            return Failure("SF-DOC-SCAN-002", "A source is unsafe, unreadable or exceeds the read limits.");
                        if (root && !source && !descend) continue;
                        string path = root ? entry.Name : directory.Path + "/" + entry.Name;
                        if (entry.Kind == RepositoryEntryKind.Directory)
                        {
                            if (descend) directories.Push((path, directory.Depth + 1));
                            continue;
                        }
                        if (!source) continue;
                        if (documents.Count == MaximumDocuments) return LimitExceeded();
                        string text = await repository.ReadUtf8Async(path, MaximumDocumentBytes, cancellationToken).ConfigureAwait(false);
                        bytesRead += Encoding.UTF8.GetByteCount(text);
                        if (bytesRead > MaximumTotalBytes) return LimitExceeded();
                        documents.Add(new(path, path, Title(text, entry.Name), root ? DocumentationSourceType.RootReadme : DocumentationSourceType.DocumentationPage));
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new(documents);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Deliberately no provider message, physical path, document content or partial successful inventory.
                return Failure("SF-DOC-SCAN-002", "A source is unsafe, unreadable or exceeds the read limits.");
            }
        }
    }

    internal static bool IsReadmeName(string name) => ReadmeName().IsMatch(name);

    internal static bool IsExcluded(string name) => name.Equals(".git", StringComparison.OrdinalIgnoreCase)
        || name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
        || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) || name.Equals(".sitefactory", StringComparison.OrdinalIgnoreCase)
        || name.Equals(".secrets", StringComparison.OrdinalIgnoreCase) || name.Equals("secrets", StringComparison.OrdinalIgnoreCase);

    private static string Title(string text, string name)
    {
        string candidate = name.Length > 3 ? name[..^3] : name; // Every selected source has the exact .md suffix.
        using StringReader lines = new(text);
        while (lines.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("# ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line[2..])) candidate = line[2..].Trim();
            break;
        }
        int length = Math.Min(candidate.Length, 200);
        if (length < candidate.Length && char.IsHighSurrogate(candidate[length - 1])) length--;
        return candidate[..length];
    }

    private static DocumentationScanResult Failure(string code, string message) => new([], new(code, message));
    private static DocumentationScanResult LimitExceeded() => Failure("SF-DOC-SCAN-003", "The repository scan limits were exceeded.");

    // Locale-labelled Markdown only, not README.txt, README.backup.md, executables or arbitrary README.*.
    [GeneratedRegex(@"\AREADME(?:\.[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*)?\.md\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReadmeName();
}
