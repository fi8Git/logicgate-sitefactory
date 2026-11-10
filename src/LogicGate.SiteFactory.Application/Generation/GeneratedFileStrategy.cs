namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>ADR-0005 generated-file decisions. No I/O, adoption, persistence or implicit replacement permission.</summary>
public static class GeneratedFileStrategy
{
    public static GeneratedFileDecision Decide(
        PlannedFile file,
        GeneratedContentHash expected,
        GeneratedContentHash? current,
        GeneratedContentHash? lastGenerated)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(expected);
        if (file.Strategy != FileGenerationStrategy.Generated)
        {
            throw new ArgumentException("Only the generated strategy is supported.", nameof(file));
        }

        if (file.Action is not (PlannedFileAction.Create or PlannedFileAction.Update))
        {
            return GeneratedFileDecision.PlanMismatch;
        }

        if (current is null)
        {
            return file.Action == PlannedFileAction.Create ? GeneratedFileDecision.Create : GeneratedFileDecision.PlanMismatch;
        }

        // A known local modification is still a conflict even when it happens to equal the new candidate.
        if (lastGenerated is not null && current != lastGenerated)
        {
            return GeneratedFileDecision.ModifiedLocally;
        }

        if (current == expected)
        {
            // No-op only: this does not adopt an untracked file or establish a generation reference.
            return GeneratedFileDecision.Unchanged;
        }

        if (lastGenerated is null)
        {
            return GeneratedFileDecision.MissingReference;
        }

        return file.Action == PlannedFileAction.Update ? GeneratedFileDecision.Update : GeneratedFileDecision.PlanMismatch;
    }
}
