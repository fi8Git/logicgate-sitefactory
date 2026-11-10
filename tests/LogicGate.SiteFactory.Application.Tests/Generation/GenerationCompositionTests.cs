using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using LogicGate.SiteFactory.ModuleSdk;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationCompositionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CoreValidatesResolvesAndPlansWithOnlyAMinimalFakeModule()
    {
        FakeModule module = new("test-module", "test-template");
        GenerationComposition composition = Compose(module);
        ClientConfigurationV1 configuration = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        Assert.True(composition.IsValid);

        TemplateResolutionResult resolved = await new TemplateResolver(composition).ResolveAsync(configuration, Token);
        Assert.NotNull(resolved.Template);
        GenerationPlan plan = GenerationPlanBuilder.Build(new("fake-plan"), configuration, resolved.Template);

        Assert.False(plan.HasErrors);
        Assert.Equal(new TemplateId("test-template"), plan.TemplateId);
        Assert.Equal(new ProductId("test-product"), plan.ProductId);
        Assert.Equal(["a.txt", "z.txt"], plan.Files.Select(static file => file.RelativePath));
        Assert.All(plan.Files, static file => Assert.Equal(PlannedFileAction.Create, file.Action));
        Assert.Equal(1, module.CatalogReads);
        Assert.Equal(0, module.ContentReads); // Planning never renders or writes content.
        Assert.Equal("fake content", await composition.ReadAsync(resolved.Template, configuration, "test/content", Token));
        Assert.Equal(1, module.ContentReads);
    }

    [Fact]
    public async Task RemovingAllModulesLeavesAValidEmptyCatalogRatherThanAnImplicitDefault()
    {
        GenerationComposition composition = new(new ModuleCatalog([]), []);
        Assert.True(composition.IsValid);
        Assert.Empty((await composition.ReadAsync(Token)).Manifests);
        TemplateResolutionResult result = await new TemplateResolver(composition).ResolveAsync(Configuration(), Token);
        Assert.Null(result.Template);
        Assert.Equal(TemplateDiagnostics.UnknownTemplate, result.Diagnostic);
    }

    [Fact]
    public async Task UnknownModuleFailsBeforeReadingAnyContribution()
    {
        FakeModule module = new("test-module", "test-template");
        GenerationComposition composition = new(new ModuleCatalog([]), [module.Contribution()]);
        Assert.Equal("SF-MODULE-001", Assert.Single(composition.Diagnostics).Code.Value);
        await AssertRejectedWithoutReading(composition, module, "SF-MODULE-001");
    }

    [Fact]
    public async Task VersionMismatchAndInvalidCatalogCannotSupplyTemplateContent()
    {
        FakeModule module = new("test-module", "test-template");
        GenerationComposition mismatch = new(Catalog(module),
            [new(module.Descriptor.Id, new("2.0.0"), module.Manifest.TemplateId, module, module)]);
        await AssertRejectedWithoutReading(mismatch, module, "SF-MODULE-029");

        GenerationComposition invalid = new(new ModuleCatalog([new("test-module", "latest", module)]), [module.Contribution()]);
        await AssertRejectedWithoutReading(invalid, module, "SF-MODULE-003");
    }

    [Fact]
    public async Task TwoOwnersOfTheSameTemplateAreRejectedBeforeReadingEitherOne()
    {
        FakeModule alpha = new("alpha", "test-template");
        FakeModule beta = new("beta", "test-template");
        GenerationComposition first = new(Catalog(alpha, beta), [alpha.Contribution(), beta.Contribution()]);
        GenerationComposition reversed = new(Catalog(beta, alpha), [beta.Contribution(), alpha.Contribution()]);
        Assert.Equal(first.Diagnostics, reversed.Diagnostics);
        await AssertRejectedWithoutReading(first, alpha, "SF-TEMPLATE-004");
        await AssertRejectedWithoutReading(reversed, beta, "SF-TEMPLATE-004");
    }

    [Fact]
    public async Task RegistrationOrderDoesNotChangeCatalogOrderOrResolution()
    {
        FakeModule alpha = new("alpha", "alpha-template");
        FakeModule beta = new("beta", "beta-template");
        GenerationComposition first = new(Catalog(beta, alpha), [beta.Contribution(), alpha.Contribution()]);
        GenerationComposition second = new(Catalog(alpha, beta), [alpha.Contribution(), beta.Contribution()]);
        TemplateCatalogReadResult one = await first.ReadAsync(Token);
        TemplateCatalogReadResult two = await second.ReadAsync(Token);
        Assert.Equal([alpha.Manifest, beta.Manifest], one.Manifests);
        Assert.Equal(one.Manifests, two.Manifests);
        Assert.Same(alpha.Manifest, (await new TemplateResolver(first).ResolveAsync(Configuration("alpha-template"), Token)).Template);
        Assert.Same(alpha.Manifest, (await new TemplateResolver(second).ResolveAsync(Configuration("alpha-template"), Token)).Template);
    }

    [Fact]
    public async Task AContributionCannotClaimAnotherTemplatesIdentity()
    {
        FakeModule module = new("test-module", "actual-template");
        GenerationComposition composition = new(Catalog(module),
            [new(module.Descriptor.Id, module.Descriptor.Version, new("declared-template"), module, module)]);
        Assert.Equal(TemplateDiagnostics.InvalidManifest, (await composition.ReadAsync(Token)).Diagnostic);
        Assert.Equal(0, module.ContentReads);
    }

    [Fact]
    public async Task ContentDispatchNeverFallsBackToAnotherProvider()
    {
        FakeModule module = new("test-module", "test-template");
        GenerationComposition composition = Compose(module);
        TemplateManifest foreign = new(new("other-template"), new("1.0.0"), [new("test-product")], module.Manifest.Files);
        Assert.Null(await composition.ReadAsync(foreign, Configuration(), "test/content", Token));
        Assert.Equal(0, module.ContentReads);
        Assert.Null(await composition.ReadAsync(module.Manifest, Configuration(), "unknown/source", Token));
        TemplateManifest otherVersion = new(module.Manifest.TemplateId, new("2.0.0"), module.Manifest.SupportedProductIds, module.Manifest.Files);
        Assert.Null(await composition.ReadAsync(otherVersion, Configuration(), "test/content", Token));
    }

    [Fact]
    public async Task CancellationIsCheckedAndPropagatedWithoutCallingAnAlternativeProvider()
    {
        FakeModule module = new("test-module", "test-template");
        GenerationComposition composition = Compose(module);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => composition.ReadAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => composition.ReadAsync(module.Manifest, Configuration(), "test/content", cancelled.Token));
        Assert.Equal(0, module.CatalogReads);
        Assert.Equal(0, module.ContentReads);
        await composition.ReadAsync(Token);
        Assert.Equal(Token, module.LastToken);
        await composition.ReadAsync(module.Manifest, Configuration(), "test/content", Token);
        Assert.Equal(Token, module.LastToken);
    }

    [Fact]
    public async Task ContributionCollectionIsCapturedAndProviderFailuresAreNotHidden()
    {
        FakeModule module = new("test-module", "test-template");
        GenerationContribution[] input = [module.Contribution()];
        GenerationComposition composition = new(Catalog(module), input);
        input[0] = new FakeModule("replacement", "replacement-template").Contribution();
        Assert.Same(module.Manifest, Assert.Single((await composition.ReadAsync(Token)).Manifests));
        module.Failure = new InvalidOperationException("Test provider defect.");
        Assert.Same(module.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => composition.ReadAsync(Token)));
    }

    private static async Task AssertRejectedWithoutReading(GenerationComposition composition, FakeModule module, string code)
    {
        Assert.False(composition.IsValid);
        TemplateResolutionResult result = await new TemplateResolver(composition).ResolveAsync(Configuration(), Token);
        Assert.Null(result.Template);
        Assert.Equal(code, result.Diagnostic!.Code.Value);
        Assert.Null(await composition.ReadAsync(module.Manifest, Configuration(), "test/content", Token));
        Assert.Equal(0, module.CatalogReads);
        Assert.Equal(0, module.ContentReads);
    }

    private static ModuleCatalog Catalog(params FakeModule[] modules) => new(modules.Select(static module =>
        new ModuleRegistration(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)));

    private static GenerationComposition Compose(FakeModule module) => new(Catalog(module), [module.Contribution()]);

    private static ClientConfigurationV1 Configuration(string template = "test-template") => new(1,
        new("fictitious-client", "Fictitious Client"), new("test-product", template, "fr-FR", "https://example.test"),
        new([]), new("docker-compose-vps"), new(true, false));

    private sealed class FakeModule : ISiteFactoryModule, ITemplateCatalog, ITemplateContentProvider
    {
        public FakeModule(string id, string templateId)
        {
            Descriptor = new(new(id), new("1.0.0"), []);
            Manifest = new(new(templateId), new("1.0.0"), [new("test-product")],
                [new(new("z-file"), "z.txt", "test/content", FileGenerationStrategy.Generated),
                 new(new("a-file"), "a.txt", "test/content", FileGenerationStrategy.CreateOnce)]);
        }

        public ModuleDescriptor Descriptor { get; }
        public TemplateManifest Manifest { get; }
        public int CatalogReads { get; private set; }
        public int ContentReads { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Exception? Failure { get; set; }

        public GenerationContribution Contribution() => new(Descriptor.Id, Descriptor.Version, Manifest.TemplateId, this, this);

        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CatalogReads++;
            LastToken = cancellationToken;
            if (Failure is not null) throw Failure;
            return Task.FromResult(TemplateCatalogReadResult.Success([Manifest]));
        }

        public Task<string?> ReadAsync(TemplateManifest manifest, ClientConfigurationV1 configuration,
            string source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContentReads++;
            LastToken = cancellationToken;
            return Task.FromResult(manifest.TemplateVersion == Manifest.TemplateVersion && source == "test/content" ? "fake content" : null);
        }
    }
}
