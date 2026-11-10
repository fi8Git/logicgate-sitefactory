using System.Globalization;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationPlanBuilderTests
{
    [Fact]
    public void MinimalPlanCopiesStructuralIdentityAndEachDeclarationWithoutMutatingInputs()
    {
        GenerationPlanId id = new("test-plan");
        ClientConfigurationV1 configuration = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateFile readme = new(new("template-readme"), "README.md", "structure/readme", FileGenerationStrategy.CreateOnce);
        TemplateFile metadata = new(new("template-metadata"), "src/Generated/SiteMetadata.cs", "structure/site-metadata", FileGenerationStrategy.Generated);
        List<TemplateFile> declarations = [metadata, readme];
        TemplateManifest manifest = Manifest(declarations);

        GenerationPlan plan = GenerationPlanBuilder.Build(id, configuration, manifest);
        declarations.Clear();

        Assert.Same(id, plan.Id);
        Assert.Equal(1, plan.SchemaVersion);
        Assert.Equal(new ClientSlug("acme-services"), plan.ClientSlug);
        Assert.Equal(new ProductId("fixture-product"), plan.ProductId);
        Assert.Equal(manifest.TemplateId, plan.TemplateId);
        PlannedFileOrigin origin = new(PlannedFileOriginKind.Template, "fixture-template");
        Assert.Equal(
            [new PlannedFile(readme.Id, readme.RelativePath, origin, readme.Strategy, PlannedFileAction.Create),
                new PlannedFile(metadata.Id, metadata.RelativePath, origin, metadata.Strategy, PlannedFileAction.Create)], plan.Files);
        Assert.Same(readme.Id, plan.Files[0].Id);
        Assert.False(plan.HasErrors);
        Assert.Empty(plan.Diagnostics);
        Assert.Equal([readme, metadata], manifest.Files);
        Assert.True(configuration.Git.InitializeLocalRepository); // A preference, never an operation performed by this builder.
        IList<PlannedFile> exposed = Assert.IsAssignableFrom<IList<PlannedFile>>(plan.Files);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
    }

    [Theory]
    [InlineData("acme-services")]
    [InlineData("atelier-demo")]
    public void ClientIdentityComesFromConfigurationRatherThanTheTemplate(string slug)
    {
        ClientConfigurationV1 configuration = Configuration(slug);
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);

        GenerationPlan plan = Build(configuration, Manifest([File("readme", "README.md")]));

        Assert.Equal(slug, plan.ClientSlug.Value);
        Assert.Equal(configuration.Site.Product, plan.ProductId.Value);
        Assert.Equal(configuration.Site.Template, plan.TemplateId.Value);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Generated, PlannedFileAction.Create, null)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Create, null)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Preserve, null)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Preserve, null)]
    [InlineData(FileGenerationStrategy.Merge, PlannedFileAction.Conflict, "SF-GEN-FILE-006")]
    public void InitialIntentRespectsEachStrategyWithoutAssumingExistingFileState(
        FileGenerationStrategy strategy, PlannedFileAction expectedAction, string? errorCode)
    {
        TemplateFile declaration = File("logical-file", "logical/file.txt", strategy);
        GenerationPlan plan = Build(Configuration(), Manifest([declaration]));

        PlannedFile file = Assert.Single(plan.Files);
        Assert.Equal(declaration.Id, file.Id);
        Assert.Equal(declaration.Strategy, file.Strategy);
        Assert.Equal(expectedAction, file.Action);
        Assert.Equal(errorCode is not null, plan.HasErrors);
        if (errorCode is null)
        {
            Assert.Empty(plan.Diagnostics);
        }
        else
        {
            GenerationPlanDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
            Assert.Equal(errorCode, diagnostic.Code.Value);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(file.RelativePath, diagnostic.PropertyPath);
            Assert.Equal("Le manifest minimal ne déclare aucun handler de fusion structurée.", diagnostic.Message);
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void EquivalentInputsAndReversedDeclarationsProduceTheSameOrderedPlanAndDiagnostics(string cultureName)
    {
        TemplateFile[] declarations =
        [
            File("team", "équipe.md", FileGenerationStrategy.Merge),
            File("alpha", "alpha.md", FileGenerationStrategy.Custom),
            File("zeta", "Zeta.md", FileGenerationStrategy.Merge),
            File("readme", "README.md"),
        ];
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            GenerationPlan first = Build(Configuration(), Manifest(declarations));
            GenerationPlan repeated = Build(Configuration(), Manifest(declarations));
            GenerationPlan reversed = Build(Configuration(), Manifest(declarations.Reverse()));

            AssertSamePlan(first, repeated);
            AssertSamePlan(first, reversed);
            Assert.Equal(["README.md", "Zeta.md", "alpha.md", "équipe.md"], first.Files.Select(static file => file.RelativePath));
            Assert.Equal(["Zeta.md", "équipe.md"], first.Diagnostics.Select(static diagnostic => diagnostic.PropertyPath));
            Assert.All(first.Diagnostics, static diagnostic => Assert.Equal("SF-GEN-FILE-006", diagnostic.Code.Value));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void FileIdentitySurvivesRenameSourceAndStrategyChangesAndAnUnrelatedInsertion()
    {
        GenerationPlan first = Build(Configuration(), Manifest([File("readme", "README.md")]));
        TemplateFile renamed = new(new("readme"), "docs/README.md", "other/source", FileGenerationStrategy.Protected);
        GenerationPlan revised = Build(Configuration(), Manifest([File("unrelated", "AAA.md"), renamed]));
        PlannedFile revisedFile = Assert.Single(revised.Files, static file => file.Id.Value == "readme");

        Assert.Equal(Assert.Single(first.Files).Id, revisedFile.Id);
        Assert.Equal("docs/README.md", revisedFile.RelativePath);
        Assert.Equal(PlannedFileAction.Preserve, revisedFile.Action);
        Assert.False(revised.HasErrors);
    }

    [Fact]
    public void CallerPlanIdentityIsOpaqueAndDoesNotInfluenceLogicalContents()
    {
        TemplateManifest manifest = Manifest([File("readme", "README.md")]);
        GenerationPlan first = Build(Configuration(), manifest);
        GenerationPlan second = GenerationPlanBuilder.Build(new("other-explicit-run"), Configuration(), manifest);

        Assert.NotEqual(first.Id, second.Id);
        AssertSameLogicalContents(first, second);
    }

    [Fact]
    public void ReusedLogicalSourcesDoNotCollapseDistinctFileIdentities()
    {
        GenerationPlan plan = Build(Configuration(), Manifest([File("first", "first.md"), File("second", "second.md")]));

        Assert.False(plan.HasErrors);
        Assert.Equal(["first", "second"], plan.Files.Select(static file => file.Id.Value));
    }

    [Fact]
    public void TemplateAndProductMismatchProduceSortedErrorsWithoutAFilePlan()
    {
        TemplateManifest unrelated = new(new("other-template"), new("0.2.0"), [new("other-product")], [File("readme", "README.md")]);
        GenerationPlan plan = Build(Configuration(), unrelated);

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        Assert.Equal("fixture-template", plan.TemplateId.Value);
        Assert.Equal("fixture-product", plan.ProductId.Value);
        Assert.Equal(["SF-TEMPLATE-002", "SF-GEN-002"], plan.Diagnostics.Select(static diagnostic => diagnostic.Code.Value));
        Assert.Equal(["site.product", "site.template"], plan.Diagnostics.Select(static diagnostic => diagnostic.PropertyPath));
        Assert.All(plan.Diagnostics, static diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
        Assert.Equal("Le manifest résolu ne correspond pas au template de la configuration.", plan.Diagnostics[1].Message);
    }

    [Theory]
    [InlineData("duplicate-ids")]
    [InlineData("duplicate-targets")]
    [InlineData("no-files")]
    [InlineData("duplicate-products")]
    [InlineData("no-products")]
    public void InvalidDeclarationsProduceDiagnosticsInsteadOfPartialOrDuplicatePlannedFiles(string defect)
    {
        TemplateFile first = File("readme", "README.md");
        TemplateFile[] declarations = defect switch
        {
            "duplicate-ids" => [first, File("readme", "other.md")],
            "duplicate-targets" => [first, File("other-id", "README.md")],
            "no-files" => [],
            _ => [first],
        };
        ProductId[] products = defect switch
        {
            "no-products" => [],
            "duplicate-products" => [new("fixture-product"), new("fixture-product")],
            _ => [new("fixture-product")],
        };
        TemplateManifest manifest = new(new("fixture-template"), new("0.2.0"), products, declarations);
        GenerationPlan plan = Build(Configuration(), manifest);
        GenerationPlan reversed = Build(Configuration(), new(manifest.TemplateId, manifest.TemplateVersion, products.Reverse(), declarations.Reverse()));

        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        Assert.Contains(TemplateDiagnostics.InvalidManifest, plan.Diagnostics);
        AssertSamePlan(plan, reversed);
    }

    [Fact]
    public void RequiredArgumentsCannotBeNull()
    {
        TemplateManifest manifest = Manifest([File("readme", "README.md")]);
        Assert.Throws<ArgumentNullException>("id", () => GenerationPlanBuilder.Build(null!, Configuration(), manifest));
        Assert.Throws<ArgumentNullException>("configuration", () => GenerationPlanBuilder.Build(new("test-plan"), null!, manifest));
        Assert.Throws<ArgumentNullException>("template", () => GenerationPlanBuilder.Build(new("test-plan"), Configuration(), null!));
    }

    private static void AssertSamePlan(GenerationPlan expected, GenerationPlan actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        AssertSameLogicalContents(expected, actual);
    }

    private static void AssertSameLogicalContents(GenerationPlan expected, GenerationPlan actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.ClientSlug, actual.ClientSlug);
        Assert.Equal(expected.ProductId, actual.ProductId);
        Assert.Equal(expected.TemplateId, actual.TemplateId);
        Assert.Equal(expected.Files, actual.Files);
        Assert.Equal(expected.Diagnostics, actual.Diagnostics);
        Assert.Equal(expected.HasErrors, actual.HasErrors);
    }

    private static GenerationPlan Build(ClientConfigurationV1 configuration, TemplateManifest manifest) =>
        GenerationPlanBuilder.Build(new("test-plan"), configuration, manifest);

    private static TemplateManifest Manifest(IEnumerable<TemplateFile> files) =>
        new(new("fixture-template"), new("0.2.0"), [new("fixture-product")], files);

    private static TemplateFile File(string id, string path, FileGenerationStrategy strategy = FileGenerationStrategy.CreateOnce) =>
        new(new(id), path, "logical/shared-source", strategy);

    private static ClientConfigurationV1 Configuration(string slug = "acme-services") =>
        new(1, new(slug, "Acme Services"), new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"),
            new([]), new("docker-compose-vps"), new(true, false));
}
