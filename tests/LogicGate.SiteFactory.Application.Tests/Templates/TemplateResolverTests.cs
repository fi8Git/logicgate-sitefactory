using System.Globalization;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Templates;

public sealed class TemplateResolverTests
{
    [Fact]
    public async Task ValidConfigurationResolvesTheExactDeclaredStableVersion()
    {
        TemplateManifest manifest = Manifest();
        ClientConfigurationV1 configuration = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success([manifest])));

        TemplateResolutionResult result = await resolver.ResolveAsync(configuration, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Diagnostic);
        Assert.NotNull(result.Template);
        Assert.Same(manifest, result.Template);
        Assert.Equal(new TemplateVersion("0.1.0"), result.Template.TemplateVersion);
    }

    [Theory]
    [InlineData("missing-template")]
    [InlineData("FIXTURE-TEMPLATE")]
    [InlineData(" fixture-template ")]
    public async Task MissingOrNonExactTemplateIdentifiersReturnAStableDiagnostic(string template)
    {
        TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success([Manifest()])));

        AssertFailure(await resolver.ResolveAsync(Configuration(template: template), TestContext.Current.CancellationToken),
            "SF-TEMPLATE-001", "site.template", "Le template demandé est absent du catalogue.");
    }

    [Fact]
    public async Task EmptyCatalogReturnsUnknownTemplate()
    {
        TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success([])));

        Assert.Equal(TemplateDiagnostics.UnknownTemplate,
            (await resolver.ResolveAsync(Configuration(), TestContext.Current.CancellationToken)).Diagnostic);
    }

    [Theory]
    [InlineData("site-pro")]
    [InlineData("FIXTURE-PRODUCT")]
    [InlineData("fixture-product ")]
    public async Task IncompatibleProductReturnsAStableDiagnostic(string product)
    {
        TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success([Manifest()])));

        AssertFailure(await resolver.ResolveAsync(Configuration(product: product), TestContext.Current.CancellationToken),
            "SF-TEMPLATE-002", "site.product", "Le template ne prend pas en charge le produit demandé.");
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0")]
    [InlineData("10.0.0")]
    public async Task MultipleDeclarationsNeverSelectFirstOrLatestEvenIfOnlyOneProductMatches(string otherVersion)
    {
        TemplateManifest first = Manifest();
        TemplateManifest second = Manifest(version: otherVersion, product: "other-product");

        foreach (TemplateManifest[] order in new[] { new[] { first, second }, new[] { second, first }, new[] { first, first } })
        {
            TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success(order)));
            AssertFailure(await resolver.ResolveAsync(Configuration(), TestContext.Current.CancellationToken),
                "SF-TEMPLATE-004", "site.template", "Plusieurs manifests correspondent au template demandé.");
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public async Task CatalogDiscoveryOrderAndCultureDoNotChangeResolution(string cultureName)
    {
        TemplateManifest expected = Manifest();
        TemplateManifest[] manifests = [Manifest("zeta-template"), expected, Manifest("alpha-template")];
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            TemplateResolver forward = new(new StubCatalog(TemplateCatalogReadResult.Success(manifests)));
            TemplateResolver backward = new(new StubCatalog(TemplateCatalogReadResult.Success(manifests.Reverse())));

            TemplateResolutionResult first = await forward.ResolveAsync(Configuration(), TestContext.Current.CancellationToken);
            TemplateResolutionResult second = await backward.ResolveAsync(Configuration(), TestContext.Current.CancellationToken);
            TemplateResolutionResult repeated = await forward.ResolveAsync(Configuration(), TestContext.Current.CancellationToken);

            Assert.Same(expected, first.Template);
            Assert.Same(first.Template, second.Template);
            Assert.Same(first.Template, repeated.Template);
            Assert.Null(first.Diagnostic);
            Assert.Null(second.Diagnostic);
            Assert.Null(repeated.Diagnostic);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("no-products")]
    [InlineData("no-files")]
    [InlineData("duplicate-products")]
    [InlineData("duplicate-targets")]
    [InlineData("duplicate-ids")]
    public async Task InvalidManifestDeclarationsBlockResolutionRegardlessOfCatalogOrder(string defect)
    {
        TemplateFile file = new(new("readme"), "README.md", "structure/readme", FileGenerationStrategy.CreateOnce);
        ProductId product = new("fixture-product");
        TemplateManifest invalid = new(new("unrelated-template"), new("0.1.0"),
            defect == "no-products" ? [] : defect == "duplicate-products" ? [product, product] : [product],
            defect == "no-files" ? [] : defect == "duplicate-targets"
                ? [file, new(new("different-id"), file.RelativePath, "other-source", FileGenerationStrategy.Generated)]
                : defect == "duplicate-ids"
                    ? [file, new(new("readme"), "other.md", "other-source", FileGenerationStrategy.Generated)] : [file]);

        foreach (TemplateManifest[] order in new[] { new[] { invalid, Manifest() }, new[] { Manifest(), invalid } })
        {
            TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success(order)));
            AssertFailure(await resolver.ResolveAsync(Configuration(), TestContext.Current.CancellationToken),
                "SF-TEMPLATE-003", "templates", "Le catalogue contient un manifest de template invalide.");
        }
    }

    [Fact]
    public async Task CatalogFailureIsPropagatedWithoutReturningAPartialTemplate()
    {
        TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest)));
        TemplateResolutionResult result = await resolver.ResolveAsync(Configuration(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Template);
        Assert.Same(TemplateDiagnostics.InvalidManifest, result.Diagnostic);
    }

    [Fact]
    public async Task CancellationIsCheckedBeforeAndAfterTheCatalogAndUsesTheCallerToken()
    {
        using CancellationTokenSource cancellation = new();
        bool invoked = false;
        DelegateCatalog catalog = new(token =>
        {
            invoked = true;
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult(TemplateCatalogReadResult.Success([Manifest()]));
        });
        TemplateResolver resolver = new(catalog);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(Configuration(), cancellation.Token));
        Assert.True(invoked);
        invoked = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(Configuration(), cancellation.Token));
        Assert.False(invoked);
    }

    [Fact]
    public async Task UnexpectedCatalogFailuresAreNotConvertedIntoUserDiagnostics()
    {
        InvalidOperationException failure = new("Test-only unexpected catalog failure.");
        TemplateResolver resolver = new(new DelegateCatalog(_ => Task.FromException<TemplateCatalogReadResult>(failure)));

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveAsync(Configuration(), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task NullRequiredArgumentsRemainProgrammingErrors()
    {
        Assert.Throws<ArgumentNullException>(() => new TemplateResolver(null!));
        TemplateResolver resolver = new(new StubCatalog(TemplateCatalogReadResult.Success([])));
        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveAsync(null!, TestContext.Current.CancellationToken));
    }

    private static void AssertFailure(TemplateResolutionResult result, string code, string path, string message)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Template);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(code, result.Diagnostic.Code.Value);
        Assert.Equal(DiagnosticSeverity.Error, result.Diagnostic.Severity);
        Assert.Equal(path, result.Diagnostic.PropertyPath);
        Assert.Equal(message, result.Diagnostic.Message);
    }

    private static TemplateManifest Manifest(string template = "fixture-template", string version = "0.1.0", string product = "fixture-product") =>
        new(new(template), new(version), [new(product)], [new(new("readme"), "README.md", "structure/readme", FileGenerationStrategy.CreateOnce)]);

    private static ClientConfigurationV1 Configuration(string template = "fixture-template", string product = "fixture-product") =>
        new(1, new("acme-services", "Acme Services"), new(product, template, "fr-FR", "https://www.example.test"),
            new([]), new("docker-compose-vps"), new(true, false));

    private sealed class StubCatalog(TemplateCatalogReadResult result) : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class DelegateCatalog(Func<CancellationToken, Task<TemplateCatalogReadResult>> read) : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default) => read(cancellationToken);
    }
}
