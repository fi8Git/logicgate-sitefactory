using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Applies create-once, custom and protected intentions without comparing client content or replacing files.</summary>
public sealed class ClientFileApplier
{
    private readonly AtomicFileWriter writer;

    public ClientFileApplier() : this(new AtomicFileWriter())
    {
    }

    internal ClientFileApplier(AtomicFileWriter writer) => this.writer = writer;

    /// <remarks>
    /// Content is required and encoded only for an absent create-once file with action Create.
    /// Protected reports the write prohibition without inspecting existence. Custom and preserve-only
    /// intentions never create directories. No ownership state, backup, sidecar or merge is produced.
    /// </remarks>
    public async Task<FileGenerationResult> ApplyAsync(
        GenerationRoot root,
        PlannedFile file,
        string? content = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        bool? destinationExisted = null;
        GeneratedContentHash? proposedHash = null;

        FileGenerationResult Observe(FileGenerationResult result) =>
            new(result.FileId, result.Status, result.Error,
                new(file, destinationExisted, proposedHash: proposedHash));

        GenerationExecutionError? blocked = FileGenerationStrategyGuard.GetBlockingError(file.Strategy, file.Action);
        if (blocked is not null) return Observe(new(file.Id, FileGenerationStatus.Conflict, blocked));
        ClientFileDecision initial = ClientFileStrategy.Decide(file, destinationExists: false);
        root.Resolve(file.RelativePath);
        if (initial is ClientFileDecision.Conflict or ClientFileDecision.Protect) return Observe(Result(file, initial));

        try
        {
            using AtomicWriteSession? existing = AtomicWriteSession.OpenExisting(root, file.RelativePath, cancellationToken);
            existing?.VerifyDirectories();
            destinationExisted = existing?.DestinationExists() ?? false;
            ClientFileDecision decision = ClientFileStrategy.Decide(file, destinationExisted.Value);
            cancellationToken.ThrowIfCancellationRequested();
            if (decision != ClientFileDecision.Create) return Observe(Result(file, decision));

            ArgumentNullException.ThrowIfNull(content);
            GeneratedFileContent prepared = new(content);
            proposedHash = prepared.Hash;
            using AtomicWriteSession? created = existing is null ? AtomicWriteSession.Open(root, file.RelativePath, cancellationToken) : null;
            AtomicWriteSession session = existing ?? created!;
            writer.Reach(AtomicWriteCheckpoint.DirectoriesOpened, cancellationToken);
            session.VerifyDirectories();
            if (session.DestinationExists()) return Observe(Result(file, ClientFileDecision.Preserve));
            writer.Reach(AtomicWriteCheckpoint.DestinationInspected, cancellationToken);

            Task<bool> StillAbsent(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                session.VerifyDirectories();
                return Task.FromResult(!session.DestinationExists());
            }

            // The precondition avoids needless promotion attempts. CreateNew also forbids overwriting
            // a file appearing after that check; never fall back to replacement or copying.
            bool written = await writer.WritePreparedAsync(session, prepared.Bytes, AtomicFileWriteMode.CreateNew,
                StillAbsent, cancellationToken).ConfigureAwait(false);
            return Observe(Result(file, written ? ClientFileDecision.Create : ClientFileDecision.Preserve));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Observe(new(file.Id, FileGenerationStatus.Failed,
                new(new("SF-GEN-FILE-015"), "The client file could not be inspected or created safely.")));
        }
    }

    private static FileGenerationResult Result(PlannedFile file, ClientFileDecision decision) => decision switch
    {
        ClientFileDecision.Create => new(file.Id, FileGenerationStatus.Created),
        ClientFileDecision.Preserve => new(file.Id, FileGenerationStatus.Preserved),
        ClientFileDecision.Skip => new(file.Id, FileGenerationStatus.Skipped),
        ClientFileDecision.Protect => new(file.Id, FileGenerationStatus.Protected),
        ClientFileDecision.Conflict => new(file.Id, FileGenerationStatus.Conflict, new(new(file.Strategy switch
        {
            FileGenerationStrategy.Custom => "SF-GEN-FILE-008",
            FileGenerationStrategy.Protected => "SF-GEN-FILE-009",
            _ => "SF-GEN-FILE-014",
        }), "The planned action is not allowed by the file preservation strategy.")),
        _ => throw new InvalidOperationException("Unknown client file decision."),
    };
}
