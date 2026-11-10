using System.Reflection;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class SiteFactoryLockTests
{
    [Fact]
    public void CandidateSnapshotsOnlyResolvedStructuralMetadata()
    {
        ClientConfigurationV1 configuration = Configuration();
        TemplateManifest manifest = Manifest();
        SiteFactoryVersion version = new("0.1.0-dev+abc123");

        SiteFactoryLock candidate = new(version, configuration, manifest);

        Assert.Equal(1, candidate.SchemaVersion);
        Assert.Equal(version, candidate.SiteFactoryVersion);
        Assert.Equal(configuration.SchemaVersion, candidate.ConfigurationSchemaVersion);
        Assert.Equal(new ProductId(configuration.Site.Product), candidate.ProductId);
        Assert.Equal(manifest.TemplateId, candidate.TemplateId);
        Assert.Equal(manifest.TemplateVersion, candidate.TemplateVersion);
        Assert.Equal(manifest.SchemaVersion, candidate.TemplateManifestSchemaVersion);
        Assert.DoesNotContain(typeof(SiteFactoryLock).GetProperties(), property => property.PropertyType == typeof(ClientConfigurationV1)
            || property.PropertyType == typeof(TemplateManifest));
        Assert.All(typeof(SiteFactoryLock).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.Empty(typeof(SiteFactoryLock).GetFields(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void RequestedModulesAreNotSilentlyPresentedAsAnEmptyResolvedComposition()
    {
        ClientConfigurationV1 configuration = Configuration(modules: ["contact-form"]);
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);

        ArgumentException error = Assert.Throws<ArgumentException>(nameof(configuration),
            () => new SiteFactoryLock(new("0.1.0-dev"), configuration, Manifest()));

        Assert.StartsWith("A lock cannot be created for unresolved modules; module resolution is not implemented.", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("contact-form", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("product")]
    [InlineData("template")]
    [InlineData("url")]
    public void InvalidConfigurationCannotBecomeALockCandidate(string defect)
    {
        ClientConfigurationV1 configuration = Configuration(defect: defect);

        // Offer identity now comes from the supplied manifest, not a hard-coded structural validator.
        string parameter = defect is "product" or "template" ? "resolvedTemplate" : nameof(configuration);
        Assert.Throws<ArgumentException>(parameter, () => new SiteFactoryLock(new("0.1.0-dev"), configuration, Manifest()));
    }

    [Theory]
    [InlineData("template-mismatch")]
    [InlineData("unsupported-product")]
    [InlineData("no-products")]
    [InlineData("duplicate-products")]
    [InlineData("no-files")]
    [InlineData("duplicate-ids")]
    [InlineData("duplicate-paths")]
    public void InvalidOrMismatchedManifestCannotSupplyResolvedMetadata(string defect)
    {
        TemplateFile first = File("readme", "README.md");
        TemplateFile[] files = defect switch
        {
            "no-files" => [],
            "duplicate-ids" => [first, File("readme", "other.md")],
            "duplicate-paths" => [first, File("other-id", "README.md")],
            _ => [first],
        };
        ProductId[] products = defect switch
        {
            "no-products" => [],
            "duplicate-products" => [new("fixture-product"), new("fixture-product")],
            "unsupported-product" => [new("other-product")],
            _ => [new("fixture-product")],
        };
        TemplateManifest manifest = new(new(defect == "template-mismatch" ? "other-template" : "fixture-template"), new("0.2.0"), products, files);

        Assert.Throws<ArgumentException>("resolvedTemplate", () => new SiteFactoryLock(new("0.1.0-dev"), Configuration(), manifest));
    }

    [Fact]
    public void IncompleteInputsRemainContractErrors()
    {
        Assert.Throws<ArgumentNullException>("siteFactoryVersion", () => new SiteFactoryLock(null!, Configuration(), Manifest()));
        Assert.Throws<ArgumentNullException>("configuration", () => new SiteFactoryLock(new("0.1.0-dev"), null!, Manifest()));
        Assert.Throws<ArgumentNullException>("resolvedTemplate", () => new SiteFactoryLock(new("0.1.0-dev"), Configuration(), null!));
    }

    private static ClientConfigurationV1 Configuration(string? defect = null, IEnumerable<string>? modules = null) =>
        new(defect == "schema" ? 2 : 1, new("acme-services", "Private display name"),
            new(defect == "product" ? "unknown-product" : "fixture-product", defect == "template" ? "unknown-template" : "fixture-template",
                "fr-FR", defect == "url" ? "invalid-url" : "https://www.example.test"),
            new(modules ?? []), new("docker-compose-vps"), new(true, false));

    private static TemplateManifest Manifest() => new(new("fixture-template"), new("0.2.0"), [new("fixture-product")], [File("readme", "README.md")]);

    private static TemplateFile File(string id, string path) => new(new(id), path, "structure/readme", FileGenerationStrategy.CreateOnce);
}
