using System.Reflection;
using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationReportTests
{
    [Theory]
    [InlineData(FileGenerationStatus.Created, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Updated, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Unchanged, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Preserved, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Skipped, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Protected, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Conflict, GenerationExecutionStatus.Conflict)]
    [InlineData(FileGenerationStatus.Failed, GenerationExecutionStatus.Failed)]
    public void ActualOutcomeIsNeverInferredFromThePlannedCreateAction(FileGenerationStatus status, GenerationExecutionStatus expected)
    {
        FileGenerationResult actual = Observed("file", status);
        GenerationReport report = new(new("0.1.0-dev"), new(new("plan"), [actual], []));

        Assert.Equal(expected, report.Status);
        GenerationReportOperation operation = Assert.Single(report.Operations);
        Assert.Equal(status, operation.Result);
        Assert.Equal(PlannedFileAction.Create, operation.PlannedAction);
        Assert.Equal("file.txt", operation.Path);
        Assert.Equal("sitefactory", operation.SourceComponent);
        Assert.Equal("0.1.0-dev", operation.SourceVersion);
        Assert.Equal(status is FileGenerationStatus.Conflict or FileGenerationStatus.Failed ? 1 : 0, report.Diagnostics.Count);
        Assert.Equal(status == FileGenerationStatus.Conflict ? "SF-GEN-FILE-004" : null, operation.ConflictCode);
    }

    [Fact]
    public void FailurePrecedesConflictWithoutDroppingEarlierSuccessfulOperations()
    {
        FileGenerationResult[] results = [Observed("created", FileGenerationStatus.Created), Observed("conflict", FileGenerationStatus.Conflict)];
        GenerationReport report = new(new("0.1.0-dev"), new(new("plan"), results, [new(new("SF-GEN-FILE-015"), "Private failure.")]));

        Assert.Equal(GenerationExecutionStatus.Failed, report.Status);
        Assert.Equal([FileGenerationStatus.Created, FileGenerationStatus.Conflict], report.Operations.Select(static operation => operation.Result));
        Assert.Equal(1, report.Diagnostics[0].OperationIndex);
        Assert.Null(report.Diagnostics[1].OperationIndex);
        Assert.DoesNotContain("Private failure", report.Diagnostics[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureBeforeAnyFileDoesNotInventFileOutcomes()
    {
        GenerationReport report = new(new("0.1.0-dev"), new(new("not-published"), [], [new(new("SF-GEN-FILE-015"), "Private.")]));

        Assert.Equal(GenerationExecutionStatus.Failed, report.Status);
        Assert.Empty(report.Operations);
        Assert.Null(Assert.Single(report.Diagnostics).OperationIndex);
    }

    [Fact]
    public void LegacyResultsRemainSupportedButCannotBeExportedAsObservedOperations()
    {
        FileGenerationResult legacy = new(new("file"), FileGenerationStatus.Created);
        Assert.Null(legacy.Observation);
        GenerationExecutionResult execution = new(new("plan"), [legacy], []);

        Assert.Throws<ArgumentException>(nameof(execution), () => new GenerationReport(new("0.1.0-dev"), execution));
    }

    [Fact]
    public void ObservationMustBelongToTheSameFileAndHashesCannotClaimAnUnobservedDestination()
    {
        PlannedFile file = File("first");
        Assert.Throws<ArgumentException>("observation", () => new FileGenerationResult(new("other"), FileGenerationStatus.Created, null, new(file)));
        GeneratedContentHash hash = new(new string('A', 64));
        Assert.Throws<ArgumentException>("currentHash", () => new FileGenerationObservation(file, currentHash: hash));
        Assert.Throws<ArgumentException>("currentHash", () => new FileGenerationObservation(file, false, currentHash: hash));
        Assert.Equal(hash, new FileGenerationObservation(file, true, currentHash: hash).CurrentHash);
    }

    [Fact]
    public void MissingContentObservationsAndSourceVersionsStayExplicitlyUnknown()
    {
        PlannedFile file = new(new("template-file"), "README.md", new(PlannedFileOriginKind.Template, "fixture-template"),
            FileGenerationStrategy.Protected, PlannedFileAction.Preserve);
        FileGenerationResult result = new(file.Id, FileGenerationStatus.Protected, null, new(file));
        GenerationReport report = new(new("0.1.0-dev"), new(new("plan"), [result], []));

        GenerationReportOperation operation = Assert.Single(report.Operations);
        Assert.Null(operation.SourceVersion);
        Assert.Null(operation.DestinationExistedAtInspection);
        Assert.Null(operation.PreviousHash);
        Assert.Null(operation.CurrentHash);
        Assert.Null(operation.ProposedHash);
    }

    [Theory]
    [InlineData("SF-GEN-FILE-001")]
    [InlineData("SF-GEN-FILE-015")]
    [InlineData("SF-GEN-FILE-025")]
    public void KnownCodesUseFixedExplanationsInsteadOfArbitraryMessages(string code)
    {
        GenerationReport report = new(new("0.1.0-dev"), new(new("plan"), [], [new(new(code), "<PRIVATE_MESSAGE>")]));

        GenerationReportDiagnostic diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.DoesNotContain("PRIVATE_MESSAGE", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<PRIVATE_CODE>")]
    [InlineData("SF-GEN-FILE-015\n<PRIVATE_DETAIL>")]
    [InlineData("SF-GEN-FILE-999")]
    public void UnknownCodesDoNotLeakThroughThePublicationBoundary(string code)
    {
        GenerationReport report = new(new("0.1.0-dev"), new(new("plan"), [], [new(new(code), "<PRIVATE_MESSAGE>")]));
        GenerationReportDiagnostic diagnostic = Assert.Single(report.Diagnostics);

        Assert.Equal(GenerationExecutionStatus.Failed, report.Status);
        Assert.Null(diagnostic.Code);
        Assert.Equal("An unrecognized execution error was reported; its details were omitted.", diagnostic.Message);
    }

    [Fact]
    public void ReportSnapshotsAreImmutableAndDoNotRetainRawExecutionErrorsOrIds()
    {
        List<FileGenerationResult> results = [Observed("z", FileGenerationStatus.Conflict), Observed("a", FileGenerationStatus.Created)];
        GenerationReport report = new(new("0.1.0-dev"), new(new("private-plan-id"), results, []));
        results.Clear();

        Assert.Equal(["z.txt", "a.txt"], report.Operations.Select(static operation => operation.Path));
        Assert.Throws<NotSupportedException>(() => ((IList<GenerationReportOperation>)report.Operations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<GenerationReportDiagnostic>)report.Diagnostics).Clear());
        foreach (Type type in new[] { typeof(GenerationReport), typeof(GenerationReportOperation), typeof(GenerationReportDiagnostic) })
        {
            Assert.All(type.GetProperties(), static property => Assert.Null(property.SetMethod));
            Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
            Assert.DoesNotContain(type.GetProperties(), static property => property.PropertyType == typeof(GenerationExecutionResult)
                || property.PropertyType == typeof(GenerationExecutionError) || property.PropertyType == typeof(PlannedFileId));
        }
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>("siteFactoryVersion", () => new GenerationReport(null!, new(new("plan"), [], [])));
        Assert.Throws<ArgumentNullException>("execution", () => new GenerationReport(new("0.1.0-dev"), null!));
        Assert.Throws<ArgumentNullException>("file", () => new FileGenerationObservation(null!));
    }

    private static PlannedFile File(string id) => new(new(id), id + ".txt", new(PlannedFileOriginKind.Generator, "sitefactory"),
        FileGenerationStrategy.Generated, PlannedFileAction.Create);

    private static FileGenerationResult Observed(string id, FileGenerationStatus status)
    {
        PlannedFile file = File(id);
        return new(file.Id, status, status is FileGenerationStatus.Conflict or FileGenerationStatus.Failed
            ? new(new("SF-GEN-FILE-004"), "Not published.") : null, new(file));
    }
}
