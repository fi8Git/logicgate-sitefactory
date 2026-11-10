using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Infrastructure.Configuration;

namespace LogicGate.SiteFactory.Cli.Commands;

internal static class ConfigurationCommandInput
{
    public static async Task<(ClientConfigurationV1? Configuration, int ExitCode)> LoadAndValidateAsync(
        string configurationPath,
        IConfigurationSelectionValidator? selectionValidator,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        ConfigurationLoadResult loaded = await YamlConfigurationLoader.LoadAsync(configurationPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        ConfigurationValidationResult validation = loaded.Configuration is { } configuration
            ? ClientConfigurationV1Validator.Validate(configuration, selectionValidator)
            : loaded.Validation;

        // Both commands preserve the same ordered, safe diagnostics and exit codes.
        foreach (ConfigurationDiagnostic diagnostic in validation.Diagnostics)
        {
            string line = $"{diagnostic.Code.Value} {diagnostic.PropertyPath ?? "-"} {diagnostic.Message}";
            await standardError.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!validation.IsValid)
        {
            int exitCode = validation.Diagnostics.Any(static d => d.Code.Value == "SF-CONFIG-SCHEMA-002")
                ? CliExitCode.UnsupportedVersion
                : CliExitCode.InvalidConfiguration;
            return (null, exitCode);
        }

        return (loaded.Configuration, CliExitCode.Success);
    }
}
