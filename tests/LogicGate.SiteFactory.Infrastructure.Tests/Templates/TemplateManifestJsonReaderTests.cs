using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Templates;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Templates;

public sealed class TemplateManifestJsonReaderTests
{
    private const string MinimalJson = """
        {
          "schemaVersion": 2,
          "templateId": "documentation-minimal",
          "templateVersion": "0.1.0",
          "supportedProductIds": ["documentation"],
          "files": [{"id": "template-readme", "relativePath": "README.md", "source": "structure/readme", "strategy": "create-once"}]
        }
        """;

    [Theory]
    [InlineData("generated", FileGenerationStrategy.Generated)]
    [InlineData("create-once", FileGenerationStrategy.CreateOnce)]
    [InlineData("merge", FileGenerationStrategy.Merge)]
    [InlineData("custom", FileGenerationStrategy.Custom)]
    [InlineData("protected", FileGenerationStrategy.Protected)]
    public void ExactAdrNamesProduceTypedManifests(string name, FileGenerationStrategy expected)
    {
        TemplateCatalogReadResult result = new TemplateManifestJsonReader().Read(MinimalJson.Replace("create-once", name, StringComparison.Ordinal));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Diagnostic);
        TemplateManifest manifest = Assert.Single(result.Manifests);
        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal("template-readme", Assert.Single(manifest.Files).Id.Value);
        Assert.Equal("documentation-minimal", manifest.TemplateId.Value);
        Assert.Equal("0.1.0", manifest.TemplateVersion.Value);
        Assert.Equal("documentation", Assert.Single(manifest.SupportedProductIds).Value);
        Assert.Equal(expected, Assert.Single(manifest.Files).Strategy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    public void MalformedOrIncompleteDocumentsReturnOnlyABoundedDiagnostic(string json)
    {
        AssertInvalid(new TemplateManifestJsonReader().Read(json));
    }

    [Theory]
    [InlineData("\"schemaVersion\": 2", "\"schemaVersion\": 1")]
    [InlineData("\"schemaVersion\": 2", "\"schemaVersion\": 3")]
    [InlineData("\"schemaVersion\": 2", "\"schemaVersion\": \"2\"")]
    [InlineData("\"schemaVersion\": 2", "\"schemaVersion\": 2, \"schemaVersion\": 2")]
    [InlineData("\"schemaVersion\": 2,", "")]
    [InlineData("\"id\": \"template-readme\",", "")]
    [InlineData("\"id\"", "\"Id\"")]
    [InlineData("\"id\": \"template-readme\"", "\"id\": \"template-readme\", \"id\": \"other-id\"")]
    [InlineData("\"template-readme\"", "null")]
    [InlineData("\"template-readme\"", "true")]
    [InlineData("\"template-readme\"", "42")]
    [InlineData("\"template-readme\"", "\"\"")]
    [InlineData("\"template-readme\"", "\" \"")]
    [InlineData("\"template-readme\"", "\"\\uD800\"")]
    [InlineData("\"templateId\"", "\"TemplateId\"")]
    [InlineData("\"templateId\"", "\"\\uD800\"")]
    [InlineData("\"documentation-minimal\"", "\"\"")]
    [InlineData("\"documentation-minimal\"", "null")]
    [InlineData("\"documentation-minimal\"", "true")]
    [InlineData("\"documentation-minimal\"", "\"\\uD800\"")]
    [InlineData("\"0.1.0\"", "\"latest\"")]
    [InlineData("\"0.1.0\"", "\"*\"")]
    [InlineData("\"0.1.0\"", "\"0.1.0-preview\"")]
    [InlineData("\"0.1.0\"", "\"1.2\"")]
    [InlineData("\"0.1.0\"", "0.1")]
    [InlineData("[\"documentation\"]", "{}")]
    [InlineData("[\"documentation\"]", "[null]")]
    [InlineData("[\"documentation\"]", "[1]")]
    [InlineData("[\"documentation\"]", "[\"\"]")]
    [InlineData("\"files\": [", "\"files\": [null,")]
    [InlineData("\"relativePath\": \"README.md\"", "\"relativePath\": \"README.md\", \"extra\": true")]
    [InlineData("\"relativePath\": \"README.md\"", "\"relativePath\": \"README.md\", \"relativePath\": \"README.md\"")]
    [InlineData("\"source\": \"structure/readme\"", "\"$ref\": \"https://example.test/source\"")]
    [InlineData("\"README.md\"", "\"../outside\"")]
    [InlineData("\"README.md\"", "\"C:/outside\"")]
    [InlineData("\"structure/readme\"", "\"~/outside\"")]
    [InlineData("\"structure/readme\"", "\"\"")]
    [InlineData("\"create-once\"", "\"owned\"")]
    [InlineData("\"create-once\"", "\"manual\"")]
    [InlineData("\"create-once\"", "\"preserve\"")]
    [InlineData("\"create-once\"", "\"extension-point\"")]
    [InlineData("\"create-once\"", "\"future-strategy\"")]
    [InlineData("\"create-once\"", "\"Generated\"")]
    [InlineData("\"create-once\"", "\"generated, merge\"")]
    [InlineData("\"create-once\"", "0")]
    [InlineData("\"create-once\"", "\"create-once\",")]
    public void InvalidShapeTypesVersionsPathsOrStrategiesAreRejected(string original, string replacement)
    {
        Assert.Contains(original, MinimalJson, StringComparison.Ordinal);
        string json = MinimalJson.Replace(original, replacement, StringComparison.Ordinal);

        AssertInvalid(new TemplateManifestJsonReader().Read(json));
    }

    [Fact]
    public void NullInputRemainsAProgrammingError()
    {
        Assert.Throws<ArgumentNullException>(() => new TemplateManifestJsonReader().Read(null!));
    }

    [Fact]
    public void LegacyManifestCannotInventAnIdentityOrBeUpgradedImplicitly()
    {
        string legacy = MinimalJson.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal)
            .Replace("\"id\": \"template-readme\", ", "", StringComparison.Ordinal);

        AssertInvalid(new TemplateManifestJsonReader().Read(legacy));
    }

    [Fact]
    public void LogicalUnicodeSourcesArePreservedWithoutOpeningThem()
    {
        TemplateCatalogReadResult result = new TemplateManifestJsonReader().Read(
            MinimalJson.Replace("structure/readme", "structure/équipe", StringComparison.Ordinal));

        Assert.True(result.IsSuccess);
        Assert.Equal("structure/équipe", Assert.Single(Assert.Single(result.Manifests).Files).Source);
    }

    private static void AssertInvalid(TemplateCatalogReadResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Manifests);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(TemplateDiagnostics.InvalidManifest, result.Diagnostic);
        Assert.Equal("SF-TEMPLATE-003", result.Diagnostic.Code.Value);
        Assert.Equal("Le catalogue contient un manifest de template invalide.", result.Diagnostic.Message);
    }
}
