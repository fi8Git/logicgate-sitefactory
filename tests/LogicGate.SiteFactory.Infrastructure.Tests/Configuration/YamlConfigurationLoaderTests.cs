using System.Globalization;
using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Infrastructure.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Configuration;

public sealed class YamlConfigurationLoaderTests : IDisposable
{
    private const string ValidYaml = """
        schemaVersion: 1
        client:
          slug: acme-services
          displayName: "Société Démo — 東京 🚀"
        site:
          product: fixture-product
          template: fixture-template
          defaultLocale: fr-FR
          publicBaseUrl: https://www.example.test
        modules:
          enabled: [content, contact, seo]
        deployment:
          profile: docker-compose-vps
        git:
          initializeLocalRepository: true
          publishRemote: false
        """;

    private readonly string directory = Directory.CreateTempSubdirectory("sitefactory-yaml-tests-").FullName;

    public static TheoryData<string> AmbiguousStringScalars { get; } = new()
    {
        "true", "false", "True", "FALSE",
        "123", "-123", "+123", "01230", "0",
        "12.4", "-12.4", ".5", "1.", "1e3", "-2E-5", "1e9999",
        "0x3A", "0o17", ".inf", "-.Inf", "+.INF", ".NaN",
        "null", "Null", "NULL", "~", "",
        "2026-09-14", "2026-07-28T10:00:00Z", "2026-09-14T12:30:45.123+02:00",
        "2026-09-14 12:30:00", "2026-9-14", "2026-99-99",
    };

    [Theory]
    [MemberData(nameof(AmbiguousStringScalars))]
    public async Task PlainAmbiguousStringScalarsAreRejectedWithStableDiagnostics(string value)
    {
        string yaml = ValidYaml.Replace("\"Société Démo — 東京 🚀\"", value, StringComparison.Ordinal);

        ConfigurationDiagnostic first = AssertFailure(await LoadAsync(yaml), "SF-CONFIG-SCHEMA-005");
        ConfigurationDiagnostic second = AssertFailure(await LoadAsync(yaml), "SF-CONFIG-SCHEMA-005");

        Assert.Equal("client.displayName", first.PropertyPath);
        Assert.Equal("Le type de valeur ne correspond pas au contrat de configuration V1.", first.Message);
        Assert.Equal(first, second);
    }

    [Theory]
    [MemberData(nameof(AmbiguousStringScalars))]
    public async Task PlainAmbiguousModuleElementsAreRejectedAtTheirLogicalIndex(string value)
    {
        string yaml = ValidYaml.Replace("[content, contact, seo]", $"\n    - content\n    - {value}\n", StringComparison.Ordinal);

        ConfigurationDiagnostic diagnostic = AssertFailure(await LoadAsync(yaml), "SF-CONFIG-SCHEMA-005");

        Assert.Equal("modules.enabled[1]", diagnostic.PropertyPath);
    }

    [Theory]
    [MemberData(nameof(AmbiguousStringScalars))]
    public async Task QuotedAmbiguousScalarsRemainExactStringsIncludingModuleElements(string value)
    {
        foreach (char quote in "\"'")
        {
            string quoted = $"{quote}{value}{quote}";
            string yaml = ValidYaml.Replace("\"Société Démo — 東京 🚀\"", quoted, StringComparison.Ordinal)
                .Replace("acme-services", quoted, StringComparison.Ordinal)
                .Replace("[content, contact, seo]", $"[content, {quoted}]", StringComparison.Ordinal);

            ConfigurationLoadResult result = await LoadAsync(yaml);

            Assert.Empty(result.Validation.Diagnostics);
            ClientConfigurationV1 configuration = Assert.IsType<ClientConfigurationV1>(result.Configuration);
            Assert.Equal(value, configuration.Client.DisplayName);
            Assert.Equal(value, configuration.Client.Slug);
            Assert.Equal(["content", value], configuration.Modules.Enabled);
            Assert.Equal(1, configuration.SchemaVersion);
            Assert.True(configuration.Git.InitializeLocalRepository);
            Assert.False(configuration.Git.PublishRemote);
        }
    }

    [Theory]
    [InlineData("acme-services", "client.slug")]
    [InlineData("fixture-product", "site.product")]
    [InlineData("fixture-template", "site.template")]
    [InlineData("fr-FR", "site.defaultLocale")]
    [InlineData("https://www.example.test", "site.publicBaseUrl")]
    [InlineData("docker-compose-vps", "deployment.profile")]
    public async Task EveryTextPropertyRejectsAnUnquotedInteger(string original, string propertyPath)
    {
        ConfigurationDiagnostic diagnostic = AssertFailure(
            await LoadAsync(ValidYaml.Replace(original, "123", StringComparison.Ordinal)), "SF-CONFIG-SCHEMA-005");

        Assert.Equal(propertyPath, diagnostic.PropertyPath);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("true-story")]
    [InlineData("123-business")]
    [InlineData("1.2.0")]
    [InlineData("2026-09-14-campaign")]
    [InlineData("Public Démo — 東京 🚀")]
    public async Task UnambiguousPlainTextRemainsUnchanged(string value)
    {
        ConfigurationLoadResult result = await LoadAsync(
            ValidYaml.Replace("\"Société Démo — 東京 🚀\"", value, StringComparison.Ordinal));

        Assert.Empty(result.Validation.Diagnostics);
        Assert.Equal(value, Assert.IsType<ClientConfigurationV1>(result.Configuration).Client.DisplayName);
    }

    [Theory]
    [InlineData("|-")]
    [InlineData(">-")]
    public async Task ExplicitBlockStringStylesAreNotTreatedAsImplicitTypes(string style)
    {
        ConfigurationLoadResult result = await LoadAsync(
            ValidYaml.Replace("\"Société Démo — 東京 🚀\"", $"{style}\n    123", StringComparison.Ordinal));

        Assert.Empty(result.Validation.Diagnostics);
        Assert.Equal("123", Assert.IsType<ClientConfigurationV1>(result.Configuration).Client.DisplayName);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public async Task AmbiguousStringDetectionDoesNotDependOnCulture(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            foreach (string value in new[] { "12.4", "2026-09-14", ".INF" })
                AssertFailure(await LoadAsync(ValidYaml.Replace("\"Société Démo — 東京 🚀\"", value, StringComparison.Ordinal)), "SF-CONFIG-SCHEMA-005");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Utf8DocumentLoadsTheCompleteV1ContractWithoutChangingTheFile(bool bom, bool crlf)
    {
        string yaml = crlf ? ValidYaml.Replace("\n", "\r\n", StringComparison.Ordinal) : ValidYaml;
        string path = await WriteAsync(yaml, bom);
        byte[] original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        ConfigurationLoadResult result = await YamlConfigurationLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.Validation.IsValid);
        Assert.Empty(result.Validation.Diagnostics);
        ClientConfigurationV1 configuration = Assert.IsType<ClientConfigurationV1>(result.Configuration);
        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal("acme-services", configuration.Client.Slug);
        Assert.Equal("Société Démo — 東京 🚀", configuration.Client.DisplayName);
        Assert.Equal("fixture-product", configuration.Site.Product);
        Assert.Equal("fixture-template", configuration.Site.Template);
        Assert.Equal("fr-FR", configuration.Site.DefaultLocale);
        Assert.Equal("https://www.example.test", configuration.Site.PublicBaseUrl);
        Assert.Equal(["content", "contact", "seo"], configuration.Modules.Enabled);
        Assert.Equal("docker-compose-vps", configuration.Deployment.Profile);
        Assert.True(configuration.Git.InitializeLocalRepository);
        Assert.False(configuration.Git.PublishRemote);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingFileReturnsADiagnosticWithoutItsPhysicalPath()
    {
        string path = Path.Combine(directory, "PRIVATE_PATH_SENTINEL.yml");

        ConfigurationLoadResult result = await YamlConfigurationLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        ConfigurationDiagnostic diagnostic = AssertFailure(result, "SF-CONFIG-YAML-001");
        Assert.DoesNotContain("PRIVATE_PATH_SENTINEL", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingParentDirectoryReturnsAMissingFileDiagnostic()
    {
        ConfigurationLoadResult result = await YamlConfigurationLoader.LoadAsync(
            Path.Combine(directory, "missing", "client.sitefactory.yml"), TestContext.Current.CancellationToken);

        AssertFailure(result, "SF-CONFIG-YAML-001");
    }

    [Fact]
    public async Task DirectoryInsteadOfFileReturnsAReadDiagnostic()
    {
        ConfigurationLoadResult result = await YamlConfigurationLoader.LoadAsync(directory, TestContext.Current.CancellationToken);

        AssertFailure(result, "SF-CONFIG-YAML-002");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t\n")]
    [InlineData("# Empty configuration\n")]
    [InlineData("---\n")]
    public async Task EmptyDocumentsFailWithoutAConfiguration(string yaml)
    {
        AssertFailure(await LoadAsync(yaml), "SF-CONFIG-YAML-004");
    }

    [Theory]
    [InlineData("client: [unterminated")]
    [InlineData("client: {slug: acme")]
    [InlineData("client: \"UNTRUSTED_CONTENT_SENTINEL")]
    [InlineData("schemaVersion: [\nUNTRUSTED_CONTENT_SENTINEL: [")]
    public async Task InvalidSyntaxReturnsAControlledDiagnostic(string yaml)
    {
        ConfigurationDiagnostic diagnostic = AssertFailure(await LoadAsync(yaml), "SF-CONFIG-YAML-004");

        Assert.DoesNotContain("UNTRUSTED_CONTENT_SENTINEL", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"\UFFFFFFFF")]
    [InlineData(@"\U80000000")]
    [InlineData(@"\U00110000")]
    [InlineData(@"\uD800")]
    public async Task InvalidUnicodeEscapesReturnAStableSyntaxDiagnostic(string escape)
    {
        string yaml = ValidYaml.Replace("\"Société Démo — 東京 🚀\"", $"\"{escape}\"", StringComparison.Ordinal);

        ConfigurationDiagnostic first = AssertFailure(await LoadAsync(yaml), "SF-CONFIG-YAML-004");
        ConfigurationDiagnostic second = AssertFailure(await LoadAsync(yaml), "SF-CONFIG-YAML-004");

        Assert.Null(first.PropertyPath);
        Assert.Equal("La syntaxe du document YAML est invalide.", first.Message);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("\"\\U0010FFFF\"", "\U0010FFFF")]
    [InlineData("\"\\U0001F680\"", "🚀")]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("'\\UFFFFFFFF'", @"\UFFFFFFFF")]
    [InlineData("\"\\\\UFFFFFFFF\"", @"\UFFFFFFFF")]
    public async Task ValidUnicodeEscapesAndLiteralBackslashesRemainText(string scalar, string expected)
    {
        ConfigurationLoadResult result = await LoadAsync(
            ValidYaml.Replace("\"Société Démo — 東京 🚀\"", scalar, StringComparison.Ordinal));

        Assert.Empty(result.Validation.Diagnostics);
        Assert.Equal(expected, Assert.IsType<ClientConfigurationV1>(result.Configuration).Client.DisplayName);
    }

    [Theory]
    [InlineData("schemaVersion: 1", "schemaVersion: 1\nUNTRUSTED_CONTENT_SENTINEL: true", null)]
    [InlineData("schemaVersion: 1", "SchemaVersion: 1", null)]
    [InlineData("client:", "client:\n  include: another-file.yml", "client")]
    [InlineData("displayName:", "DisplayName:", "client")]
    [InlineData("site:", "site:\n  unknown: true", "site")]
    [InlineData("modules:", "modules:\n  unknown: []", "modules")]
    [InlineData("deployment:", "deployment:\n  unknown: true", "deployment")]
    [InlineData("git:", "git:\n  unknown: true", "git")]
    public async Task UnknownPropertiesAreRejectedAtEveryLevel(string oldText, string newText, string? parentPath)
    {
        ConfigurationDiagnostic diagnostic = AssertFailure(
            await LoadAsync(ValidYaml.Replace(oldText, newText, StringComparison.Ordinal)), "SF-CONFIG-SCHEMA-004");

        Assert.Equal(parentPath, diagnostic.PropertyPath);
        Assert.DoesNotContain("UNTRUSTED_CONTENT_SENTINEL", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("another-file.yml", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schemaVersion: 1\n", "", "SF-CONFIG-SCHEMA-001", "schemaVersion")]
    [InlineData("schemaVersion: 1", "schemaVersion: 99", "SF-CONFIG-SCHEMA-002", "schemaVersion")]
    [InlineData("schemaVersion: 1", "schemaVersion: \"1\"", "SF-CONFIG-SCHEMA-005", "schemaVersion")]
    [InlineData("schemaVersion: 1", "schemaVersion: null", "SF-CONFIG-SCHEMA-005", "schemaVersion")]
    [InlineData("  slug: acme-services\n", "", "SF-CONFIG-SCHEMA-003", "client.slug")]
    [InlineData("slug: acme-services", "slug: null", "SF-CONFIG-SCHEMA-005", "client.slug")]
    [InlineData("slug: acme-services", "slug: ~", "SF-CONFIG-SCHEMA-005", "client.slug")]
    [InlineData("slug: acme-services", "slug: []", "SF-CONFIG-SCHEMA-005", "client.slug")]
    [InlineData("enabled: [content, contact, seo]", "enabled: {}", "SF-CONFIG-SCHEMA-005", "modules.enabled")]
    [InlineData("enabled: [content, contact, seo]", "enabled: [content, null]", "SF-CONFIG-SCHEMA-005", "modules.enabled[1]")]
    [InlineData("  publishRemote: false", "", "SF-CONFIG-SCHEMA-003", "git.publishRemote")]
    [InlineData("publishRemote: false", "publishRemote: \"false\"", "SF-CONFIG-SCHEMA-005", "git.publishRemote")]
    [InlineData("publishRemote: false", "publishRemote: yes", "SF-CONFIG-SCHEMA-005", "git.publishRemote")]
    [InlineData("publishRemote: false", "publishRemote: 1", "SF-CONFIG-SCHEMA-005", "git.publishRemote")]
    public async Task MissingOrMistypedFieldsDoNotAcquireImplicitDefaults(string oldText, string newText, string code, string path)
    {
        ConfigurationDiagnostic diagnostic = AssertFailure(
            await LoadAsync(ValidYaml.Replace(oldText, newText, StringComparison.Ordinal)), code);

        Assert.Equal(path, diagnostic.PropertyPath);
    }

    [Fact]
    public async Task LoadingDoesNotApplyFunctionalValidationNormalizationOrEnvironmentInterpolation()
    {
        string yaml = ValidYaml
            .Replace("acme-services", "\" Invalid_Public_Slug \"", StringComparison.Ordinal)
            .Replace("Société Démo — 東京 🚀", "${PATH}", StringComparison.Ordinal)
            .Replace("fixture-product", "unknown-product", StringComparison.Ordinal)
            .Replace("fixture-template", "unknown-template", StringComparison.Ordinal)
            .Replace("fr-FR", "not-a-locale", StringComparison.Ordinal)
            .Replace("https://www.example.test", "not a URL", StringComparison.Ordinal)
            .Replace("[content, contact, seo]", "[unknown-module, unknown-module]", StringComparison.Ordinal)
            .Replace("publishRemote: false", "publishRemote: true", StringComparison.Ordinal);

        ConfigurationLoadResult result = await LoadAsync(yaml);

        Assert.True(result.Validation.IsValid);
        ClientConfigurationV1 configuration = Assert.IsType<ClientConfigurationV1>(result.Configuration);
        Assert.Equal(" Invalid_Public_Slug ", configuration.Client.Slug);
        Assert.Equal("${PATH}", configuration.Client.DisplayName);
        Assert.Equal("unknown-product", configuration.Site.Product);
        Assert.Equal("unknown-template", configuration.Site.Template);
        Assert.Equal("not-a-locale", configuration.Site.DefaultLocale);
        Assert.Equal("not a URL", configuration.Site.PublicBaseUrl);
        Assert.Equal(["unknown-module", "unknown-module"], configuration.Modules.Enabled);
        Assert.True(configuration.Git.PublishRemote);
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Fact]
    public async Task EmptyModulesAndQuotedEmptyTextRemainAvailableForLaterValidation()
    {
        string yaml = ValidYaml.Replace("[content, contact, seo]", "[]", StringComparison.Ordinal)
            .Replace("acme-services", "\"\"", StringComparison.Ordinal);

        ConfigurationLoadResult result = await LoadAsync(yaml);

        Assert.True(result.Validation.IsValid);
        ClientConfigurationV1 configuration = Assert.IsType<ClientConfigurationV1>(result.Configuration);
        Assert.Empty(configuration.Modules.Enabled);
        Assert.Equal("", configuration.Client.Slug);
    }

    [Theory]
    [InlineData("schemaVersion: 1\nschemaVersion: 1", "SF-CONFIG-YAML-006")]
    [InlineData("client: {slug: a, slug: b}", "SF-CONFIG-YAML-006")]
    [InlineData("---\nschemaVersion: 1\n---\nschemaVersion: 1", "SF-CONFIG-YAML-005")]
    [InlineData("---\nschemaVersion: 1\n---\n", "SF-CONFIG-YAML-005")]
    [InlineData("client: &client {slug: a}", "SF-CONFIG-YAML-007")]
    [InlineData("client: *missing", "SF-CONFIG-YAML-007")]
    [InlineData("client: {<<: {slug: a}}", "SF-CONFIG-YAML-009")]
    [InlineData("client: !include another-file.yml", "SF-CONFIG-YAML-008")]
    [InlineData("client: !include https://example.test/config.yml", "SF-CONFIG-YAML-008")]
    [InlineData("client: !!System.Diagnostics.Process {}", "SF-CONFIG-YAML-008")]
    [InlineData("client: !!map {}", "SF-CONFIG-YAML-008")]
    [InlineData("client: {slug: !!str a}", "SF-CONFIG-YAML-008")]
    [InlineData("? [client, site]\n: value", "SF-CONFIG-SCHEMA-005")]
    public async Task UnsupportedYamlFeaturesAreRejectedBeforeMapping(string yaml, string code)
    {
        AssertFailure(await LoadAsync(yaml), code);
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
    }

    [Theory]
    [InlineData(new byte[] { 0xC3, 0x28 })]
    [InlineData(new byte[] { 0xEF, 0xBB })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x61, 0x00 })]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x61 })]
    public async Task InvalidUtf8AndOtherEncodingBomsAreRejected(byte[] bytes)
    {
        string path = Path.Combine(directory, "client.sitefactory.yml");
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

        AssertFailure(await YamlConfigurationLoader.LoadAsync(path, TestContext.Current.CancellationToken), "SF-CONFIG-YAML-011");
    }

    [Fact]
    public async Task FileExactlyAtTheByteLimitCanBeLoaded()
    {
        int paddingLength = YamlConfigurationLoader.MaximumFileSizeBytes - Encoding.UTF8.GetByteCount(ValidYaml) - 2;
        string yaml = ValidYaml + "\n#" + new string(' ', paddingLength);

        ConfigurationLoadResult result = await LoadAsync(yaml);

        Assert.True(result.Validation.IsValid);
        Assert.NotNull(result.Configuration);
    }

    [Fact]
    public async Task FileAboveTheByteLimitIsRejectedBeforeParsing()
    {
        string path = Path.Combine(directory, "client.sitefactory.yml");
        await File.WriteAllBytesAsync(path, new byte[YamlConfigurationLoader.MaximumFileSizeBytes + 1], TestContext.Current.CancellationToken);

        AssertFailure(await YamlConfigurationLoader.LoadAsync(path, TestContext.Current.CancellationToken), "SF-CONFIG-YAML-003");
    }

    [Theory]
    [InlineData(0, "SF-CONFIG-SCHEMA-005")]
    [InlineData(1, "SF-CONFIG-YAML-010")]
    public async Task DepthLimitIsEnforcedBeforeBuildingTheEntireTree(int excess, string code)
    {
        int depth = YamlConfigurationLoader.MaximumDepth + excess;
        string yaml = new string('[', depth) + "0" + new string(']', depth);

        AssertFailure(await LoadAsync(yaml), code);
    }

    [Theory]
    [InlineData(0, "SF-CONFIG-SCHEMA-005")]
    [InlineData(1, "SF-CONFIG-SCHEMA-006")]
    public async Task NodeLimitIsEnforcedBeforeMapping(int excess, string code)
    {
        string yaml = "[" + string.Join(',', Enumerable.Repeat("a", YamlConfigurationLoader.MaximumNodeCount - 1 + excess)) + "]";

        AssertFailure(await LoadAsync(yaml), code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationIsPropagatedInsteadOfBecomingADiagnostic(bool fileExists)
    {
        string path = fileExists ? await WriteAsync(ValidYaml) : Path.Combine(directory, "missing.yml");
        using CancellationTokenSource source = new();
        source.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => YamlConfigurationLoader.LoadAsync(path, source.Token));

        Assert.Equal(source.Token, exception.CancellationToken);
    }

    [Fact]
    public void ApplicationAndDomainDoNotReferenceTheYamlAssembly()
    {
        foreach (Type marker in new[] { typeof(ClientConfigurationV1), typeof(Domain.AssemblyMarker) })
            Assert.DoesNotContain(marker.Assembly.GetReferencedAssemblies(), static assembly => assembly.Name == "YamlDotNet");
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task<string> WriteAsync(string text, bool bom = false)
    {
        string path = Path.Combine(directory, "client.sitefactory.yml");
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(bom), TestContext.Current.CancellationToken);
        return path;
    }

    private async Task<ConfigurationLoadResult> LoadAsync(string text) =>
        await YamlConfigurationLoader.LoadAsync(await WriteAsync(text), TestContext.Current.CancellationToken);

    private static ConfigurationDiagnostic AssertFailure(ConfigurationLoadResult result, string code)
    {
        Assert.Null(result.Configuration);
        Assert.False(result.Validation.IsValid);
        ConfigurationDiagnostic diagnostic = Assert.Single(result.Validation.Diagnostics);
        Assert.Equal(code, diagnostic.Code.Value);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        return diagnostic;
    }
}
