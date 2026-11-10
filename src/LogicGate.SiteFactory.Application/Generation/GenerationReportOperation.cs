using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Actual outcome plus logical metadata, with no content, exception or unconstrained file identifier.</summary>
public sealed class GenerationReportOperation
{
    internal GenerationReportOperation(FileGenerationResult result, SiteFactoryVersion version)
    {
        FileGenerationObservation observed = result.Observation
            ?? throw new ArgumentException("Observed file outcomes are required for a report.", nameof(result));
        PlannedFile file = observed.File;
        Path = file.RelativePath;
        Strategy = file.Strategy;
        PlannedAction = file.Action;
        SourceKind = file.Origin.Kind;
        SourceComponent = file.Origin.ComponentId;
        // PlannedFile does not carry a resolved component version. Do not infer a template/module version.
        SourceVersion = file.Origin.Kind == PlannedFileOriginKind.Generator && file.Origin.ComponentId == "sitefactory"
            ? version.Value : null;
        DestinationExistedAtInspection = observed.DestinationExistedAtInspection;
        PreviousHash = observed.PreviousHash;
        CurrentHash = observed.CurrentHash;
        ProposedHash = observed.ProposedHash;
        Result = result.Status;
        ConflictCode = result.Status == FileGenerationStatus.Conflict
            ? new GenerationReportDiagnostic(result.Error!, null).Code : null;
    }

    public string Path { get; }

    public FileGenerationStrategy Strategy { get; }

    public PlannedFileAction PlannedAction { get; }

    public PlannedFileOriginKind SourceKind { get; }

    public string SourceComponent { get; }

    public string? SourceVersion { get; }

    public bool? DestinationExistedAtInspection { get; }

    public GeneratedContentHash? PreviousHash { get; }

    public GeneratedContentHash? CurrentHash { get; }

    public GeneratedContentHash? ProposedHash { get; }

    public FileGenerationStatus Result { get; }

    public string? ConflictCode { get; }
}
