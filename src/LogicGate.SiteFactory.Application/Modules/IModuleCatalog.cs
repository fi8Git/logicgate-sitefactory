using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Application.Modules;

/// <summary>A fixed catalog of explicitly supplied modules; no discovery or module execution.</summary>
public interface IModuleCatalog
{
    bool IsValid { get; }

    /// <summary>Ordinally ordered metadata; empty if any registration is invalid.</summary>
    IReadOnlyList<ModuleDescriptor> Modules { get; }

    IReadOnlyList<ModuleCapability> Capabilities { get; }

    IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }

    ModuleResolutionResult Resolve(ModuleId id);
}
