using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>UTF-8 without BOM, CRLF/CR normalized to LF, without adding or removing a final newline.</summary>
public sealed class AtomicFileWriter : IAtomicFileWriter
{
    private readonly Action<AtomicWriteCheckpoint>? checkpoint;

    public AtomicFileWriter()
    {
    }

    // Deterministic failure/race injection only; production always uses the real native filesystem backend.
    internal AtomicFileWriter(Action<AtomicWriteCheckpoint> checkpoint) => this.checkpoint = checkpoint;

    public async Task<AtomicFileWriteResult> WriteAsync(
        GenerationRoot root,
        string logicalPath,
        string content,
        AtomicFileWriteMode mode = AtomicFileWriteMode.CreateNew,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(content);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        root.Resolve(logicalPath);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = GeneratedFileContent.Encode(content);

        using AtomicWriteSession session = AtomicWriteSession.Open(root, logicalPath, cancellationToken);
        Reach(AtomicWriteCheckpoint.DirectoriesOpened, cancellationToken);
        session.VerifyDirectories();
        await WritePreparedAsync(session, bytes, mode, null, cancellationToken).ConfigureAwait(false);
        return new(logicalPath, bytes.LongLength);
    }

    internal async Task<bool> WritePreparedAsync(AtomicWriteSession session, ReadOnlyMemory<byte> bytes,
        AtomicFileWriteMode mode, Func<CancellationToken, Task<bool>>? precondition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.VerifyDirectories();
        session.CreateTemporary();
        Reach(AtomicWriteCheckpoint.TemporaryCreated, cancellationToken);
        session.VerifyDirectories();

        // A non-owning wrapper leaves the native handle available for handle-based promotion and cleanup.
        using (FileStream stream = session.OpenStream())
        {
            for (int offset = 0; offset < bytes.Length; offset += Math.Min(4096, bytes.Length - offset))
            {
                await stream.WriteAsync(bytes.Slice(offset, Math.Min(4096, bytes.Length - offset)), cancellationToken).ConfigureAwait(false);
                Reach(AtomicWriteCheckpoint.ChunkWritten, cancellationToken);
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            stream.Flush(flushToDisk: true);
        }

        Reach(AtomicWriteCheckpoint.ContentFlushed, cancellationToken);
        session.VerifyDirectories();
        if (precondition is not null && !await precondition(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        session.Commit(mode);
        // Rename is the commit point. Cancellation after it must not misreport a completed write as cancelled.
        return true;
    }

    internal void Reach(AtomicWriteCheckpoint stage, CancellationToken cancellationToken)
    {
        checkpoint?.Invoke(stage);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

internal enum AtomicWriteCheckpoint
{
    DirectoriesOpened,
    TemporaryCreated,
    ChunkWritten,
    ContentFlushed,
    DestinationInspected,
}
