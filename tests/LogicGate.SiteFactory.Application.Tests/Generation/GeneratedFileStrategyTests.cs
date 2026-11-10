using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GeneratedFileStrategyTests
{
    private static readonly GeneratedContentHash Old = new(new string('A', 64));
    private static readonly GeneratedContentHash New = new(new string('B', 64));
    private static readonly GeneratedContentHash Local = new(new string('C', 64));

    [Fact]
    public void AbsentDestinationCanBeCreatedButAnUpdateDoesNotRecreateADeletedFile()
    {
        Assert.Equal(GeneratedFileDecision.Create, GeneratedFileStrategy.Decide(File(), New, null, null));
        Assert.Equal(GeneratedFileDecision.Create, GeneratedFileStrategy.Decide(File(), New, null, Old));
        Assert.Equal(GeneratedFileDecision.PlanMismatch, GeneratedFileStrategy.Decide(File(PlannedFileAction.Update), New, null, Old));
    }

    [Fact]
    public void IdenticalContentIsANoOpEvenWithoutAnOwnershipReference()
    {
        Assert.Equal(GeneratedFileDecision.Unchanged, GeneratedFileStrategy.Decide(File(), New, New, New));
        Assert.Equal(GeneratedFileDecision.Unchanged, GeneratedFileStrategy.Decide(File(), New, New, null));
    }

    [Fact]
    public void AnExplicitUpdateRequiresTheCurrentFileToMatchTheLastGeneration()
    {
        PlannedFile file = File(PlannedFileAction.Update);
        Assert.Equal(GeneratedFileDecision.Update, GeneratedFileStrategy.Decide(file, New, Old, Old));
        Assert.Equal(GeneratedFileDecision.MissingReference, GeneratedFileStrategy.Decide(file, New, Old, null));
        Assert.Equal(GeneratedFileDecision.ModifiedLocally, GeneratedFileStrategy.Decide(file, New, Local, Old));
        Assert.Equal(GeneratedFileDecision.ModifiedLocally, GeneratedFileStrategy.Decide(file, New, New, Old));
    }

    [Fact]
    public void ACreatePlanCannotSilentlyBecomeAnUpdate()
    {
        Assert.Equal(GeneratedFileDecision.PlanMismatch, GeneratedFileStrategy.Decide(File(), New, Old, Old));
    }

    [Theory]
    [InlineData(PlannedFileAction.Preserve)]
    [InlineData(PlannedFileAction.Conflict)]
    public void NonWritingPlanActionsDoNotAcquirePermission(PlannedFileAction action)
    {
        Assert.Equal(GeneratedFileDecision.PlanMismatch, GeneratedFileStrategy.Decide(File(action), New, null, null));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Merge)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public void OtherStrategiesAreNotInterpreted(FileGenerationStrategy strategy)
    {
        Assert.Throws<ArgumentException>("file", () => GeneratedFileStrategy.Decide(File(strategy: strategy), New, null, null));
    }

    [Fact]
    public void Sha256ValuesAreCanonicalImmutableAndIndependentOfInfrastructure()
    {
        GeneratedContentHash lower = new(new string('a', 64));
        Assert.Equal(Old, lower);
        Assert.Equal(Old.GetHashCode(), lower.GetHashCode());
        Assert.Equal(new string('A', 64), lower.Value);
        Assert.Equal(typeof(PlannedFile).Assembly, typeof(GeneratedContentHash).Assembly);
        Assert.All(typeof(GeneratedContentHash).GetProperties(), static property => Assert.Null(property.SetMethod));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("sha256:abc")]
    [InlineData("ABC")]
    public void InvalidHashShapesAreRejected(string hash)
    {
        Assert.Throws<ArgumentException>("value", () => new GeneratedContentHash(hash));
    }

    [Fact]
    public void HashValidationRejectsNonHexNonAsciiAndWrongLengths()
    {
        foreach (string hash in new[] { new string('g', 64), new string('Ａ', 64), new string('A', 63), new string('A', 65) })
        {
            Assert.Throws<ArgumentException>("value", () => new GeneratedContentHash(hash));
        }

        Assert.Throws<ArgumentNullException>("value", () => new GeneratedContentHash(null!));
        Assert.Throws<ArgumentNullException>("file", () => GeneratedFileStrategy.Decide(null!, New, null, null));
        Assert.Throws<ArgumentNullException>("expected", () => GeneratedFileStrategy.Decide(File(), null!, null, null));
    }

    private static PlannedFile File(PlannedFileAction action = PlannedFileAction.Create,
        FileGenerationStrategy strategy = FileGenerationStrategy.Generated) =>
        new(new("generated-file"), "generated/file.txt", new(PlannedFileOriginKind.Template, "fixture-template"), strategy, action);
}
