namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>Closed provider failures. No machine detail, exception, arbitrary message or successful partial result.</summary>
public enum TemplateReadFailure
{
    InvalidDefinition = 1,
    UnsupportedTemplate = 2,
    UnknownContent = 3,
    ContentUnavailable = 4,
}
