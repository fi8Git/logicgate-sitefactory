using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Applies one generated file using an explicit caller-owned reference, without persisting generation state.</summary>
public sealed class GeneratedFileApplier
{
    private readonly AtomicFileWriter writer;

    public GeneratedFileApplier() : this(new AtomicFileWriter())
    {
    }

    internal GeneratedFileApplier(AtomicFileWriter writer) => this.writer = writer;

    /// <remarks>
    /// The caller must associate lastGenerated with this file and supply trusted rendered text.
    /// The root requires exclusive application access: the final identity/hash check is optimistic,
    /// not a kernel compare-and-swap or a lock against other processes changing the file afterwards.
    /// No-op results neither persist ownership nor adopt previously untracked files.
    /// </remarks>
    public async Task<FileGenerationResult> ApplyAsync(
        GenerationRoot root,
        PlannedFile file,
        GeneratedFileContent content,
        GeneratedContentHash? lastGenerated = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        bool? destinationExisted = null;
        GeneratedContentHash? inspectedHash = null;

        FileGenerationResult Observe(FileGenerationResult result) =>
            new(result.FileId, result.Status, result.Error,
                new(file, destinationExisted, lastGenerated, inspectedHash, content.Hash));

        GenerationExecutionError? blocked = FileGenerationStrategyGuard.GetBlockingError(file.Strategy, file.Action);
        // A guard refusal does not consume the candidate or reference, and observes no destination.
        if (blocked is not null) return new(file.Id, FileGenerationStatus.Conflict, blocked, new(file));
        // Validate strategy and planned action before opening or creating anything.
        GeneratedFileDecision initial = GeneratedFileStrategy.Decide(file, content.Hash, null, lastGenerated);
        if (file.Action is not (PlannedFileAction.Create or PlannedFileAction.Update)) return Observe(Result(file, initial));
        root.Resolve(file.RelativePath);

        try
        {
            using AtomicWriteSession session = AtomicWriteSession.Open(root, file.RelativePath, cancellationToken,
                createDirectories: file.Action == PlannedFileAction.Create);
            writer.Reach(AtomicWriteCheckpoint.DirectoriesOpened, cancellationToken);
            session.VerifyDirectories();
            using AtomicFileSnapshot? observed = await session.ReadDestinationAsync(cancellationToken).ConfigureAwait(false);
            destinationExisted = observed is not null;
            inspectedHash = observed?.Hash;
            GeneratedFileDecision decision = GeneratedFileStrategy.Decide(file, content.Hash, observed?.Hash, lastGenerated);
            writer.Reach(AtomicWriteCheckpoint.DestinationInspected, cancellationToken);

            async Task<bool> StillMatches(CancellationToken token)
            {
                session.VerifyDirectories();
                using AtomicFileSnapshot? current = await session.ReadDestinationAsync(token).ConfigureAwait(false);
                return AtomicFileSnapshot.Matches(observed, current);
            }

            if (decision is GeneratedFileDecision.Create or GeneratedFileDecision.Update)
            {
                bool written = await writer.WritePreparedAsync(session, content.Bytes,
                    decision == GeneratedFileDecision.Create ? AtomicFileWriteMode.CreateNew : AtomicFileWriteMode.CreateOrReplace,
                    StillMatches, cancellationToken).ConfigureAwait(false);
                if (!written) return Observe(ChangedDuringApplication(file));
            }
            else if (decision == GeneratedFileDecision.Unchanged && !await StillMatches(cancellationToken).ConfigureAwait(false))
            {
                return Observe(ChangedDuringApplication(file));
            }

            // Successful promotion is not retroactively cancelled. No-op/conflict paths still observe cancellation.
            if (decision is not (GeneratedFileDecision.Create or GeneratedFileDecision.Update)) cancellationToken.ThrowIfCancellationRequested();
            return Observe(Result(file, decision));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Observe(new(file.Id, FileGenerationStatus.Failed,
                new(new("SF-GEN-FILE-015"), "The generated file could not be inspected or written safely.")));
        }
    }

    private static FileGenerationResult Result(PlannedFile file, GeneratedFileDecision decision) => decision switch
    {
        GeneratedFileDecision.Create => new(file.Id, FileGenerationStatus.Created),
        GeneratedFileDecision.Update => new(file.Id, FileGenerationStatus.Updated),
        GeneratedFileDecision.Unchanged => new(file.Id, FileGenerationStatus.Unchanged),
        GeneratedFileDecision.ModifiedLocally => Conflict(file, "SF-GEN-FILE-004", "The file differs from the last generated content."),
        GeneratedFileDecision.MissingReference => Conflict(file, "SF-GEN-FILE-014", "A trusted last-generated reference is required before replacement."),
        GeneratedFileDecision.PlanMismatch => Conflict(file, "SF-GEN-FILE-014", "The planned action does not authorize the required file change."),
        _ => throw new InvalidOperationException("Unknown generated file decision."),
    };

    private static FileGenerationResult ChangedDuringApplication(PlannedFile file) =>
        Conflict(file, "SF-GEN-FILE-013", "The destination changed during application; no candidate was promoted.");

    private static FileGenerationResult Conflict(PlannedFile file, string code, string message) =>
        new(file.Id, FileGenerationStatus.Conflict, new(new(code), message));
}
