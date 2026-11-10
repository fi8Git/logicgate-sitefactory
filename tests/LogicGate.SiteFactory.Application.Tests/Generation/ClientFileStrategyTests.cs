using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class ClientFileStrategyTests
{
    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Create, false, ClientFileDecision.Create)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Create, true, ClientFileDecision.Preserve)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Preserve, false, ClientFileDecision.Skip)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Preserve, true, ClientFileDecision.Preserve)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Update, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Update, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Conflict, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Conflict, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Create, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Create, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Preserve, false, ClientFileDecision.Skip)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Preserve, true, ClientFileDecision.Preserve)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Update, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Update, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Conflict, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Conflict, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Create, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Create, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Preserve, false, ClientFileDecision.Protect)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Preserve, true, ClientFileDecision.Protect)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Update, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Update, true, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Conflict, false, ClientFileDecision.Conflict)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Conflict, true, ClientFileDecision.Conflict)]
    public void ExactPreservationMatrixNeverAuthorizesReplacement(
        FileGenerationStrategy strategy, PlannedFileAction action, bool exists, ClientFileDecision expected)
    {
        PlannedFile file = File(strategy, action);

        Assert.Equal(expected, ClientFileStrategy.Decide(file, exists));
        Assert.Equal(expected, ClientFileStrategy.Decide(file, exists));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Generated)]
    [InlineData(FileGenerationStrategy.Merge)]
    public void OtherStrategiesAreNotReinterpreted(FileGenerationStrategy strategy) =>
        Assert.Throws<ArgumentException>("file", () => ClientFileStrategy.Decide(File(strategy, PlannedFileAction.Create), false));

    [Fact]
    public void MissingFileIsAContractError() =>
        Assert.Throws<ArgumentNullException>("file", () => ClientFileStrategy.Decide(null!, false));

    private static PlannedFile File(FileGenerationStrategy strategy, PlannedFileAction action) =>
        new(new("client-file"), "customizations/file.txt", new(PlannedFileOriginKind.Template, "fixture-template"), strategy, action);
}
