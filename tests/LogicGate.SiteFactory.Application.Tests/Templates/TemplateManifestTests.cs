using System.Globalization;
using System.Reflection;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Templates;

public sealed class TemplateManifestTests
{
    [Fact]
    public void ManifestSnapshotsItsProductsAndFilesInReadOnlyCollections()
    {
        ProductId product = new("fixture-product");
        TemplateFile file = new(new("readme"), "README.md", "structure/readme", FileGenerationStrategy.CreateOnce);
        List<ProductId> products = [product];
        List<TemplateFile> files = [file];
        TemplateManifest manifest = CreateManifest(products, files);

        products.Clear();
        files.Clear();
        IList<ProductId> exposedProducts = Assert.IsAssignableFrom<IList<ProductId>>(manifest.SupportedProductIds);
        IList<TemplateFile> exposedFiles = Assert.IsAssignableFrom<IList<TemplateFile>>(manifest.Files);

        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal(new TemplateId("test-template"), manifest.TemplateId);
        Assert.Equal(new TemplateVersion("0.1.0"), manifest.TemplateVersion);
        Assert.True(exposedProducts.IsReadOnly);
        Assert.True(exposedFiles.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposedProducts.Clear());
        Assert.Throws<NotSupportedException>(() => exposedFiles.Clear());
        Assert.Throws<NotSupportedException>(() => exposedProducts[0] = new("other-product"));
        Assert.Throws<NotSupportedException>(() => exposedFiles[0] = new(new("other-id"), "other.md", "other-source", FileGenerationStrategy.Generated));
        Assert.Same(product, Assert.Single(manifest.SupportedProductIds));
        Assert.Same(file, Assert.Single(manifest.Files));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void ProductsAndFilesHaveACompleteOrdinalOrderIndependentOfInputAndCulture(string cultureName)
    {
        ProductId[] products = [new("Zeta"), new("alpha"), new("équipe")];
        TemplateFile[] files =
        [
            new(new("zeta"), "Zeta.md", "zeta-source", FileGenerationStrategy.Generated),
            new(new("upper-source"), "alpha.md", "Zeta", FileGenerationStrategy.Generated),
            new(new("ID"), "alpha.md", "alpha", FileGenerationStrategy.Generated),
            new(new("id"), "alpha.md", "alpha", FileGenerationStrategy.Generated),
            new(new("unicode-source"), "alpha.md", "équipe", FileGenerationStrategy.Generated),
            new(new("create-once"), "alpha.md", "alpha", FileGenerationStrategy.CreateOnce),
            new(new("team"), "équipe.md", "team", FileGenerationStrategy.Protected),
        ];
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            TemplateManifest reversed = CreateManifest(products.Reverse(), files.Reverse());
            TemplateManifest rotated = CreateManifest(products, files.Skip(2).Concat(files.Take(2)));

            Assert.Equal(products, reversed.SupportedProductIds);
            Assert.Equal(files, reversed.Files);
            Assert.Equal(rotated.SupportedProductIds, reversed.SupportedProductIds);
            Assert.Equal(rotated.Files, reversed.Files);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void ContractPreservesDuplicatesWithoutCatalogueResolutionOrCollisionDetection()
    {
        ProductId product = new("future-product");
        TemplateFile file = new(new("readme"), "README.md", "future/source", FileGenerationStrategy.Generated);
        TemplateManifest manifest = CreateManifest([product, product], [file, file]);

        Assert.Equal([product, product], manifest.SupportedProductIds);
        Assert.Equal([file, file], manifest.Files);
    }

    [Theory]
    [InlineData("templateId")]
    [InlineData("templateVersion")]
    [InlineData("supportedProductIds")]
    [InlineData("files")]
    public void RequiredArgumentsCannotBeNull(string parameter)
    {
        Assert.Throws<ArgumentNullException>(parameter, () => new TemplateManifest(
            parameter == "templateId" ? null! : new("test-template"),
            parameter == "templateVersion" ? null! : new("0.1.0"),
            parameter == "supportedProductIds" ? null! : [],
            parameter == "files" ? null! : []));
    }

    [Fact]
    public void NullCollectionEntriesAreRejected()
    {
        Assert.Throws<ArgumentException>("supportedProductIds", () => CreateManifest([null!], []));
        Assert.Throws<ArgumentException>("files", () => CreateManifest([], [null!]));
    }

    [Fact]
    public void ContractsAreImmutableAndApplicationReferencesOnlyDomainModuleSdkAndRuntimeTypes()
    {
        Type[] contracts = [typeof(TemplateManifest), typeof(TemplateFile), typeof(TemplateVersion)];

        foreach (Type contract in contracts)
        {
            Assert.True(contract.IsSealed);
            Assert.All(contract.GetProperties(), static property => Assert.Null(property.SetMethod));
            Assert.Empty(contract.GetFields(BindingFlags.Instance | BindingFlags.Public));
        }

        Assert.Equal(typeof(PlannedFile).Assembly, typeof(TemplateManifest).Assembly);
        Assert.Equal(typeof(PlannedFile).Assembly, typeof(TemplateFile).Assembly);
        Assert.Equal(typeof(TemplateId).Assembly, typeof(TemplateVersion).Assembly);
        Assert.All(typeof(TemplateManifest).Assembly.GetReferencedAssemblies(), static reference =>
            Assert.True(reference.Name == "LogicGate.SiteFactory.Domain"
                || reference.Name == "LogicGate.SiteFactory.ModuleSdk"
                || reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
                $"Unexpected contract dependency: {reference.Name}"));
    }

    private static TemplateManifest CreateManifest(IEnumerable<ProductId> products, IEnumerable<TemplateFile> files)
    {
        return new TemplateManifest(new("test-template"), new("0.1.0"), products, files);
    }
}
