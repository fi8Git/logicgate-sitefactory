namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Aggregate outcome of reported results, not a permission to execute a plan.</summary>
public enum GenerationExecutionStatus
{
    Succeeded,
    Conflict,
    Failed,
}
