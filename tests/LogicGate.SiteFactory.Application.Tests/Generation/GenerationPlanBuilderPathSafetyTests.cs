using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationPlanBuilderPathSafetyTests
{
    [Theory]
    [InlineData("README.md", "readme.md", "SF-GEN-FILE-012")]
    [InlineData("assets", "assets/site.css", "SF-GEN-002")]
    [InlineData("Assets", "assets/site.css", "SF-GEN-002")]
    [InlineData("docs/first.md", "Docs/second.md", "SF-GEN-FILE-012")]
    public void IncompatibleLogicalTargetsBlockTheWholePlan(string firstPath, string secondPath, string code)
    {
        TemplateFile first = File("first", firstPath);
        TemplateFile second = File("second", secondPath);

        GenerationPlan forward = Build([first, second]);
        GenerationPlan backward = Build([second, first]);

        Assert.True(forward.HasErrors);
        Assert.Empty(forward.Files);
        Assert.Contains(forward.Diagnostics, diagnostic => diagnostic.Code.Value == code);
        Assert.Equal(forward.Diagnostics, backward.Diagnostics);
        Assert.Empty(backward.Files);
    }

    [Theory]
    [InlineData("NUL.txt")]
    [InlineData("src/aux/readme.md")]
    [InlineData("COM1")]
    public void WindowsDeviceNamesAreRejectedWithoutOpeningThem(string path)
    {
        GenerationPlan plan = Build([File("unsafe", path), File("otherwise-safe", "README.md")]);

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        GenerationPlanDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("SF-GEN-FILE-025", diagnostic.Code.Value);
        Assert.Equal(path, diagnostic.PropertyPath);
    }

    [Fact]
    public void DuplicateExactTargetsStayRejectedWithAPathDiagnostic()
    {
        GenerationPlan plan = Build([File("first", "README.md"), File("second", "README.md")]);

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        Assert.Contains(plan.Diagnostics, static diagnostic => diagnostic.Code.Value == "SF-GEN-002" && diagnostic.PropertyPath == "README.md");
    }

    private static TemplateFile File(string id, string path) =>
        new(new(id), path, "logical/source", FileGenerationStrategy.Generated);

    private static GenerationPlan Build(IEnumerable<TemplateFile> files) => GenerationPlanBuilder.Build(
        new("path-safety-plan"),
        new ClientConfigurationV1(1, new("acme-services", "Acme Services"),
            new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"),
            new([]), new("docker-compose-vps"), new(true, false)),
        new TemplateManifest(new("fixture-template"), new("0.2.0"), [new("fixture-product")], files));
}
