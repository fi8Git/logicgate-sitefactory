namespace LogicGate.SiteFactory.Runtime.Contracts;

public sealed record StartupValidationId
{
    public StartupValidationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}

public enum StartupValidationSeverity { Success, Warning, BlockingFailure }

/// <summary>Codes and logical properties are separate from the safe, human-readable summary.</summary>
public sealed record StartupValidationResult
{
    public StartupValidationResult(StartupValidationId validationId, StartupValidationSeverity severity,
        string? errorCode, string summary, string? propertyPath = null)
    {
        ArgumentNullException.ThrowIfNull(validationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        if (severity != StartupValidationSeverity.Success) ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ValidationId = validationId;
        Severity = severity;
        ErrorCode = errorCode;
        Summary = summary;
        PropertyPath = propertyPath;
    }

    public StartupValidationId ValidationId { get; }
    public StartupValidationSeverity Severity { get; }
    public string? ErrorCode { get; }
    public string Summary { get; }
    public string? PropertyPath { get; }
    public override string ToString() => $"{ErrorCode} {PropertyPath} {Summary}".Trim();
}

public sealed class RuntimeStartupReport
{
    public RuntimeStartupReport(IEnumerable<StartupValidationResult> results, bool cancelled = false)
    {
        ArgumentNullException.ThrowIfNull(results);
        StartupValidationResult[] snapshot = results.ToArray();
        if (snapshot.Any(static result => result is null)) throw new ArgumentException("Results cannot contain null.", nameof(results));
        Results = Array.AsReadOnly(snapshot);
        Cancelled = cancelled;
    }

    public int SchemaVersion { get; } = 1;
    public IReadOnlyList<StartupValidationResult> Results { get; }
    public bool Cancelled { get; }
    public bool IsReady => !Cancelled && Results.Count > 0
        && Results.All(static result => result.Severity != StartupValidationSeverity.BlockingFailure);
    public bool HasWarnings => Results.Any(static result => result.Severity == StartupValidationSeverity.Warning);
}

/// <summary>A read-only check. Implementations must not repair state or contact external providers.</summary>
public interface IStartupValidation
{
    StartupValidationId Id { get; }
    Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken);
}

public interface IRuntimeStartupValidator
{
    Task<RuntimeStartupReport> ValidateAsync(CancellationToken cancellationToken);
}
