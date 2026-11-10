namespace LogicGate.SiteFactory.Runtime.Hosting;

/// <summary>A read-only view supplied by a specialized startup adapter, not owned by the lifecycle.</summary>
public interface IRuntimeStartupReadiness
{
    /// <summary>All local prerequisites are valid; a binding snapshot alone is insufficient.</summary>
    bool ConfigurationValidated { get; }

    /// <summary>The specialized adapter's current completed validation permits readiness.</summary>
    bool IsReady { get; }
}
