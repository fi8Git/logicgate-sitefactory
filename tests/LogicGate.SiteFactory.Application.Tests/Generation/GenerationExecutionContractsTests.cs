using System.Reflection;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationExecutionContractsTests
{
    [Fact]
    public void RequestKeepsTheSuppliedPlanAndLogicalRootWithoutReplanningOrDroppingWarnings()
    {
        GenerationPlan plan = Plan([File("readme", "README.md")],
            [new(new("TEST-WARNING"), DiagnosticSeverity.Warning, "A warning.")]);

        GenerationExecutionRequest request = new(plan, "clients/Acme");

        Assert.Same(plan, request.Plan);
        Assert.Equal("clients/Acme", request.TargetRoot);
        Assert.Single(request.Plan.Diagnostics);
        Assert.Equal(PlannedFileAction.Create, Assert.Single(request.Plan.Files).Action);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("/")]
    [InlineData("../client")]
    [InlineData("clients/../client")]
    [InlineData("/clients/acme")]
    [InlineData("C:\\clients\\acme")]
    [InlineData("clients\\acme")]
    [InlineData("//server/share")]
    [InlineData("~/client")]
    [InlineData("$HOME/client")]
    [InlineData("%USERPROFILE%/client")]
    [InlineData("clients//acme")]
    [InlineData("clients/acme/")]
    [InlineData("clients/.Git/acme")]
    [InlineData("clients/CON")]
    public void RootMustBeACanonicalPortableLogicalPath(string targetRoot)
    {
        Assert.Throws<ArgumentException>(nameof(targetRoot), () => new GenerationExecutionRequest(Plan(), targetRoot));
    }

    [Fact]
    public void RequestRejectsErrorDiagnosticsAndConflictActionsEvenWithoutDiagnostics()
    {
        GenerationPlan errorPlan = Plan(diagnostics: [new(new("TEST-ERROR"), DiagnosticSeverity.Error, "Blocked.")]);
        GenerationPlan conflictPlan = Plan([File("readme", "README.md", action: PlannedFileAction.Conflict)]);

        Assert.Throws<ArgumentException>("plan", () => new GenerationExecutionRequest(errorPlan, "clients/acme"));
        Assert.Throws<ArgumentException>("plan", () => new GenerationExecutionRequest(conflictPlan, "clients/acme"));
    }

    [Theory]
    [InlineData("README.md", "README.md")]
    [InlineData("README.md", "readme.md")]
    [InlineData("docs", "docs/readme.md")]
    [InlineData("README.md", "nested/.Git/config")]
    [InlineData("README.md", "NUL.txt")]
    public void MissingDiagnosticsCannotBypassExistingLogicalPathValidation(string firstPath, string secondPath)
    {
        GenerationPlan plan = Plan([File("first", firstPath), File("second", secondPath)]);

        Assert.False(plan.HasErrors);
        Assert.Throws<ArgumentException>(nameof(plan), () => new GenerationExecutionRequest(plan, "clients/acme"));
    }

    [Fact]
    public void RequestRejectsAmbiguousFileIdsWithoutChangingThePlanContract()
    {
        GenerationPlan plan = Plan([File("same-id", "a.md"), File("same-id", "b.md")]);

        Assert.Equal(2, plan.Files.Count);
        Assert.Throws<ArgumentException>(nameof(plan), () => new GenerationExecutionRequest(plan, "clients/acme"));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Create)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Update)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Create)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Update)]
    public void RequestCannotAuthorizeWritesForbiddenByDeclaredOwnership(FileGenerationStrategy strategy, PlannedFileAction action)
    {
        GenerationPlan blocked = Plan([File("client-file", "custom/file.cs", strategy, action)]);
        GenerationPlan preserved = Plan([File("client-file", "custom/file.cs", strategy, PlannedFileAction.Preserve)]);

        Assert.Throws<ArgumentException>("plan", () => new GenerationExecutionRequest(blocked, "clients/acme"));
        Assert.Same(preserved, new GenerationExecutionRequest(preserved, "clients/acme").Plan);
    }

    [Theory]
    [InlineData(FileGenerationStatus.Created, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Updated, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Unchanged, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Preserved, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Skipped, GenerationExecutionStatus.Succeeded)]
    [InlineData(FileGenerationStatus.Conflict, GenerationExecutionStatus.Conflict)]
    [InlineData(FileGenerationStatus.Failed, GenerationExecutionStatus.Failed)]
    [InlineData(FileGenerationStatus.Protected, GenerationExecutionStatus.Succeeded)]
    public void OverallStatusFollowsReportedFileOutcomes(FileGenerationStatus status, GenerationExecutionStatus expected)
    {
        FileGenerationResult file = Result("readme", status);
        GenerationExecutionResult result = new(new("plan-1"), [file], []);

        Assert.Equal(expected, result.Status);
        Assert.Same(file, Assert.Single(result.Files));
        Assert.Equal(new PlannedFileId("readme"), result.Files[0].FileId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutionOrFileFailureTakesPrecedenceOverConflict(bool executionWideError)
    {
        List<FileGenerationResult> files = [Result("conflict", FileGenerationStatus.Conflict)];
        if (!executionWideError)
        {
            files.Add(Result("failure", FileGenerationStatus.Failed));
        }

        GenerationExecutionResult result = new(new("plan-1"), files, executionWideError ? [Error()] : []);

        Assert.Equal(GenerationExecutionStatus.Failed, result.Status);
        Assert.Equal(FileGenerationStatus.Conflict, result.Files[0].Status);
        Assert.NotNull(result.Files[0].Error);
    }

    [Fact]
    public void AnExecutionFailureCanBeReportedBeforeAnyFileAndAnEmptyPlanCanSucceed()
    {
        GenerationExecutionError error = Error();
        GenerationExecutionResult failed = new(new("plan-1"), [], [error]);
        GenerationExecutionResult empty = new(new("empty-plan"), [], []);

        Assert.Empty(failed.Files);
        Assert.Same(error, Assert.Single(failed.Errors));
        Assert.Equal(GenerationExecutionStatus.Failed, failed.Status);
        Assert.Equal(GenerationExecutionStatus.Succeeded, empty.Status);
    }

    [Theory]
    [InlineData(FileGenerationStatus.Created, false)]
    [InlineData(FileGenerationStatus.Updated, false)]
    [InlineData(FileGenerationStatus.Unchanged, false)]
    [InlineData(FileGenerationStatus.Preserved, false)]
    [InlineData(FileGenerationStatus.Skipped, false)]
    [InlineData(FileGenerationStatus.Conflict, true)]
    [InlineData(FileGenerationStatus.Failed, true)]
    [InlineData(FileGenerationStatus.Protected, false)]
    public void OnlyConflictsAndFailuresRequireAStableErrorExplanation(FileGenerationStatus status, bool requiresError)
    {
        GenerationExecutionError error = Error();
        FileGenerationResult file = new(new("readme"), status, requiresError ? error : null);

        Assert.Equal(requiresError ? error : null, file.Error);
        Assert.Throws<ArgumentException>("error", () => new FileGenerationResult(new("readme"), status, requiresError ? null : error));
    }

    [Fact]
    public void HumanMessagesDoNotDetermineMachineErrorIdentity()
    {
        GenerationExecutionError first = new(new("TEST-EXECUTION-001"), "First explanation.");
        GenerationExecutionError second = new(new("TEST-EXECUTION-001"), "Different explanation.");

        Assert.Equal(first.Code, second.Code);
        Assert.NotEqual(first.Message, second.Message);
    }

    [Fact]
    public void ResultSnapshotsPreserveSuppliedOrderAndCannotBeMutatedOrChangeGlobalStatus()
    {
        FileGenerationResult[] expected =
        [
            Result("z-created", FileGenerationStatus.Created),
            Result("a-preserved", FileGenerationStatus.Preserved),
            Result("m-skipped", FileGenerationStatus.Skipped),
        ];
        List<FileGenerationResult> files = [.. expected];
        GenerationExecutionError error = Error();
        List<GenerationExecutionError> errors = [error];
        GenerationExecutionResult result = new(new("plan-1"), files, errors);

        files.Clear();
        errors.Clear();

        Assert.Equal(expected, result.Files);
        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Equal(GenerationExecutionStatus.Failed, result.Status);
        Assert.Throws<NotSupportedException>(() => ((IList<FileGenerationResult>)result.Files).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<FileGenerationResult>)result.Files)[0] = expected[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<GenerationExecutionError>)result.Errors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<GenerationExecutionError>)result.Errors)[0] = Error());
    }

    [Fact]
    public void ConflictingReportsForTheSameFileIdAreRejected()
    {
        FileGenerationResult[] files = [Result("readme", FileGenerationStatus.Created), Result("readme", FileGenerationStatus.Failed)];

        Assert.Throws<ArgumentException>(nameof(files), () => new GenerationExecutionResult(new("plan-1"), files, []));
    }

    [Fact]
    public void IncompleteContractsAndUnknownOutcomesAreRejected()
    {
        Assert.Throws<ArgumentNullException>("plan", () => new GenerationExecutionRequest(null!, "clients/acme"));
        Assert.Throws<ArgumentNullException>("targetRoot", () => new GenerationExecutionRequest(Plan(), null!));
        Assert.Throws<ArgumentNullException>("planId", () => new GenerationExecutionResult(null!, [], []));
        Assert.Throws<ArgumentNullException>("files", () => new GenerationExecutionResult(new("plan-1"), null!, []));
        Assert.Throws<ArgumentNullException>("errors", () => new GenerationExecutionResult(new("plan-1"), [], null!));
        Assert.Throws<ArgumentException>("files", () => new GenerationExecutionResult(new("plan-1"), [null!], []));
        Assert.Throws<ArgumentException>("errors", () => new GenerationExecutionResult(new("plan-1"), [], [null!]));
        Assert.Throws<ArgumentNullException>("fileId", () => new FileGenerationResult(null!, FileGenerationStatus.Skipped));
        Assert.Throws<ArgumentOutOfRangeException>("status", () => new FileGenerationResult(new("readme"), (FileGenerationStatus)99));
        Assert.Throws<ArgumentNullException>("code", () => new GenerationExecutionError(null!, "Failure."));
        Assert.Throws<ArgumentNullException>("message", () => new GenerationExecutionError(new("TEST-ERROR"), null!));
        Assert.Throws<ArgumentException>("message", () => new GenerationExecutionError(new("TEST-ERROR"), " "));
    }

    [Fact]
    public void ExecutionDataContractsStayImmutableInApplication()
    {
        Type[] contracts = [typeof(GenerationExecutionRequest), typeof(GenerationExecutionResult), typeof(FileGenerationResult), typeof(GenerationExecutionError)];
        foreach (Type contract in contracts)
        {
            Assert.Equal(typeof(GenerationPlan).Assembly, contract.Assembly);
            Assert.True(contract.IsSealed);
            Assert.All(contract.GetProperties(), static property => Assert.Null(property.SetMethod));
            Assert.Empty(contract.GetFields(BindingFlags.Instance | BindingFlags.Public));
        }
    }

    private static GenerationPlan Plan(IEnumerable<PlannedFile>? files = null, IEnumerable<GenerationPlanDiagnostic>? diagnostics = null) =>
        new(new("plan-1"), new("acme"), new("fixture-product"), new("fixture-template"), files ?? [], diagnostics ?? []);

    private static PlannedFile File(string id, string path,
        FileGenerationStrategy strategy = FileGenerationStrategy.Generated, PlannedFileAction action = PlannedFileAction.Create) =>
        new(new(id), path, new(PlannedFileOriginKind.Template, "fixture-template"), strategy, action);

    private static GenerationExecutionError Error() => new(new("TEST-EXECUTION-001"), "A bounded failure explanation.");

    private static FileGenerationResult Result(string id, FileGenerationStatus status) =>
        new(new(id), status, status is FileGenerationStatus.Conflict or FileGenerationStatus.Failed ? Error() : null);
}
