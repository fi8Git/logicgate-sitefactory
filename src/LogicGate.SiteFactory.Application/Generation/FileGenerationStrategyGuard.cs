namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Blocks unsupported automation before I/O. Passing this guard is not permission to write.</summary>
public static class FileGenerationStrategyGuard
{
    /// <remarks>
    /// Only explicitly supported strategies can continue to their own policy and filesystem preconditions.
    /// Unknown values (including future enum members) never inherit generated-file behavior.
    /// </remarks>
    public static GenerationExecutionError? GetBlockingError(FileGenerationStrategy strategy, PlannedFileAction action)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));

        return strategy switch
        {
            FileGenerationStrategy.Generated or FileGenerationStrategy.CreateOnce => null,
            FileGenerationStrategy.Merge => new(new("SF-GEN-FILE-006"), "No structured merge handler is implemented; the file was not changed."),
            FileGenerationStrategy.Custom or FileGenerationStrategy.Protected when action == PlannedFileAction.Preserve => null,
            FileGenerationStrategy.Custom => new(new("SF-GEN-FILE-008"), "The planned action is not allowed by the file preservation strategy."),
            FileGenerationStrategy.Protected => new(new("SF-GEN-FILE-009"), "The planned action is not allowed by the file preservation strategy."),
            _ => new(new("SF-GEN-FILE-002"), "The file strategy is unknown or not supported; the file was not changed."),
        };
    }
}
