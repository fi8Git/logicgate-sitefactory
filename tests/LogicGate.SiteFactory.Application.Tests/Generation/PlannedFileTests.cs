using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class PlannedFileTests
{
    [Theory]
    [InlineData(FileGenerationStrategy.Generated)]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Merge)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public void FileDescribesAnExplicitStrategyWithoutChangingItsLogicalPath(FileGenerationStrategy strategy)
    {
        PlannedFile file = CreateFile("src/Exemple fictif/Équipe.cs", strategy);

        Assert.Equal("src/Exemple fictif/Équipe.cs", file.RelativePath);
        Assert.Equal(strategy, file.Strategy);
        Assert.Equal(file, CreateFile(file.RelativePath, strategy));
        Assert.NotEqual(file, CreateFile("README.md", strategy));
    }

    [Fact]
    public void StrategyIsPartOfTheFileDescription()
    {
        Assert.NotEqual(
            CreateFile("README.md", FileGenerationStrategy.Generated),
            CreateFile("README.md", FileGenerationStrategy.CreateOnce));
    }

    [Theory]
    [InlineData(PlannedFileAction.Create)]
    [InlineData(PlannedFileAction.Preserve)]
    [InlineData(PlannedFileAction.Update)]
    [InlineData(PlannedFileAction.Conflict)]
    public void EveryActionIsRepresentedWithExplicitIdentityAndLogicalOrigin(PlannedFileAction action)
    {
        PlannedFile file = new(
            new("template-readme"), "README.md",
            new(PlannedFileOriginKind.Template, "fixture-template"),
            FileGenerationStrategy.CreateOnce, action);

        Assert.Equal(new PlannedFileId("template-readme"), file.Id);
        Assert.Equal(new PlannedFileOrigin(PlannedFileOriginKind.Template, "fixture-template"), file.Origin);
        Assert.Equal(action, file.Action);
        Assert.Equal(file, new PlannedFile(new("template-readme"), "README.md",
            new(PlannedFileOriginKind.Template, "fixture-template"), FileGenerationStrategy.CreateOnce, action));
    }

    [Fact]
    public void ActionSetContainsOnlyTheFourDeclaredIntentions()
    {
        Assert.Equal(
            [PlannedFileAction.Create, PlannedFileAction.Preserve, PlannedFileAction.Update, PlannedFileAction.Conflict],
            Enum.GetValues<PlannedFileAction>());
    }

    [Fact]
    public void StableIdentityDoesNotDependOnThePathOriginStrategyOrAction()
    {
        PlannedFileId id = new("readme");
        PlannedFile original = new(id, "README.md", new(PlannedFileOriginKind.Generator, "sitefactory"),
            FileGenerationStrategy.Generated, PlannedFileAction.Create);
        PlannedFile revised = new(new("readme"), "docs/README.md", new(PlannedFileOriginKind.Template, "fixture-template"),
            FileGenerationStrategy.CreateOnce, PlannedFileAction.Preserve);

        Assert.Equal(original.Id, revised.Id);
        Assert.NotEqual(original, revised);
        Assert.Equal("README.md", original.RelativePath);
        Assert.Equal(PlannedFileAction.Create, original.Action);
    }

    [Fact]
    public void NewMetadataParticipatesInValueEquality()
    {
        PlannedFile file = CreateFile("README.md", FileGenerationStrategy.Generated);

        Assert.NotEqual(file, new PlannedFile(new("other-id"), file.RelativePath, file.Origin, file.Strategy, file.Action));
        Assert.NotEqual(file, new PlannedFile(file.Id, file.RelativePath,
            new(PlannedFileOriginKind.Module, file.Origin.ComponentId), file.Strategy, file.Action));
        Assert.NotEqual(file, new PlannedFile(file.Id, file.RelativePath,
            new(file.Origin.Kind, "other-component"), file.Strategy, file.Action));
        Assert.NotEqual(file, new PlannedFile(file.Id, file.RelativePath, file.Origin, file.Strategy, PlannedFileAction.Update));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("C:drive-relative.txt")]
    [InlineData("//server/share/file.txt")]
    [InlineData("src\\file.cs")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside.txt")]
    [InlineData("src/../outside.txt")]
    [InlineData("src/./file.cs")]
    [InlineData("src//file.cs")]
    [InlineData("src/")]
    [InlineData("src/file.cs ")]
    [InlineData("src/file.cs.")]
    [InlineData("src/file\0.cs")]
    [InlineData("src/file\n.cs")]
    [InlineData("src/*.cs")]
    [InlineData("src/file?.cs")]
    [InlineData("src/<file>.cs")]
    [InlineData("src/file|other.cs")]
    [InlineData("src/\"file\".cs")]
    [InlineData("~/file.cs")]
    [InlineData("~someone/file.cs")]
    [InlineData("$HOME/file.cs")]
    [InlineData("${HOME}/file.cs")]
    [InlineData("%USERPROFILE%/file.cs")]
    [InlineData("src/$USER/file.cs")]
    [InlineData("\\\\server\\share\\file.cs")]
    [InlineData("C:\\Data\\Example\\file.cs")]
    public void NonCanonicalLogicalPathsAreRejectedWithoutResolvingThem(string relativePath)
    {
        Assert.Throws<ArgumentException>(nameof(relativePath), () => CreateFile(relativePath, FileGenerationStrategy.Generated));
    }

    [Fact]
    public void NullPathAndUnknownStrategyAreRejected()
    {
        Assert.Throws<ArgumentNullException>("relativePath", () => CreateFile(null!, FileGenerationStrategy.Generated));
        Assert.Throws<ArgumentOutOfRangeException>("strategy", () => CreateFile("README.md", (FileGenerationStrategy)99));
    }

    [Fact]
    public void IdentityAndOriginCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>("id", () => new PlannedFile(null!, "README.md",
            new(PlannedFileOriginKind.Generator, "sitefactory"), FileGenerationStrategy.Generated, PlannedFileAction.Create));
        Assert.Throws<ArgumentNullException>("origin", () => new PlannedFile(new("readme"), "README.md",
            null!, FileGenerationStrategy.Generated, PlannedFileAction.Create));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void UnknownActionIsRejected(int action)
    {
        Assert.Throws<ArgumentOutOfRangeException>(nameof(action), () => new PlannedFile(new("readme"), "README.md",
            new(PlannedFileOriginKind.Generator, "sitefactory"), FileGenerationStrategy.Generated, (PlannedFileAction)action));
    }

    private static PlannedFile CreateFile(string relativePath, FileGenerationStrategy strategy)
    {
        return new PlannedFile(new("test-file-001"), relativePath,
            new(PlannedFileOriginKind.Generator, "sitefactory"), strategy, PlannedFileAction.Create);
    }
}
