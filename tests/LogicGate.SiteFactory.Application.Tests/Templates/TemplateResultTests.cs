using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Templates;

public sealed class TemplateResultTests
{
    [Fact]
    public void CatalogSnapshotIsReadOnlyAndRetainsDuplicatesForAmbiguityDetection()
    {
        TemplateManifest manifest = new(new("test-template"), new("0.1.0"), [], []);
        List<TemplateManifest> input = [manifest, manifest];
        TemplateCatalogReadResult result = TemplateCatalogReadResult.Success(input);
        input.Clear();
        IList<TemplateManifest> exposed = Assert.IsAssignableFrom<IList<TemplateManifest>>(result.Manifests);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Diagnostic);
        Assert.Equal([manifest, manifest], result.Manifests);
        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
        Assert.Throws<NotSupportedException>(() => exposed[0] = manifest);
    }

    [Fact]
    public void FailureResultsContainOnlyTheBlockingDiagnostic()
    {
        TemplateCatalogReadResult catalog = TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest);
        TemplateResolutionResult resolution = TemplateResolutionResult.Failure(TemplateDiagnostics.UnknownTemplate);

        Assert.False(catalog.IsSuccess);
        Assert.Empty(catalog.Manifests);
        Assert.Same(TemplateDiagnostics.InvalidManifest, catalog.Diagnostic);
        Assert.False(resolution.IsSuccess);
        Assert.Null(resolution.Template);
        Assert.Same(TemplateDiagnostics.UnknownTemplate, resolution.Diagnostic);
    }

    [Fact]
    public void ResultFactoriesRejectNullsAndNullCatalogEntries()
    {
        Assert.Throws<ArgumentNullException>(() => TemplateCatalogReadResult.Success(null!));
        Assert.Throws<ArgumentException>(() => TemplateCatalogReadResult.Success([null!]));
        Assert.Throws<ArgumentNullException>(() => TemplateCatalogReadResult.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => TemplateResolutionResult.Success(null!));
        Assert.Throws<ArgumentNullException>(() => TemplateResolutionResult.Failure(null!));
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Information)]
    [InlineData(DiagnosticSeverity.Warning)]
    public void FailureRequiresAnErrorDiagnostic(DiagnosticSeverity severity)
    {
        GenerationPlanDiagnostic diagnostic = new(new("TEST-TEMPLATE-001"), severity, "Test message.");

        Assert.Throws<ArgumentException>(() => TemplateCatalogReadResult.Failure(diagnostic));
        Assert.Throws<ArgumentException>(() => TemplateResolutionResult.Failure(diagnostic));
    }
}
