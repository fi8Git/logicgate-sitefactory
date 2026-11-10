using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Application.Modules;

/// <summary>One captured descriptor, or blocking diagnostics; never a partial successful resolution.</summary>
public sealed class ModuleResolutionResult
{
    private ModuleResolutionResult(ModuleDescriptor? module, IReadOnlyList<ConfigurationDiagnostic> diagnostics)
    {
        Module = module;
        Diagnostics = diagnostics;
    }

    public ModuleDescriptor? Module { get; }

    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }

    public bool IsSuccess => Module is not null;

    public static ModuleResolutionResult Success(ModuleDescriptor module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return new(module, Array.AsReadOnly(Array.Empty<ConfigurationDiagnostic>()));
    }

    public static ModuleResolutionResult Failure(IEnumerable<ConfigurationDiagnostic> diagnostics)
    {
        ConfigurationValidationResult validation = new(diagnostics);
        if (validation.IsValid)
            throw new ArgumentException("A failed module resolution requires an error diagnostic.", nameof(diagnostics));

        return new(null, validation.Diagnostics);
    }
}
