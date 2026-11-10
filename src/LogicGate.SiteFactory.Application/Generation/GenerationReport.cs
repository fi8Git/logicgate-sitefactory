using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// A report of supplied actual file outcomes, not a plan projection or certification of a complete client generation.
/// Producers must supply all outcomes/errors for their operation, and non-sensitive logical metadata.
/// </summary>
public sealed class GenerationReport
{
    public GenerationReport(SiteFactoryVersion siteFactoryVersion, GenerationExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(siteFactoryVersion);
        ArgumentNullException.ThrowIfNull(execution);
        if (execution.Files.Any(static file => file.Observation is null))
        {
            throw new ArgumentException("Observed file outcomes are required for a report.", nameof(execution));
        }

        List<GenerationReportOperation> operations = [];
        List<GenerationReportDiagnostic> diagnostics = [];
        foreach (FileGenerationResult file in execution.Files)
        {
            if (file.Error is not null) diagnostics.Add(new(file.Error, operations.Count));
            operations.Add(new(file, siteFactoryVersion));
        }

        diagnostics.AddRange(execution.Errors.Select(static error => new GenerationReportDiagnostic(error, null)));
        SiteFactoryVersion = siteFactoryVersion;
        Status = execution.Status;
        Operations = operations.AsReadOnly();
        Diagnostics = diagnostics.AsReadOnly();
    }

    public int SchemaVersion { get; } = 1;

    public SiteFactoryVersion SiteFactoryVersion { get; }

    /// <summary>Aggregate status of reported outcomes only; does not assert build/test success or plan completeness.</summary>
    public GenerationExecutionStatus Status { get; }

    public IReadOnlyList<GenerationReportOperation> Operations { get; }

    public IReadOnlyList<GenerationReportDiagnostic> Diagnostics { get; }
}
