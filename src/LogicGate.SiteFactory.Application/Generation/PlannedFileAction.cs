namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>An intended action only; no inspection, conflict resolution or execution is performed.</summary>
public enum PlannedFileAction
{
    Create,
    Preserve,
    Update,
    Conflict,
}
