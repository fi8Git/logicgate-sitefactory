using System.Globalization;
using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class FileGenerationStrategyGuardTests
{
    [Theory]
    [InlineData(PlannedFileAction.Create)]
    [InlineData(PlannedFileAction.Update)]
    [InlineData(PlannedFileAction.Preserve)]
    [InlineData(PlannedFileAction.Conflict)]
    public void MergeAlwaysReportsTheMissingStructuredHandler(PlannedFileAction action)
    {
        GenerationExecutionError? error = FileGenerationStrategyGuard.GetBlockingError(FileGenerationStrategy.Merge, action);

        Assert.NotNull(error);
        Assert.Equal("SF-GEN-FILE-006", error.Code.Value);
        Assert.Equal("No structured merge handler is implemented; the file was not changed.", error.Message);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Custom, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Protected, "SF-GEN-FILE-009")]
    public void ClientOwnedStrategiesMayOnlyContinueWithPreserve(FileGenerationStrategy strategy, string code)
    {
        Assert.Null(FileGenerationStrategyGuard.GetBlockingError(strategy, PlannedFileAction.Preserve));
        foreach (PlannedFileAction action in new[] { PlannedFileAction.Create, PlannedFileAction.Update, PlannedFileAction.Conflict })
        {
            GenerationExecutionError? error = FileGenerationStrategyGuard.GetBlockingError(strategy, action);
            Assert.NotNull(error);
            Assert.Equal(code, error.Code.Value);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)] // First value after the current canonical strategies.
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void UnknownOrFutureValuesNeverInheritGeneratedBehavior(int strategy)
    {
        foreach (PlannedFileAction action in Enum.GetValues<PlannedFileAction>())
        {
            GenerationExecutionError? error = FileGenerationStrategyGuard.GetBlockingError((FileGenerationStrategy)strategy, action);
            Assert.NotNull(error);
            Assert.Equal("SF-GEN-FILE-002", error.Code.Value);
            Assert.Equal("The file strategy is unknown or not supported; the file was not changed.", error.Message);
        }
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Generated)]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    public void ImplementedStrategiesStillRequireTheirOwnPolicyChecks(FileGenerationStrategy strategy)
    {
        foreach (PlannedFileAction action in Enum.GetValues<PlannedFileAction>())
        {
            // No blanket write authorization: action/ownership/content checks remain in each implementation.
            Assert.Null(FileGenerationStrategyGuard.GetBlockingError(strategy, action));
        }
    }

    [Fact]
    public void AGuardPassDoesNotAuthorizeOverwritingALocalGeneratedModification()
    {
        PlannedFile file = new(new("generated"), "file.txt", new(PlannedFileOriginKind.Template, "fixture-template"),
            FileGenerationStrategy.Generated, PlannedFileAction.Update);
        GeneratedContentHash previous = new(new string('A', 64));
        GeneratedContentHash edited = new(new string('B', 64));

        Assert.Null(FileGenerationStrategyGuard.GetBlockingError(file.Strategy, file.Action));
        Assert.Equal(GeneratedFileDecision.ModifiedLocally, GeneratedFileStrategy.Decide(file, edited, edited, previous));
    }

    [Fact]
    public void DiagnosticsStayIdenticalAcrossCallsAndCultures()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        FileGenerationStrategy[] strategies = [FileGenerationStrategy.Merge, FileGenerationStrategy.Custom, FileGenerationStrategy.Protected, (FileGenerationStrategy)5];
        GenerationExecutionError?[] expected = strategies.Select(static strategy => FileGenerationStrategyGuard.GetBlockingError(strategy, PlannedFileAction.Update)).ToArray();
        try
        {
            foreach (string name in new[] { "fr-FR", "tr-TR", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                Assert.Equal(expected, strategies.Select(static strategy => FileGenerationStrategyGuard.GetBlockingError(strategy, PlannedFileAction.Update)));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void AnInvalidActionRemainsAContractError() =>
        Assert.Throws<ArgumentOutOfRangeException>("action", () => FileGenerationStrategyGuard.GetBlockingError(FileGenerationStrategy.Generated, (PlannedFileAction)99));
}
