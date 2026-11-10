using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Templates;

public sealed class TemplateFileTests
{
    [Theory]
    [InlineData(FileGenerationStrategy.Generated)]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Merge)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public void DeclarationPreservesItsLogicalSourceAndStrategyWithoutAPlannedAction(FileGenerationStrategy strategy)
    {
        TemplateFile file = new(new("team-readme"), "docs/Équipe.md", "structure/team-readme", strategy);

        Assert.Equal(new PlannedFileId("team-readme"), file.Id);
        Assert.Equal("docs/Équipe.md", file.RelativePath);
        Assert.Equal("structure/team-readme", file.Source);
        Assert.Equal(strategy, file.Strategy);
        Assert.Equal(file, new TemplateFile(file.Id, file.RelativePath, file.Source, strategy));
        Assert.NotEqual(file, new TemplateFile(new("other-id"), file.RelativePath, file.Source, strategy));
        Assert.NotEqual(file, new TemplateFile(file.Id, "README.md", file.Source, strategy));
        Assert.NotEqual(file, new TemplateFile(file.Id, file.RelativePath, "structure/other-source", strategy));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    [InlineData("C:relative")]
    [InlineData("\\\\server\\share")]
    [InlineData("//server/share")]
    [InlineData("src\\file")]
    [InlineData("~/file")]
    [InlineData("$HOME/file")]
    [InlineData("%USERPROFILE%/file")]
    [InlineData("../file")]
    [InlineData("src/../file")]
    [InlineData("src/./file")]
    [InlineData("src//file")]
    [InlineData("src/")]
    [InlineData("src/file ")]
    [InlineData("src/file.")]
    [InlineData("src/file\0")]
    [InlineData("src/file\n")]
    [InlineData("src/*")]
    [InlineData("https://example.test/source")]
    public void BothTargetAndSourceUseCanonicalRelativeLogicalPaths(string path)
    {
        Assert.Throws<ArgumentException>("relativePath", () => new TemplateFile(new("readme"), path, "structure/readme", FileGenerationStrategy.CreateOnce));
        Assert.Throws<ArgumentException>("source", () => new TemplateFile(new("readme"), "README.md", path, FileGenerationStrategy.CreateOnce));
    }

    [Fact]
    public void NullPathsAndUnknownStrategyAreRejected()
    {
        Assert.Throws<ArgumentNullException>("id", () => new TemplateFile(null!, "README.md", "structure/readme", FileGenerationStrategy.CreateOnce));
        Assert.Throws<ArgumentNullException>("relativePath", () => new TemplateFile(new("readme"), null!, "structure/readme", FileGenerationStrategy.CreateOnce));
        Assert.Throws<ArgumentNullException>("source", () => new TemplateFile(new("readme"), "README.md", null!, FileGenerationStrategy.CreateOnce));
        Assert.Throws<ArgumentOutOfRangeException>("strategy", () => new TemplateFile(new("readme"), "README.md", "structure/readme", (FileGenerationStrategy)99));
    }

    [Fact]
    public void IdentityIsPreservedWhenPathSourceOrStrategyChanges()
    {
        TemplateFile first = new(new("readme"), "README.md", "structure/readme", FileGenerationStrategy.CreateOnce);
        TemplateFile revised = new(new("readme"), "docs/README.md", "other/source", FileGenerationStrategy.Protected);

        Assert.Equal(first.Id, revised.Id);
        Assert.NotEqual(first, revised);
    }
}
