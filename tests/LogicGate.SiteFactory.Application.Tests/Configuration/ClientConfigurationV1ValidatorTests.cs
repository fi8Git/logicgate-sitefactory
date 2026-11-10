using System.Globalization;
using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class ClientConfigurationV1ValidatorTests
{
    [Fact]
    public void ValidInitialConfigurationHasNoDiagnostics()
    {
        ConfigurationValidationResult result = Validate(CreateConfiguration());

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void UnsupportedSchemaVersionsAreRejected(int version)
    {
        AssertError(CreateConfiguration(schemaVersion: version), "SF-CONFIG-SCHEMA-002", "schemaVersion");
    }

    [Theory]
    [InlineData("client.slug")]
    [InlineData("client.displayName")]
    [InlineData("site.product")]
    [InlineData("site.template")]
    [InlineData("site.defaultLocale")]
    [InlineData("site.publicBaseUrl")]
    [InlineData("deployment.profile")]
    [InlineData("modules.enabled[0]")]
    public void RequiredValuesCannotBeEmptyOrWhitespace(string path)
    {
        foreach (string empty in new[] { "", " ", "\t\r\n", "\u00A0" })
        {
            ClientConfigurationV1 configuration = path switch
            {
                "client.slug" => CreateConfiguration(slug: empty),
                "client.displayName" => CreateConfiguration(displayName: empty),
                "site.product" => CreateConfiguration(product: empty),
                "site.template" => CreateConfiguration(template: empty),
                "site.defaultLocale" => CreateConfiguration(locale: empty),
                "site.publicBaseUrl" => CreateConfiguration(url: empty),
                "deployment.profile" => CreateConfiguration(profile: empty),
                "modules.enabled[0]" => CreateConfiguration(modules: [empty]),
                _ => throw new InvalidOperationException("Unknown test property."),
            };

            AssertError(configuration, "SF-CONFIG-SCHEMA-003", path);
        }
    }

    [Theory]
    [InlineData("Acme-services")]
    [InlineData("acme_services")]
    [InlineData("acmé-services")]
    [InlineData("acme.services")]
    [InlineData("../acme-services")]
    [InlineData("acme/services")]
    [InlineData("acme\\services")]
    [InlineData("-acme-services")]
    [InlineData("acme-services-")]
    [InlineData("acme--services")]
    [InlineData(" acme-services")]
    [InlineData("acme-services ")]
    [InlineData("acme-services\n")]
    [InlineData("acme\0services")]
    public void SlugMustUseAsciiKebabCaseWithoutNormalization(string slug)
    {
        AssertError(CreateConfiguration(slug: slug), "SF-CONFIG-SEMANTIC-001", "client.slug");
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void SlugLengthBoundariesAreEnforced(int length, bool valid)
    {
        ClientConfigurationV1 configuration = CreateConfiguration(slug: new string('a', length));

        if (valid)
            Assert.True(Validate(configuration).IsValid);
        else
            AssertError(configuration, "SF-CONFIG-SEMANTIC-001", "client.slug");
    }

    [Fact]
    public void ReservedSlugNamesAreRejectedIdenticallyOnEveryPlatform()
    {
        List<string> reserved = ["con", "prn", "aux", "nul"];

        for (int number = 1; number <= 9; number++)
        {
            reserved.Add("com" + number.ToString(CultureInfo.InvariantCulture));
            reserved.Add("lpt" + number.ToString(CultureInfo.InvariantCulture));
        }

        foreach (string slug in reserved)
            AssertError(CreateConfiguration(slug: slug), "SF-CONFIG-SEMANTIC-001", "client.slug");
    }

    [Theory]
    [InlineData("000")]
    [InlineData("3d-business")]
    [InlineData("con-services")]
    [InlineData("com0")]
    [InlineData("com10")]
    [InlineData("admin")]
    public void ValidSlugsDoNotAcquireRouteOrGenerationConstraints(string slug)
    {
        Assert.True(Validate(CreateConfiguration(slug: slug)).IsValid);
    }

    [Fact]
    public void DisplayNameAcceptsNonBlankUnicodeText()
    {
        Assert.True(Validate(CreateConfiguration(displayName: " Société Démo — 東京 🚀 ")).IsValid);
    }

    [Theory]
    [InlineData("unknown-product")]
    [InlineData("Fixture-Product")]
    [InlineData("fixture-product ")]
    public void UnknownProductIsRejectedByTheSuppliedOffer(string product)
    {
        AssertError(CreateConfiguration(product: product), "SF-CONFIG-SEMANTIC-009", "site.product");
    }

    [Theory]
    [InlineData("unknown-template")]
    [InlineData("Fixture-template")]
    [InlineData("fixture-template ")]
    public void UnknownTemplateIsRejectedByTheSuppliedOffer(string template)
    {
        AssertError(CreateConfiguration(template: template), "SF-CONFIG-SEMANTIC-006", "site.template");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    [InlineData("es-419")]
    [InlineData("zh-Hant-TW")]
    [InlineData("sr-Latn")]
    [InlineData("fil-PH")]
    [InlineData("fil-Latn-419")]
    [InlineData("FR-fr")]
    [InlineData("zz-ZZ")]
    public void LocaleFormatDoesNotDependOnInstalledCulturesOrARegistry(string locale)
    {
        Assert.True(Validate(CreateConfiguration(locale: locale)).IsValid);
    }

    [Theory]
    [InlineData("f")]
    [InlineData("f1-FR")]
    [InlineData("fr_FR")]
    [InlineData("fr-F")]
    [InlineData("fr-12")]
    [InlineData("fr-FRA")]
    [InlineData("en-Lat1-US")]
    [InlineData("en--US")]
    [InlineData("fr-FR-extra")]
    [InlineData("fr-FR\n")]
    [InlineData("fr-")]
    [InlineData("not-a-locale")]
    [InlineData("de-CH-1901")]
    public void InvalidOrUnsupportedV1LocaleFormatIsRejected(string locale)
    {
        AssertError(CreateConfiguration(locale: locale), "SF-CONFIG-SEMANTIC-010", "site.defaultLocale");
    }

    [Theory]
    [InlineData("not a URL")]
    [InlineData("/relative/path")]
    [InlineData("example.test")]
    [InlineData("//example.test")]
    [InlineData("https://")]
    [InlineData("https://exa mple.test")]
    [InlineData("https://example.test/a b")]
    [InlineData("https://example.test/%zz")]
    [InlineData("https:\\example.test")]
    [InlineData(" https://example.test")]
    [InlineData("https://example.test ")]
    [InlineData("https://example.test/\n")]
    [InlineData("https://example.test/\0")]
    [InlineData("ftp://example.test")]
    [InlineData("file:///not-a-real-file")]
    [InlineData("mailto:owner@example.test")]
    [InlineData("https://USER_SENTINEL:PASSWORD_SENTINEL@example.test")]
    [InlineData("https://USER_SENTINEL@example.test")]
    public void InvalidAbsoluteWebUrlIsRejectedWithoutThrowing(string url)
    {
        AssertError(CreateConfiguration(url: url), "SF-CONFIG-SEMANTIC-011", "site.publicBaseUrl");
    }

    [Fact]
    public void OversizeInvalidUrlProducesADiagnosticInsteadOfAnException()
    {
        AssertError(CreateConfiguration(url: "https://" + new string('a', 100_000)), "SF-CONFIG-SEMANTIC-011", "site.publicBaseUrl");
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("HTTP://example.test/path")]
    public void DockerComposeVpsRequiresHttps(string url)
    {
        AssertError(CreateConfiguration(url: url), "SF-CONFIG-SEMANTIC-012", "site.publicBaseUrl");
    }

    [Theory]
    [InlineData("https://www.example.test")]
    [InlineData("HTTPS://EXAMPLE.TEST/root")]
    [InlineData("https://example.test:8443/root/a%20b?key=value#anchor")]
    public void ValidHttpsUrlDoesNotRequireNetworkResolution(string url)
    {
        Assert.True(Validate(CreateConfiguration(url: url)).IsValid);
    }

    [Fact]
    public void UnknownProfileDoesNotInventAnHttpsPolicy()
    {
        AssertError(CreateConfiguration(profile: "unknown-profile", url: "http://example.test"), "SF-CONFIG-SEMANTIC-008", "deployment.profile");
    }

    [Theory]
    [InlineData("Module")]
    [InlineData("module_name")]
    [InlineData("modulé")]
    [InlineData("module.name")]
    [InlineData("../module")]
    [InlineData("module/path")]
    [InlineData("module\\path")]
    [InlineData("-module")]
    [InlineData("module-")]
    [InlineData("module--name")]
    [InlineData("module ")]
    [InlineData("module\n")]
    public void ModuleIdentifiersMustUseAsciiKebabCase(string module)
    {
        AssertError(CreateConfiguration(modules: [module]), "SF-CONFIG-SEMANTIC-001", "modules.enabled[0]");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void ModuleIdentifierLengthIsBounded(int length, bool valid)
    {
        ClientConfigurationV1 configuration = CreateConfiguration(modules: [new string('a', length)]);

        if (valid)
            Assert.True(Validate(configuration).IsValid);
        else
            AssertError(configuration, "SF-CONFIG-SEMANTIC-001", "modules.enabled[0]");
    }

    [Fact]
    public void DuplicateModulesAreReportedAtEachRepeatedEntry()
    {
        ConfigurationValidationResult result = Validate(CreateConfiguration(modules: ["seo", "content", "seo", "seo"]));

        Assert.False(result.IsValid);
        Assert.Equal(["modules.enabled[2]", "modules.enabled[3]"], result.Diagnostics.Select(static d => d.PropertyPath));
        Assert.All(result.Diagnostics, static d => Assert.Equal("SF-CONFIG-SEMANTIC-013", d.Code.Value));
    }

    [Fact]
    public void InvalidIdentifierIsNotSilentlyNormalizedIntoADuplicate()
    {
        AssertError(CreateConfiguration(modules: ["seo", "Seo"]), "SF-CONFIG-SEMANTIC-001", "modules.enabled[1]");
    }

    [Fact]
    public void EmptyOrUnknownModuleSelectionDoesNotRequireACatalogue()
    {
        Assert.True(Validate(CreateConfiguration(modules: [])).IsValid);
        Assert.True(Validate(CreateConfiguration(modules: ["future-module", "a", "123"])).IsValid);
    }

    [Theory]
    [InlineData("unknown-profile")]
    [InlineData("Docker-Compose-Vps")]
    [InlineData("docker-compose-vps ")]
    public void UnknownDeploymentProfileIsRejected(string profile)
    {
        AssertError(CreateConfiguration(profile: profile), "SF-CONFIG-SEMANTIC-008", "deployment.profile");
    }

    [Fact]
    public void InitialConfigurationRequiresLocalGitInitialization()
    {
        AssertError(CreateConfiguration(initializeGit: false), "SF-CONFIG-SEMANTIC-014", "git.initializeLocalRepository");
    }

    [Fact]
    public void RemotePublicationCannotBeRequestedByTheConfiguration()
    {
        AssertError(CreateConfiguration(publishRemote: true), "SF-CONFIG-SEMANTIC-015", "git.publishRemote");
    }

    [Fact]
    public void RemotePublicationWithoutLocalInitializationReportsBothGitErrors()
    {
        ConfigurationValidationResult result = Validate(CreateConfiguration(initializeGit: false, publishRemote: true));

        Assert.False(result.IsValid);
        Assert.Equal(["SF-CONFIG-SEMANTIC-014", "SF-CONFIG-SEMANTIC-015"], result.Diagnostics.Select(static d => d.Code.Value));
        Assert.Equal(["git.initializeLocalRepository", "git.publishRemote"], result.Diagnostics.Select(static d => d.PropertyPath));
    }

    [Fact]
    public void IndependentErrorsAreAllReturnedInTheExistingOrdinalOrder()
    {
        ConfigurationValidationResult result = Validate(CreateInvalidConfiguration());

        Assert.False(result.IsValid);
        Assert.Equal(
            [
                ("client.displayName", "SF-CONFIG-SCHEMA-003"),
                ("client.slug", "SF-CONFIG-SEMANTIC-001"),
                ("deployment.profile", "SF-CONFIG-SEMANTIC-008"),
                ("git.initializeLocalRepository", "SF-CONFIG-SEMANTIC-014"),
                ("git.publishRemote", "SF-CONFIG-SEMANTIC-015"),
                ("modules.enabled[0]", "SF-CONFIG-SEMANTIC-001"),
                ("modules.enabled[2]", "SF-CONFIG-SEMANTIC-013"),
                ("modules.enabled[3]", "SF-CONFIG-SCHEMA-003"),
                ("schemaVersion", "SF-CONFIG-SCHEMA-002"),
                ("site.defaultLocale", "SF-CONFIG-SEMANTIC-010"),
                ("site.product", "SF-CONFIG-SEMANTIC-009"),
                ("site.publicBaseUrl", "SF-CONFIG-SEMANTIC-011"),
                ("site.template", "SF-CONFIG-SEMANTIC-006"),
            ],
            result.Diagnostics.Select(static d => (d.PropertyPath, d.Code.Value)));
        Assert.All(result.Diagnostics, static d =>
        {
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
            Assert.False(string.IsNullOrWhiteSpace(d.Message));
        });
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void DiagnosticsAreRepeatableAndIndependentOfCurrentCulture(string culture)
    {
        ClientConfigurationV1 configuration = CreateInvalidConfiguration();
        ConfigurationValidationResult expected = Validate(configuration);
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture, useUserOverride: false);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;

            Assert.Equal(expected.Diagnostics, Validate(configuration).Diagnostics);
            Assert.Equal(expected.Diagnostics, Validate(configuration).Diagnostics);
            Assert.True(Validate(CreateConfiguration(locale: "FIL-latn-419")).IsValid);

            ConfigurationValidationResult duplicates = Validate(
                CreateConfiguration(modules: Enumerable.Repeat("module", 12)));
            Assert.Equal("modules.enabled[10]", duplicates.Diagnostics[0].PropertyPath);
            Assert.Equal("modules.enabled[11]", duplicates.Diagnostics[1].PropertyPath);
            Assert.Equal("modules.enabled[1]", duplicates.Diagnostics[2].PropertyPath);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void MultipleErrorsOnTheSamePropertyAreOrderedByCode()
    {
        ConfigurationValidationResult result = Validate(CreateConfiguration(modules: ["", ""]));

        Assert.Equal(
            [("modules.enabled[0]", "SF-CONFIG-SCHEMA-003"), ("modules.enabled[1]", "SF-CONFIG-SCHEMA-003"), ("modules.enabled[1]", "SF-CONFIG-SEMANTIC-013")],
            result.Diagnostics.Select(static d => (d.PropertyPath, d.Code.Value)));
    }

    [Fact]
    public void DiagnosticsDoNotEchoUntrustedValues()
    {
        const string sentinel = "PRIVATE_CONTENT_SENTINEL";
        ClientConfigurationV1 configuration = CreateConfiguration(
            slug: sentinel, product: sentinel, template: sentinel, locale: sentinel,
            url: "https://" + sentinel + "@example.test", modules: [sentinel, sentinel], profile: sentinel);

        ConfigurationValidationResult result = Validate(configuration);

        Assert.False(result.IsValid);
        Assert.All(result.Diagnostics, static d =>
        {
            Assert.DoesNotContain(sentinel, d.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, d.PropertyPath!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ValidationDoesNotNormalizeOrMutateTheConfiguration()
    {
        ClientConfigurationV1 configuration = CreateConfiguration(
            displayName: " Société Démo ", locale: "FR-fr", url: "HTTPS://EXAMPLE.TEST/a%20b", modules: ["seo", "future-module"]);

        Assert.True(Validate(configuration).IsValid);
        Assert.Equal(" Société Démo ", configuration.Client.DisplayName);
        Assert.Equal("FR-fr", configuration.Site.DefaultLocale);
        Assert.Equal("HTTPS://EXAMPLE.TEST/a%20b", configuration.Site.PublicBaseUrl);
        Assert.Equal(["seo", "future-module"], configuration.Modules.Enabled);
    }

    [Fact]
    public void NullContractIsAnApiMisuseRatherThanAnInvalidLoadedConfiguration()
    {
        Assert.Throws<ArgumentNullException>("configuration", () => Validate(null!));
    }

    private static void AssertError(ClientConfigurationV1 configuration, string code, string path)
    {
        ConfigurationValidationResult result = Validate(configuration);

        Assert.False(result.IsValid);
        ConfigurationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code.Value);
        Assert.Equal(path, diagnostic.PropertyPath);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
    }

    private static ClientConfigurationV1 CreateInvalidConfiguration() => CreateConfiguration(
        schemaVersion: 99, slug: "../invalid", displayName: "", product: "unknown-product", template: "unknown-template",
        locale: "not-a-locale", url: "not a URL", modules: ["Invalid_Module", "module", "module", ""],
        profile: "unknown-profile", initializeGit: false, publishRemote: true);

    private static ClientConfigurationV1 CreateConfiguration(
        int schemaVersion = 1,
        string slug = "acme-services",
        string displayName = "Acme Services",
        string product = "fixture-product",
        string template = "fixture-template",
        string locale = "fr-FR",
        string url = "https://www.example.test",
        IEnumerable<string>? modules = null,
        string profile = "docker-compose-vps",
        bool initializeGit = true,
        bool publishRemote = false) => new(
            schemaVersion,
            new(slug, displayName),
            new(product, template, locale, url),
            new(modules ?? ["content", "contact", "seo"]),
            new(profile),
            new(initializeGit, publishRemote));

    private static ConfigurationValidationResult Validate(ClientConfigurationV1 configuration) =>
        ClientConfigurationV1Validator.Validate(configuration, new TestOffer());

    // The availability rule now belongs to the supplied composition, not the structural validator.
    // The real built-in offer is covered by the unchanged CLI error/golden tests.
    private sealed class TestOffer : IConfigurationSelectionValidator
    {
        public IEnumerable<ConfigurationDiagnostic> ValidateSelection(SiteConfiguration site)
        {
            if (!string.IsNullOrWhiteSpace(site.Product) && site.Product != "fixture-product")
                yield return new(new("SF-CONFIG-SEMANTIC-009"), DiagnosticSeverity.Error, "Unsupported product.", "site.product");
            if (!string.IsNullOrWhiteSpace(site.Template) && site.Template != "fixture-template")
                yield return new(new("SF-CONFIG-SEMANTIC-006"), DiagnosticSeverity.Error, "Unsupported template.", "site.template");
        }
    }
}
