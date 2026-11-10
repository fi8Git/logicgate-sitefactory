namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A preservation decision, not an executed filesystem operation.</summary>
public enum ClientFileDecision
{
    Create,
    Preserve,
    Skip,
    Protect,
    Conflict,
}
