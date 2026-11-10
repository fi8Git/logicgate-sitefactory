namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Reported file outcome, distinct from a planned action or an ownership strategy.</summary>
public enum FileGenerationStatus
{
    Created,
    Updated,
    Preserved,
    Skipped,
    Conflict,
    Failed,
    Unchanged,
    Protected,
}
