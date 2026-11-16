namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>Initial external file declarations, not permission to write or a planned action.</summary>
public enum TemplateFilePolicy
{
    /// <summary>Replacement requires the engine's proof that previously generated content was not changed locally.</summary>
    Generated,
    /// <summary>Create only when absent, then preserve the client-owned content.</summary>
    CreateOnce,
}
