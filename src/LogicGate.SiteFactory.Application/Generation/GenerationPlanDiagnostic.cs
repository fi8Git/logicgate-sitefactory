using LogicGate.SiteFactory.Application.Configuration;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A stable diagnostic about a plan, with an optional logical property path.</summary>
public sealed record GenerationPlanDiagnostic
{
    public GenerationPlanDiagnostic(
        DiagnosticCode code,
        DiagnosticSeverity severity,
        string message,
        string? propertyPath = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        if (propertyPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);
        }

        Code = code;
        Severity = severity;
        Message = message;
        PropertyPath = propertyPath;
    }

    public DiagnosticCode Code { get; }

    public DiagnosticSeverity Severity { get; }

    public string Message { get; }

    public string? PropertyPath { get; }
}
