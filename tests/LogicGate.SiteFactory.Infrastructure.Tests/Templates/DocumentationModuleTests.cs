using System.Globalization;
using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using LogicGate.SiteFactory.Tests.Fixtures;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Templates;

public sealed class DocumentationModuleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void RegistrationDescribesOnlyTheImplementedCapabilityWithoutReadingContent()
    {
        DocumentationModule module = new(new NeverReadManifest());
        ModuleCatalog catalog = Catalog(module);
        Assert.True(catalog.IsValid);
        Assert.Equal("documentation", Assert.Single(catalog.Modules).Id.Value);
        Assert.Equal("0.2.0", module.Descriptor.Version.Value);
        Assert.Equal("documentation.generation", Assert.Single(catalog.Capabilities).Value);
        Assert.Same(module.Descriptor, catalog.Resolve(new("documentation")).Module);
        Assert.Equal("SF-MODULE-001", Assert.Single(catalog.Resolve(new("unknown")).Diagnostics).Code.Value);
        Assert.Equal("SF-MODULE-005", Assert.Single(new ModuleCatalog([
            new("documentation", "0.2.0", module), new("documentation", "0.2.0", module)]).Diagnostics).Code.Value);
        GenerationContribution contribution = module.CreateContribution();
        Assert.Equal(module.Descriptor.Id, contribution.ModuleId);
        Assert.Equal(module.Descriptor.Version, contribution.ModuleVersion);
        Assert.Equal(new TemplateId("documentation-minimal"), contribution.TemplateId);
    }

    [Fact]
    public void ModuleAssemblyHasNoReferenceToAnotherModuleOrInfrastructure()
    {
        var assembly = typeof(DocumentationModule).Assembly;
        Assert.Equal("LogicGate.SiteFactory.Modules.Documentation", assembly.GetName().Name);
        Assert.Equal(["LogicGate.SiteFactory.Application", "LogicGate.SiteFactory.Domain", "LogicGate.SiteFactory.ModuleSdk"],
            assembly.GetReferencedAssemblies().Select(static reference => reference.Name!)
                .Where(static name => name.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Equal(["LogicGate.SiteFactory.Templates.Documentation.Guide", "LogicGate.SiteFactory.Templates.Documentation.Index",
            "LogicGate.SiteFactory.Templates.Documentation.Manifest"], assembly.GetManifestResourceNames().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RealTemplateResolvesAndProducesAMinimalDeterministicPlanWithoutPme()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationComposition composition = new(Catalog(module), [module.CreateContribution()]);
        ClientConfigurationV1 config = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(config).IsValid);
        TemplateResolutionResult resolved = await new TemplateResolver(composition).ResolveAsync(config, Token);
        Assert.True(resolved.IsSuccess);
        Assert.NotNull(resolved.Template);
        Assert.Equal("0.1.0", resolved.Template.TemplateVersion.Value);
        Assert.Equal(new ProductId("documentation"), Assert.Single(resolved.Template.SupportedProductIds));

        GenerationPlan first = GenerationPlanBuilder.Build(new("documentation-plan"), config, resolved.Template);
        GenerationPlan second = GenerationPlanBuilder.Build(new("documentation-plan"), config, resolved.Template);
        Assert.False(first.HasErrors);
        Assert.Empty(first.Diagnostics);
        Assert.Equal(["guide.html", "index.html"], first.Files.Select(static file => file.RelativePath));
        Assert.Equal(["documentation-guide", "documentation-index"], first.Files.Select(static file => file.Id.Value));
        Assert.All(first.Files, static file =>
        {
            Assert.Equal(FileGenerationStrategy.Generated, file.Strategy);
            Assert.Equal(PlannedFileAction.Create, file.Action);
        });
        Assert.Equal(GenerationPlanJsonSerializer.Serialize(first), GenerationPlanJsonSerializer.Serialize(second));
    }

    [Fact]
    public async Task TwoIndependentModulesUseTheSameCatalogResolverAndPlannerInEitherOrder()
    {
        DocumentationModule docs = new(new TemplateManifestJsonReader());
        PublicGenerationFixture fixture = new();
        ModuleCatalog catalog = new([new("fixture-module", "1.0.0", fixture), new("documentation", "0.2.0", docs)]);
        Assert.Equal(["documentation", "fixture-module"], catalog.Modules.Select(static module => module.Id.Value));
        GenerationComposition first = new(catalog, [fixture.CreateContribution(), docs.CreateContribution()]);
        GenerationComposition second = new(catalog, [docs.CreateContribution(), fixture.CreateContribution()]);
        foreach ((string product, string template, int files) in new[]
                 { ("documentation", "documentation-minimal", 2), ("fixture-product", "fixture-template", 7) })
        {
            ClientConfigurationV1 config = Configuration(template, product);
            TemplateResolutionResult left = await new TemplateResolver(first).ResolveAsync(config, Token);
            TemplateResolutionResult right = await new TemplateResolver(second).ResolveAsync(config, Token);
            Assert.NotNull(left.Template);
            Assert.NotNull(right.Template);
            GenerationPlan leftPlan = GenerationPlanBuilder.Build(new("coexistence"), config, left.Template);
            GenerationPlan rightPlan = GenerationPlanBuilder.Build(new("coexistence"), config, right.Template);
            Assert.False(leftPlan.HasErrors);
            Assert.Equal(files, leftPlan.Files.Count);
            Assert.Equal(GenerationPlanJsonSerializer.Serialize(leftPlan), GenerationPlanJsonSerializer.Serialize(rightPlan));
        }
    }

    [Theory]
    [InlineData("unknown", "documentation", "SF-TEMPLATE-001")]
    [InlineData("documentation-minimal", "unknown", "SF-TEMPLATE-002")]
    public async Task InvalidSelectionsHaveStableDiagnostics(string template, string product, string code)
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        TemplateResolutionResult result = await new TemplateResolver(module).ResolveAsync(Configuration(template, product), Token);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Template);
        Assert.Equal(code, result.Diagnostic!.Code.Value);
    }

    [Fact]
    public async Task TitleNavigationMarkdownAndPagesAreRenderedWithoutActiveUserContent()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        TemplateManifest manifest = Assert.Single((await module.ReadAsync(Token)).Manifests);
        ClientConfigurationV1 config = new(1, new("test-docs", "<script>alert('test')</script> & Documents"),
            new("documentation", "documentation-minimal", "fr-FR\" onload=\"alert(1)", "https://example.test"),
            new([]), new("docker-compose-vps"), new(true, false));
        foreach (TemplateFile file in manifest.Files)
        {
            string html = (await module.ReadAsync(manifest, config, file.Source, Token))!;
            Assert.StartsWith("<!DOCTYPE html>\n<html ", html, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("\" onload=\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"index.html\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"guide.html\"", html, StringComparison.Ordinal);
            Assert.Contains("<main>\n<h1>", html, StringComparison.Ordinal);
            Assert.Contains("<h2>", html, StringComparison.Ordinal);
            Assert.Contains("<p>", html, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', html);
            if (file.Source == "pages/guide")
            {
                Assert.Contains("&lt;em&gt;", html, StringComparison.Ordinal);
                Assert.DoesNotContain("<em>", html, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task RenderingIsByteIdenticalAcrossCallsAndCultures()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        TemplateManifest manifest = Assert.Single((await module.ReadAsync(Token)).Manifests);
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (TemplateFile file in manifest.Files)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                string first = (await module.ReadAsync(manifest, Configuration(), file.Source, Token))!;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                string second = (await module.ReadAsync(manifest, Configuration(), file.Source, Token))!;
                Assert.Equal(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public async Task NoFallbackForUnknownSourcesTemplatesOrVersions()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        TemplateManifest manifest = Assert.Single((await module.ReadAsync(Token)).Manifests);
        Assert.Null(await module.ReadAsync(manifest, Configuration(), "../outside.md", Token));
        TemplateManifest foreign = new(new("foreign"), manifest.TemplateVersion, manifest.SupportedProductIds, manifest.Files);
        Assert.Null(await module.ReadAsync(foreign, Configuration(), "pages/index", Token));
        TemplateManifest version = new(manifest.TemplateId, new("2.0.0"), manifest.SupportedProductIds, manifest.Files);
        Assert.Null(await module.ReadAsync(version, Configuration(), "pages/index", Token));
    }

    [Fact]
    public async Task CancellationIsPropagatedAndMissingReaderIsNotReplaced()
    {
        Assert.Throws<ArgumentNullException>(() => new DocumentationModule(null!));
        DocumentationModule module = new(new TemplateManifestJsonReader());
        TemplateManifest manifest = Assert.Single((await module.ReadAsync(Token)).Manifests);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => module.ReadAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => module.ReadAsync(manifest, Configuration(), "pages/index", cancellation.Token));
    }

    private static ModuleCatalog Catalog(DocumentationModule module) => new([new("documentation", "0.2.0", module)]);

    private static ClientConfigurationV1 Configuration(string template = "documentation-minimal", string product = "documentation") =>
        new(1, new("test-docs", "Documentation fictive"), new(product, template, "fr-FR", "https://example.test"),
            new([]), new("docker-compose-vps"), new(true, false));

    private sealed class NeverReadManifest : ITemplateManifestReader
    {
        public TemplateCatalogReadResult Read(string document) => throw new InvalidOperationException("Descriptor/registration must not parse resources.");
    }
}
