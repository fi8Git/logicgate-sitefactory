namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A pure decision, not a claim that any file has been written.</summary>
public enum GeneratedFileDecision
{
    Create,
    Update,
    Unchanged,
    ModifiedLocally,
    MissingReference,
    PlanMismatch,
}
