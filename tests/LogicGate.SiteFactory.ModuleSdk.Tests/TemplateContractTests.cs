using System.Runtime.InteropServices;
using Xunit;

namespace LogicGate.SiteFactory.ModuleSdk.Tests;

public sealed class TemplateContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("logical\nvalue")]
    [InlineData("logical\0value")]
    public void RequiredTextRejectsMissingOrControlValues(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new TemplateIdentity(value!, "1.0.0"));
        Assert.ThrowsAny<ArgumentException>(() => new TemplateFileDefinition(value!, "a.txt", "content/a", TemplateFilePolicy.Generated));
        Assert.ThrowsAny<ArgumentException>(() => new TemplateFileDefinition("a", value!, "content/a", TemplateFilePolicy.Generated));
        Assert.ThrowsAny<ArgumentException>(() => new TemplateFileDefinition("a", "a.txt", value!, TemplateFilePolicy.Generated));
        Assert.ThrowsAny<ArgumentException>(() => new TemplateRenderContext(value!, "fr-FR"));
        Assert.ThrowsAny<ArgumentException>(() => new TemplateRenderContext("Example", value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-preview.1")]
    [InlineData("1.2.3+build")]
    [InlineData("[1.2.3]")]
    [InlineData("1.2.*")]
    [InlineData("latest")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3\n")]
    public void TemplateVersionIsExactAndStable(string? value) =>
        Assert.ThrowsAny<ArgumentException>(() => new TemplateIdentity("docs", value!));

    [Fact]
    public void IdentityKeepsIdAndVersionSeparateAndUsesOrdinalEquality()
    {
        TemplateIdentity first = new("docs", "1.2.3");
        Assert.Equal("docs", first.Id);
        Assert.Equal("1.2.3", first.Version);
        Assert.Equal(first, new("docs", "1.2.3"));
        Assert.NotEqual(first, new("docs", "1.2.4"));
        Assert.NotEqual(first, new("Docs", "1.2.3"));
        Assert.NotEqual(first, new("other", "1.2.3"));
        Assert.Equal("0.0.0", new TemplateIdentity("docs", "0.0.0").Version);
        Assert.Equal("1.2.3-preview.1", new ModuleVersion("1.2.3-preview.1").Value);
    }

    [Fact]
    public void FileIdentityIsIndependentOfSourceAndPathAndSdkDoesNotAuthorizePaths()
    {
        TemplateFileDefinition first = File();
        TemplateFileDefinition moved = new(first.FileId, "new/path.bin", "new-resource", first.Policy);
        Assert.Equal(first.FileId, moved.FileId);
        Assert.NotEqual(first.RelativePath, moved.RelativePath);
        Assert.NotEqual(first.ContentKey, moved.ContentKey);
        // These are declarations, not validated executable paths. The future adapter/engine must reject unsafe paths.
        Assert.Equal("../outside", new TemplateFileDefinition("a", "../outside", "logical-key", TemplateFilePolicy.Generated).RelativePath);
        TemplateDefinition duplicates = new(new("docs", "1.0.0"), ["documentation", "documentation"], [first, first]);
        Assert.Equal(2, duplicates.Files.Count);
        Assert.Equal(2, duplicates.SupportedProductIds.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void UnsupportedPoliciesAreRejected(int policy) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemplateFileDefinition("a", "a.bin", "key", (TemplateFilePolicy)policy));

    [Fact]
    public void DefinitionsRejectIncompleteOrNullCollectionsAndEntries()
    {
        TemplateIdentity identity = new("docs", "1.0.0");
        Assert.Throws<ArgumentNullException>(() => new TemplateDefinition(null!, ["documentation"], [File()]));
        Assert.Throws<ArgumentNullException>(() => new TemplateDefinition(identity, null!, [File()]));
        Assert.Throws<ArgumentNullException>(() => new TemplateDefinition(identity, ["documentation"], null!));
        Assert.Throws<ArgumentException>(() => new TemplateDefinition(identity, [], [File()]));
        Assert.Throws<ArgumentException>(() => new TemplateDefinition(identity, ["documentation"], []));
        Assert.Throws<ArgumentException>(() => new TemplateDefinition(identity, [null!], [File()]));
        Assert.Throws<ArgumentException>(() => new TemplateDefinition(identity, [" "], [File()]));
        Assert.Throws<ArgumentException>(() => new TemplateDefinition(identity, ["doc\0s"], [File()]));
        Assert.Throws<ArgumentException>(() => new TemplateDefinition(identity, ["documentation"], [null!]));
        Assert.Throws<ArgumentNullException>(() => TemplateCatalogResult.Success(null!));
        Assert.Throws<ArgumentException>(() => TemplateCatalogResult.Success([null!]));
        Assert.Throws<ArgumentNullException>(() => TemplateContentResult.Success(null!));
    }

    [Fact]
    public void DeclarationSnapshotsCannotBeMutatedAndCatalogRetainsAllAmbiguities()
    {
        string[] products = ["documentation"];
        TemplateFileDefinition[] files = [File()];
        TemplateDefinition definition = new(new("docs", "1.0.0"), products, files);
        TemplateDefinition anotherVersion = new(new("docs", "2.0.0"), products, files);
        TemplateDefinition[] templates = [definition, anotherVersion, definition];
        TemplateCatalogResult catalog = TemplateCatalogResult.Success(templates);
        products[0] = "changed";
        files[0] = new("replacement", "other.bin", "other-key", TemplateFilePolicy.CreateOnce);
        templates[0] = anotherVersion;
        Assert.Equal(["documentation"], definition.SupportedProductIds);
        Assert.Equal("stable-file", Assert.Single(definition.Files).FileId);
        Assert.Equal([definition, anotherVersion, definition], catalog.Templates);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)definition.SupportedProductIds)[0] = "mutated");
        Assert.Throws<NotSupportedException>(() => ((IList<TemplateFileDefinition>)definition.Files).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TemplateDefinition>)catalog.Templates).Add(definition));
        Assert.True(catalog.IsSuccess);
        Assert.Null(catalog.Error);
        Assert.True(TemplateCatalogResult.Success([]).IsSuccess);
    }

    [Fact]
    public void ContentCopiesInputAndDoesNotExposeItsOwnBackingArray()
    {
        byte[] input = [0, 255, 128, 13, 10]; // Binary bytes are preserved; the SDK does not normalize encoding or newlines.
        TemplateContentResult result = TemplateContentResult.Success(input);
        input[0] = 42;
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(new byte[] { 0, 255, 128, 13, 10 }, result.Content.ToArray());
        Assert.True(MemoryMarshal.TryGetArray(result.Content, out ArraySegment<byte> exposed));
        exposed.Array![exposed.Offset] = 99;
        Assert.Equal((byte)0, result.Content.Span[0]);
        TemplateContentResult empty = TemplateContentResult.Success([]);
        Assert.True(empty.IsSuccess);
        Assert.True(empty.Content.IsEmpty);
        Assert.Null(empty.Error);
    }

    [Theory]
    [InlineData(TemplateReadFailure.InvalidDefinition)]
    [InlineData(TemplateReadFailure.UnsupportedTemplate)]
    [InlineData(TemplateReadFailure.UnknownContent)]
    [InlineData(TemplateReadFailure.ContentUnavailable)]
    public void FailuresCarryOnlyAClosedCodeAndNoPartialResult(TemplateReadFailure failure)
    {
        TemplateCatalogResult catalog = TemplateCatalogResult.Failure(failure);
        TemplateContentResult content = TemplateContentResult.Failure(failure);
        Assert.False(catalog.IsSuccess);
        Assert.False(content.IsSuccess);
        Assert.Equal(failure, catalog.Error);
        Assert.Equal(failure, content.Error);
        Assert.Empty(catalog.Templates);
        Assert.True(content.Content.IsEmpty);
        Assert.Equal(["Error", "IsSuccess", "Templates"], typeof(TemplateCatalogResult).GetProperties().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Content", "Error", "IsSuccess"], typeof(TemplateContentResult).GetProperties().Select(static property => property.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5)]
    public void UnknownFailureCodesCannotEnterResults(int code)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TemplateCatalogResult.Failure((TemplateReadFailure)code));
        Assert.Throws<ArgumentOutOfRangeException>(() => TemplateContentResult.Failure((TemplateReadFailure)code));
    }

    [Fact]
    public void RenderContextRetainsItsTwoArgumentConstructorAndExposesOnlyReadOnlyInputs()
    {
        TemplateRenderContext context = new("Example & Co", "fr-FR");
        Assert.Equal("Example & Co", context.DisplayName);
        Assert.Equal("fr-FR", context.DefaultLocale);
        Assert.All(typeof(TemplateRenderContext).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.NotNull(typeof(TemplateRenderContext).GetConstructor([typeof(string), typeof(string)]));
    }

    private static TemplateFileDefinition File() => new("stable-file", "index.bin", "resources/home", TemplateFilePolicy.Generated);
}
