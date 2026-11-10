namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>A configuration loaded successfully, or a diagnostic explaining why loading failed.</summary>
public sealed class ConfigurationLoadResult
{
    private ConfigurationLoadResult(ClientConfigurationV1? configuration, ConfigurationValidationResult validation)
    {
        Configuration = configuration;
        Validation = validation;
    }

    public ClientConfigurationV1? Configuration { get; }

    /// <summary>Loading diagnostics only; success does not imply functional validity.</summary>
    public ConfigurationValidationResult Validation { get; }

    public static ConfigurationLoadResult Success(ClientConfigurationV1 configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new(configuration, new([]));
    }

    public static ConfigurationLoadResult Failure(ConfigurationDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (diagnostic.Severity != DiagnosticSeverity.Error)
            throw new ArgumentException("A failed load requires an error diagnostic.", nameof(diagnostic));

        return new(null, new([diagnostic]));
    }
}
