namespace LogicGate.SiteFactory.Application.Repositories;

/// <summary>Opens an explicitly selected local working tree for confined, read-only access.</summary>
public interface ILocalRepositoryReader
{
    Task<IReadOnlyRepository> OpenAsync(string rootPath, CancellationToken cancellationToken = default);
}

/// <summary>Relative paths only; no Git internals, links, writes, discovery of parent repositories or remote access.</summary>
public interface IReadOnlyRepository : IDisposable
{
    // Empty relativeDirectory denotes the root. Enumeration is non-recursive and bounded.
    IReadOnlyList<RepositoryEntry> List(string relativeDirectory, int maximumEntries, CancellationToken cancellationToken = default);

    Task<string> ReadUtf8Async(string relativePath, int maximumBytes, CancellationToken cancellationToken = default);
}

public enum RepositoryEntryKind { File, Directory, Unsupported }

public sealed record RepositoryEntry(string Name, RepositoryEntryKind Kind);
