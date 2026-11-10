using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Application.Modules;

/// <summary>Codes follow ADR-0007 section 64. Raw registration values and provider errors are never echoed.</summary>
internal static class ModuleDiagnostics
{
    internal static ConfigurationDiagnostic Unknown(ModuleId id) => Create("SF-MODULE-001",
        "Le module demandé est absent du catalogue.", id);

    internal static ConfigurationDiagnostic InvalidId() => Create("SF-MODULE-002",
        "L'identifiant déclaré du module est invalide.", member: "id");

    internal static ConfigurationDiagnostic InvalidVersion(ModuleId id) => Create("SF-MODULE-003",
        "La version déclarée du module doit être une version SemVer exacte.", id, "version");

    internal static ConfigurationDiagnostic Duplicate(ModuleId id) => Create("SF-MODULE-005",
        "Plusieurs enregistrements déclarent le même identifiant de module.", id);

    internal static ConfigurationDiagnostic InvalidRegistration(ModuleId? id = null) => Create("SF-MODULE-029",
        "L'enregistrement du module est absent ou incohérent avec son descripteur.", id);

    private static ConfigurationDiagnostic Create(string code, string message, ModuleId? id = null, string? member = null)
    {
        string path = id is null ? "modules" : $"modules[{id.Value}]";
        if (member is not null) path += "." + member;
        return new(new(code), DiagnosticSeverity.Error, message, path);
    }
}
