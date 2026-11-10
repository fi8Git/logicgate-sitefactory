using System.Text;
using LogicGate.SiteFactory.Application.Repositories;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Repositories;

/// <summary>Local .git-directory working trees only. No Git process, config, hooks, alternates or gitfile are followed.</summary>
public sealed class LocalGitRepositoryReader : ILocalRepositoryReader
{
    public async Task<IReadOnlyRepository> OpenAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GenerationRoot root = new(rootPath);
        Repository repository = new(root, cancellationToken);
        try
        {
            await repository.ValidateGitAsync(cancellationToken).ConfigureAwait(false);
            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    private sealed class Repository : IReadOnlyRepository
    {
        // This name is never created or read. It selects the parent directory in a read-only native session.
        private const string ProbeName = ".sitefactory-read-only-probe";
        private readonly GenerationRoot root;
        private readonly AtomicWriteSession anchor;
        private bool disposed;

        internal Repository(GenerationRoot root, CancellationToken token)
        {
            this.root = root;
            anchor = Open(ProbeName, token);
        }

        internal async Task ValidateGitAsync(CancellationToken token)
        {
            // The only exception to the public .git exclusion: fixed metadata paths, no user-controlled redirection.
            using AtomicWriteSession objects = Open(".git/objects/" + ProbeName, token);
            using AtomicWriteSession refs = Open(".git/refs/" + ProbeName, token);
            using AtomicWriteSession head = Open(".git/HEAD", token);
            byte[] bytes = await head.ReadControlDocumentAsync(4096, token).ConfigureAwait(false)
                ?? throw new IOException("The local Git HEAD is absent.");
            string value = Decode(bytes).TrimEnd('\r', '\n');
            bool detached = value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);
            bool symbolic = value.StartsWith("ref: refs/", StringComparison.Ordinal) && IsReference(value[5..]);
            if (!detached && !symbolic) throw new IOException("The local Git HEAD is invalid.");
            objects.VerifyDirectories();
            refs.VerifyDirectories();
            anchor.VerifyDirectories();
        }

        public IReadOnlyList<RepositoryEntry> List(string relativeDirectory, int maximumEntries, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(relativeDirectory);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
            cancellationToken.ThrowIfCancellationRequested();
            string logical = relativeDirectory.Length == 0 ? ProbeName : relativeDirectory + "/" + ProbeName;
            string physicalParent = Path.GetDirectoryName(root.Resolve(logical))!;
            anchor.VerifyDirectories();
            using AtomicWriteSession session = Open(logical, cancellationToken);
            session.VerifyDirectories();
            List<RepositoryEntry> entries = [];
            foreach ((string name, FileAttributes attributes) in session.EnumerateParent(physicalParent))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count == maximumEntries) throw new IOException("The repository enumeration limit was exceeded.");
                RepositoryEntryKind kind = (attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0
                    ? RepositoryEntryKind.Unsupported
                    : (attributes & FileAttributes.Directory) != 0 ? RepositoryEntryKind.Directory : RepositoryEntryKind.File;
                entries.Add(new(name, kind));
            }
            session.VerifyDirectories();
            anchor.VerifyDirectories();
            return Array.AsReadOnly(entries.OrderBy(static entry => entry.Name, StringComparer.Ordinal).ToArray());
        }

        public async Task<string> ReadUtf8Async(string relativePath, int maximumBytes, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
            cancellationToken.ThrowIfCancellationRequested();
            _ = root.Resolve(relativePath);
            anchor.VerifyDirectories();
            using AtomicWriteSession session = Open(relativePath, cancellationToken);
            byte[] bytes = await session.ReadControlDocumentAsync(maximumBytes, cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The repository source disappeared.");
            anchor.VerifyDirectories();
            return Decode(bytes);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            anchor.Dispose();
        }

        private AtomicWriteSession Open(string logical, CancellationToken token) =>
            AtomicWriteSession.OpenExisting(root, logical, token) ?? throw new IOException("A required repository directory is absent.");

        private static string Decode(byte[] bytes)
        {
            try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
            catch (DecoderFallbackException) { throw new IOException("A repository text source is not valid UTF-8."); }
        }

        private static bool IsReference(string value) =>
            !value.Contains("..", StringComparison.Ordinal) && !value.Contains("@{", StringComparison.Ordinal)
            && !value.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character) || "~^:?*[\\".Contains(character, StringComparison.Ordinal))
            && value.Split('/').All(static part => part.Length != 0 && !part.StartsWith('.') && !part.EndsWith('.')
                && !part.EndsWith(".lock", StringComparison.Ordinal));
    }
}
