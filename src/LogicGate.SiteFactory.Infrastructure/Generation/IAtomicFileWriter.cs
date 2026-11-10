namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Writes one text file, with explicit replacement policy; never executes an ownership strategy.</summary>
public interface IAtomicFileWriter
{
    /// <summary>Publishes complete UTF-8/LF content; replacement never implies ownership authorization.</summary>
    /// <remarks>
    /// Requires a root controlled by the caller; hostile processes sharing its identity are outside the isolation boundary.
    /// Failure may leave newly created directories. Abrupt termination may leave a temporary file.
    /// Cancellation is observed before the native rename commit point, not after a successful promotion.
    /// </remarks>
    /// <exception cref="IOException">Confinement, filesystem capabilities, access, creation or promotion failed.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was observed before promotion.</exception>
    Task<AtomicFileWriteResult> WriteAsync(
        GenerationRoot root,
        string logicalPath,
        string content,
        AtomicFileWriteMode mode = AtomicFileWriteMode.CreateNew,
        CancellationToken cancellationToken = default);
}
