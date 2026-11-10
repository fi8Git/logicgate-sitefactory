using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Application.Modules;

/// <summary>Immutable metadata snapshot of trusted modules composed explicitly by the caller.</summary>
public sealed class ModuleCatalog : IModuleCatalog
{
    public ModuleCatalog(IEnumerable<ModuleRegistration?> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ModuleRegistration?[] snapshot = registrations.ToArray();
        List<ConfigurationDiagnostic> diagnostics = [];
        List<ModuleDescriptor> modules = [];
        HashSet<ModuleId> seen = [];

        // Stable traversal, independent of caller order. No filesystem, reflection or dictionary discovery.
        foreach (ModuleRegistration? registration in snapshot
                     .OrderBy(static registration => registration?.ExpectedId, StringComparer.Ordinal)
                     .ThenBy(static registration => registration?.ExpectedVersion, StringComparer.Ordinal))
        {
            if (registration is null)
            {
                diagnostics.Add(ModuleDiagnostics.InvalidRegistration());
                continue;
            }

            ModuleId id;
            try
            {
                id = new ModuleId(registration.ExpectedId!);
            }
            catch (ArgumentException)
            {
                diagnostics.Add(ModuleDiagnostics.InvalidId());
                continue;
            }

            if (!seen.Add(id)) diagnostics.Add(ModuleDiagnostics.Duplicate(id));

            ModuleVersion version;
            try
            {
                version = new ModuleVersion(registration.ExpectedVersion!);
            }
            catch (ArgumentException)
            {
                diagnostics.Add(ModuleDiagnostics.InvalidVersion(id));
                continue;
            }

            // Read exactly once per valid declaration. Unexpected getter exceptions are programming errors,
            // not invalid-version diagnostics: the SDK already guarantees descriptor invariants.
            ModuleDescriptor? descriptor = registration.Module?.Descriptor;
            if (descriptor is null || descriptor.Id != id || descriptor.Version != version)
            {
                diagnostics.Add(ModuleDiagnostics.InvalidRegistration(id));
                continue;
            }

            modules.Add(descriptor);
        }

        Diagnostics = new ConfigurationValidationResult(diagnostics.Distinct()).Diagnostics;
        Modules = Array.AsReadOnly(IsValid
            ? modules.OrderBy(static module => module.Id.Value, StringComparer.Ordinal).ToArray()
            : Array.Empty<ModuleDescriptor>());
        Capabilities = Array.AsReadOnly(Modules.SelectMany(static module => module.Capabilities)
            .OrderBy(static capability => capability.Value, StringComparer.Ordinal).ToArray());
    }

    public bool IsValid => Diagnostics.Count == 0;

    public IReadOnlyList<ModuleDescriptor> Modules { get; }

    public IReadOnlyList<ModuleCapability> Capabilities { get; }

    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }

    public ModuleResolutionResult Resolve(ModuleId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!IsValid) return ModuleResolutionResult.Failure(Diagnostics);

        ModuleDescriptor? module = Modules.FirstOrDefault(module => module.Id == id);
        return module is null
            ? ModuleResolutionResult.Failure([ModuleDiagnostics.Unknown(id)])
            : ModuleResolutionResult.Success(module);
    }
}
