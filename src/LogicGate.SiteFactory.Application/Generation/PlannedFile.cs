namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A file intention with a stable identity, logical origin and target, but no physical source or content.</summary>
public sealed record PlannedFile
{
    public PlannedFile(
        PlannedFileId id,
        string relativePath,
        PlannedFileOrigin origin,
        FileGenerationStrategy strategy,
        PlannedFileAction action)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(origin);

        // Require an already canonical, slash-separated relative path on every OS.
        // Resolving a target root, filesystem constraints and collisions belongs to later stages.
        LogicalFilePath.Validate(relativePath, nameof(relativePath));

        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(strategy));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        Id = id;
        RelativePath = relativePath;
        Origin = origin;
        Strategy = strategy;
        Action = action;
    }

    public PlannedFileId Id { get; }

    public string RelativePath { get; }

    public PlannedFileOrigin Origin { get; }

    public FileGenerationStrategy Strategy { get; }

    public PlannedFileAction Action { get; }
}
