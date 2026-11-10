using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;

namespace LogicGate.SiteFactory.Runtime.Hosting;

/// <summary>Closed, startup-only binding at the configuration boundary; no value is echoed in diagnostics.</summary>
public static class RuntimeConfigurationReader
{
    public static RuntimeConfiguration Read(IConfiguration configuration, string hostEnvironment, RuntimeComponent component)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IConfigurationSection section = configuration.GetSection("SiteFactory:Runtime");
        if (!section.Exists())
            throw new RuntimeConfigurationException([new(new("startup.configuration.schema"), StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-001", "The SiteFactory:Runtime section is required.", "Runtime")]);

        foreach (IConfigurationSection property in section.GetChildren())
        {
            if ((!property.Key.Equals("ClientSlug", StringComparison.OrdinalIgnoreCase)
                && !property.Key.Equals("PublicBaseUrl", StringComparison.OrdinalIgnoreCase)) || property.GetChildren().Any())
                throw new RuntimeConfigurationException([new(new("startup.configuration.schema"), StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-002", "Unknown or nested runtime properties are not supported.", "Runtime")]);
        }

        // Host environment is fixed before application settings load. Never accept a later conflicting override
        // or convert the framework's implicit Production default into an explicit runtime identity.
        string? explicitEnvironment = configuration["environment"];
        if (explicitEnvironment is not null && !string.Equals(explicitEnvironment, hostEnvironment, StringComparison.OrdinalIgnoreCase))
            throw new RuntimeConfigurationException([new(new("startup.configuration.schema"), StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-006", "Runtime and host environments must agree.", "Environment")]);

        if (!RuntimeConfiguration.TryCreate(section["ClientSlug"], explicitEnvironment, component, section["PublicBaseUrl"],
            out RuntimeConfiguration? result, out IReadOnlyList<StartupValidationResult> diagnostics))
            throw new RuntimeConfigurationException(diagnostics);
        return result;
    }
}

public sealed class RuntimeConfigurationException : Exception
{
    public RuntimeConfigurationException(IReadOnlyList<StartupValidationResult> diagnostics) : base(string.Join("\n", diagnostics))
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<StartupValidationResult> Diagnostics { get; }
}
