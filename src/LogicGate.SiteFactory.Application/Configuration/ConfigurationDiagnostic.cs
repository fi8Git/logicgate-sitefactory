namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>A configuration diagnostic with no dependency on its source format.</summary>
public sealed record ConfigurationDiagnostic
{
    public ConfigurationDiagnostic(
        DiagnosticCode code,
        DiagnosticSeverity severity,
        string message,
        string? propertyPath = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!Enum.IsDefined(severity))
            throw new ArgumentOutOfRangeException(nameof(severity));

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (propertyPath is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        Code = code;
        Severity = severity;
        Message = message;
        PropertyPath = propertyPath;
    }

    public DiagnosticCode Code { get; }

    public DiagnosticSeverity Severity { get; }

    public string Message { get; }

    /// <summary>An optional logical property path, not a filesystem path.</summary>
    public string? PropertyPath { get; }
}
