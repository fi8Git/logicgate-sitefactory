using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class ClientConfigurationV1Tests
{
    [Fact]
    public void VerticalSliceConfigurationRetainsItsInputsAfterTheSourceModulesChange()
    {
        List<string> sourceModules = ["content", "contact", "seo"];
        ClientConfigurationV1 configuration = new(
            schemaVersion: 1,
            client: new("acme-services", "Acme Services"),
            site: new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"),
            modules: new(sourceModules),
            deployment: new("docker-compose-vps"),
            git: new(initializeLocalRepository: true, publishRemote: false));

        sourceModules[0] = "media";
        sourceModules.Clear();

        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal("acme-services", configuration.Client.Slug);
        Assert.Equal("Acme Services", configuration.Client.DisplayName);
        Assert.Equal("fixture-product", configuration.Site.Product);
        Assert.Equal("fixture-template", configuration.Site.Template);
        Assert.Equal("fr-FR", configuration.Site.DefaultLocale);
        Assert.Equal("https://www.example.test", configuration.Site.PublicBaseUrl);
        Assert.Equal(["content", "contact", "seo"], configuration.Modules.Enabled);
        Assert.Equal("docker-compose-vps", configuration.Deployment.Profile);
        Assert.True(configuration.Git.InitializeLocalRepository);
        Assert.False(configuration.Git.PublishRemote);
    }

    [Fact]
    public void ConstructionDoesNotValidateOrNormalizeBusinessValues()
    {
        ClientConfigurationV1 configuration = new(
            schemaVersion: 99,
            client: new(" Invalid_Public_Slug ", ""),
            site: new("unknown-product", "unknown-template", "not-a-locale", "not a URL"),
            modules: new(["Unknown_Module", "", "Unknown_Module"]),
            deployment: new("unknown-profile"),
            git: new(initializeLocalRepository: false, publishRemote: true));

        Assert.Equal(99, configuration.SchemaVersion);
        Assert.Equal(" Invalid_Public_Slug ", configuration.Client.Slug);
        Assert.Equal("", configuration.Client.DisplayName);
        Assert.Equal("unknown-product", configuration.Site.Product);
        Assert.Equal("unknown-template", configuration.Site.Template);
        Assert.Equal("not-a-locale", configuration.Site.DefaultLocale);
        Assert.Equal("not a URL", configuration.Site.PublicBaseUrl);
        Assert.Equal(["Unknown_Module", "", "Unknown_Module"], configuration.Modules.Enabled);
        Assert.Equal("unknown-profile", configuration.Deployment.Profile);
        Assert.False(configuration.Git.InitializeLocalRepository);
        Assert.True(configuration.Git.PublishRemote);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("site")]
    [InlineData("modules")]
    [InlineData("deployment")]
    [InlineData("git")]
    public void RequiredSectionsCannotBeNull(string missingSection)
    {
        Assert.Throws<ArgumentNullException>(missingSection, () => new ClientConfigurationV1(
            1,
            missingSection == "client" ? null! : new("acme-services", "Acme Services"),
            missingSection == "site" ? null! : new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"),
            missingSection == "modules" ? null! : new([]),
            missingSection == "deployment" ? null! : new("docker-compose-vps"),
            missingSection == "git" ? null! : new(true, false)));
    }

    [Fact]
    public void RequiredTextCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>("slug", () => new ClientIdentityConfiguration(null!, "Acme Services"));
        Assert.Throws<ArgumentNullException>("displayName", () => new ClientIdentityConfiguration("acme-services", null!));
        Assert.Throws<ArgumentNullException>("product", () => new SiteConfiguration(null!, "fixture-template", "fr-FR", "https://www.example.test"));
        Assert.Throws<ArgumentNullException>("template", () => new SiteConfiguration("fixture-product", null!, "fr-FR", "https://www.example.test"));
        Assert.Throws<ArgumentNullException>("defaultLocale", () => new SiteConfiguration("fixture-product", "fixture-template", null!, "https://www.example.test"));
        Assert.Throws<ArgumentNullException>("publicBaseUrl", () => new SiteConfiguration("fixture-product", "fixture-template", "fr-FR", null!));
        Assert.Throws<ArgumentNullException>("profile", () => new DeploymentConfiguration(null!));
    }
}
