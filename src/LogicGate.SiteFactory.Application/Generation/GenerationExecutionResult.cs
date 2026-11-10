namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// Immutable reported outcomes in caller-supplied plan order. Failure takes precedence over conflict, then success.
/// This contract neither executes operations nor guarantees completeness, atomicity or rollback.
/// </summary>
public sealed class GenerationExecutionResult
{
    public GenerationExecutionResult(
        GenerationPlanId planId,
        IEnumerable<FileGenerationResult> files,
        IEnumerable<GenerationExecutionError> errors)
    {
        ArgumentNullException.ThrowIfNull(planId);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(errors);

        FileGenerationResult[] fileSnapshot = files.ToArray();
        GenerationExecutionError[] errorSnapshot = errors.ToArray();
        if (fileSnapshot.Any(static file => file is null))
        {
            throw new ArgumentException("File results cannot contain null entries.", nameof(files));
        }

        if (fileSnapshot.Select(static file => file.FileId).Distinct().Count() != fileSnapshot.Length)
        {
            throw new ArgumentException("Each planned file can have only one reported outcome.", nameof(files));
        }

        if (errorSnapshot.Any(static error => error is null))
        {
            throw new ArgumentException("Errors cannot contain null entries.", nameof(errors));
        }

        PlanId = planId;
        Files = Array.AsReadOnly(fileSnapshot);
        Errors = Array.AsReadOnly(errorSnapshot);
        Status = errorSnapshot.Length != 0 || fileSnapshot.Any(static file => file.Status == FileGenerationStatus.Failed)
            ? GenerationExecutionStatus.Failed
            : fileSnapshot.Any(static file => file.Status == FileGenerationStatus.Conflict)
                ? GenerationExecutionStatus.Conflict
                : GenerationExecutionStatus.Succeeded;
    }

    public GenerationPlanId PlanId { get; }

    public IReadOnlyList<FileGenerationResult> Files { get; }

    /// <summary>Execution-wide failures, including failures before any file is processed. File errors remain in Files.</summary>
    public IReadOnlyList<GenerationExecutionError> Errors { get; }

    public GenerationExecutionStatus Status { get; }
}
