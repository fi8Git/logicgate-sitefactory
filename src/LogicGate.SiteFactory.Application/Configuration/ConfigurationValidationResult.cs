namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>An immutable snapshot of configuration diagnostics.</summary>
public sealed class ConfigurationValidationResult
{
    public ConfigurationValidationResult(IEnumerable<ConfigurationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        ConfigurationDiagnostic[] snapshot = [.. diagnostics];

        if (Array.Exists(snapshot, static d => d is null))
            throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(diagnostics));

        Diagnostics = Array.AsReadOnly(snapshot
            .OrderBy(static d => d.PropertyPath, StringComparer.Ordinal)
            .ThenBy(static d => d.Code.Value, StringComparer.Ordinal)
            .ThenBy(static d => d.Message, StringComparer.Ordinal)
            .ThenBy(static d => d.Severity)
            .ToArray());
    }

    /// <summary>Diagnostics ordered by property path, code, message and severity.</summary>
    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }

    /// <summary>True when no diagnostic has Error severity, including an empty result.</summary>
    public bool IsValid => !Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error);
}
