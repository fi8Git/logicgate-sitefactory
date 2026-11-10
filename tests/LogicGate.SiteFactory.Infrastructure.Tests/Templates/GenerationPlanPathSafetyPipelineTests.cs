using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Templates;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Templates;

public sealed class GenerationPlanPathSafetyPipelineTests
{
    private static readonly string[] SupportedProducts = ["documentation"];

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("../secret")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\temp\\file")]
    [InlineData("src\\mixed/file.cs")]
    [InlineData("src/mixed\\file.cs")]
    [InlineData("src//file.cs")]
    [InlineData("src/./file.cs")]
    [InlineData("src/../file.cs")]
    public void UnsafeManifestTargetsAreRejectedAtTheContractBoundaryWithoutProducingDeclarations(string path)
    {
        TemplateCatalogReadResult read = ReadManifest(path);

        Assert.False(read.IsSuccess);
        Assert.Empty(read.Manifests);
        Assert.Same(TemplateDiagnostics.InvalidManifest, read.Diagnostic);
    }

    [Theory]
    [InlineData("README.md", "readme.md", "SF-GEN-FILE-012")]
    [InlineData("docs/a.md", "Docs/b.md", "SF-GEN-FILE-012")]
    [InlineData("assets", "assets/site.css", "SF-GEN-002")]
    [InlineData("NUL.txt", "README.md", "SF-GEN-FILE-025")]
    public async Task CanonicalButIncompatibleTargetsCannotReachAUsablePlan(string first, string second, string expectedCode)
    {
        TemplateCatalogReadResult read = ReadManifest(first, second);
        Assert.True(read.IsSuccess);
        ClientConfigurationV1 configuration = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateResolutionResult resolved = await new TemplateResolver(new ParsedCatalog(read))
            .ResolveAsync(configuration, TestContext.Current.CancellationToken);
        Assert.True(resolved.IsSuccess);
        Assert.NotNull(resolved.Template);

        GenerationPlan plan = GenerationPlanBuilder.Build(new("path-safety-plan"), configuration, resolved.Template);

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code.Value == expectedCode);
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData(".git/hooks/post-checkout")]
    [InlineData(".GIT/config")]
    [InlineData("foo/.git/config")]
    [InlineData("foo/.Git/hooks/pre-commit")]
    [InlineData("foo/.git")]
    public async Task GitInternalManifestTargetsInvalidateTheEntirePlan(string path)
    {
        TemplateCatalogReadResult read = ReadManifest("README.md", path);
        Assert.True(read.IsSuccess);
        ClientConfigurationV1 configuration = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateResolutionResult resolved = await new TemplateResolver(new ParsedCatalog(read))
            .ResolveAsync(configuration, TestContext.Current.CancellationToken);
        Assert.True(resolved.IsSuccess);
        Assert.NotNull(resolved.Template);

        GenerationPlan plan = GenerationPlanBuilder.Build(new("git-path-safety-plan"), configuration, resolved.Template);

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        GenerationPlanDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Equal("SF-GEN-FILE-026", diagnostic.Code.Value);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(path, diagnostic.PropertyPath);
    }

    [Fact]
    public async Task GitRelatedOrdinaryFilesRemainPlannableThroughThePipeline()
    {
        string[] paths = [".gitignore", ".gitattributes", ".github/workflows/ci.yml", "github.txt", "some.git.txt"];
        TemplateResolutionResult resolved = await new TemplateResolver(new ParsedCatalog(ReadManifest(paths)))
            .ResolveAsync(Configuration(), TestContext.Current.CancellationToken);
        Assert.True(resolved.IsSuccess);
        Assert.NotNull(resolved.Template);

        GenerationPlan plan = GenerationPlanBuilder.Build(new("ordinary-git-files-plan"), Configuration(), resolved.Template);

        Assert.False(plan.HasErrors);
        Assert.Empty(plan.Diagnostics);
        Assert.Equal(paths.Order(StringComparer.Ordinal), plan.Files.Select(static file => file.RelativePath));
        Assert.All(plan.Files, static file => Assert.Equal(PlannedFileAction.Create, file.Action));
    }

    [Fact]
    public async Task ExactDuplicateTargetsAreAlreadyBlockedByTheResolver()
    {
        TemplateResolutionResult resolved = await new TemplateResolver(new ParsedCatalog(ReadManifest("README.md", "README.md")))
            .ResolveAsync(Configuration(), TestContext.Current.CancellationToken);

        Assert.False(resolved.IsSuccess);
        Assert.Null(resolved.Template);
        Assert.Same(TemplateDiagnostics.InvalidManifest, resolved.Diagnostic);
    }

    private static TemplateCatalogReadResult ReadManifest(params string[] paths)
    {
        string json = JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            templateId = "documentation-minimal",
            templateVersion = "0.2.0",
            supportedProductIds = SupportedProducts,
            files = paths.Select(static (path, index) => new
            {
                id = "test-file-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                relativePath = path,
                source = "logical/source",
                strategy = "generated",
            }),
        });
        return new TemplateManifestJsonReader().Read(json);
    }

    private static ClientConfigurationV1 Configuration() => new(1, new("acme-services", "Acme Services"),
        new("documentation", "documentation-minimal", "fr-FR", "https://www.example.test"),
        new([]), new("docker-compose-vps"), new(true, false));

    private sealed class ParsedCatalog(TemplateCatalogReadResult read) : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(read);
        }
    }
}
