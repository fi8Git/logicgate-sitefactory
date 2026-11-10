namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// A previously validated plan and a canonical relative logical root, never a resolved physical directory.
/// These guards reject known structural blockers; they do not authorize writes or inspect target state.
/// </summary>
public sealed class GenerationExecutionRequest
{
    public GenerationExecutionRequest(GenerationPlan plan, string targetRoot)
    {
        ArgumentNullException.ThrowIfNull(plan);
        LogicalFilePath.Validate(targetRoot, nameof(targetRoot));

        if (GenerationPlanPathValidator.ValidatePaths([targetRoot]).Count != 0)
        {
            throw new ArgumentException("A portable, unreserved logical root is required.", nameof(targetRoot));
        }

        if (plan.HasErrors
            || plan.Files.Any(static file => file.Action == PlannedFileAction.Conflict)
            || GenerationPlanPathValidator.Validate(plan.Files).Count != 0
            || plan.Files.Select(static file => file.Id).Distinct().Count() != plan.Files.Count
            || plan.Files.Any(static file => new FileGenerationStrategyRules(file.Strategy).ForbidsAutomaticWrites
                && file.Action != PlannedFileAction.Preserve))
        {
            throw new ArgumentException("A validated plan without blocking errors or conflicts is required.", nameof(plan));
        }

        Plan = plan;
        TargetRoot = targetRoot;
    }

    public GenerationPlan Plan { get; }

    /// <summary>Logical path using '/', without expansion, normalization or filesystem resolution.</summary>
    public string TargetRoot { get; }
}
