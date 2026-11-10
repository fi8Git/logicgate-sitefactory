namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Declared file ownership strategies from ADR-0005, not executable operations.</summary>
public enum FileGenerationStrategy
{
    /// <summary>SiteFactory-generated content; replacement requires unchanged previously generated content.</summary>
    Generated,

    /// <summary>Create only when absent, then preserve the client-owned file.</summary>
    CreateOnce,

    /// <summary>Shared content requiring a structured, format-specific merge handler.</summary>
    Merge,

    /// <summary>Client customizations that SiteFactory must not create, modify or delete automatically.</summary>
    Custom,

    /// <summary>Explicitly forbids all automatic writes in the normal generation or upgrade pipeline.</summary>
    Protected,
}
