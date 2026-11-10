using System.Globalization;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

public sealed class ValidateCommandTests : IDisposable
{
    private const string ValidYaml = """
        schemaVersion: 1
        client:
          slug: acme-services
          displayName: "Société Démo — 東京 🚀"
        site:
          product: documentation
          template: documentation-minimal
          defaultLocale: fr-FR
          publicBaseUrl: https://www.example.test
        modules:
          enabled: []
        deployment:
          profile: docker-compose-vps
        git:
          initializeLocalRepository: true
          publishRemote: false
        """;

    private readonly string directory = Directory.CreateTempSubdirectory("sitefactory-cli-tests-").FullName;

    public static TheoryData<string[]> InvalidArguments { get; } = new()
    {
        new[] { "validate" },
        new[] { "validate", "--config" },
        new[] { "validate", "--config", "" },
        new[] { "validate", "--config", "  " },
        new[] { "validate", "--config", "bad\0path" },
        new[] { "validate", "--config", "bad\npath" },
        new[] { "validate", "--config", "--unknown" },
        new[] { "validate", "--config", "--help" },
        new[] { "validate", "--unknown" },
        new[] { "validate", "--help", "--unknown" },
        new[] { "validate", "--config", "missing.yml", "--unknown" },
        new[] { "validate", "--config", "missing.yml", "--json" },
        new[] { "validate", "--config", "missing.yml", "extra" },
        new[] { "validate", "--config", "missing.yml", "--config", "another.yml" },
        new[] { "validate", "--config=missing.yml" },
        new[] { "validate", "-c", "missing.yml" },
        new[] { "validate", "--Config", "missing.yml" },
        new[] { "Validate", "--config", "missing.yml" },
        new[] { "generate" },
        new[] { "plan" },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidUtf8ConfigurationUsesStdoutAndDoesNotChangeAnyFile(bool relativePath)
    {
        string path = await WriteAsync(ValidYaml);
        byte[] original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        string argument = relativePath ? Path.GetRelativePath(Environment.CurrentDirectory, path) : path;

        CliResult result = await RunPublicAsync("validate", "--config", argument);

        AssertSuccess(result);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidArgumentsReturnTwoBeforeTryingToLoadAFile(string[] arguments)
    {
        CliResult result = await RunPublicAsync(arguments);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.StartsWith("Erreur :", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("SF-CONFIG", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("missing.yml", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfigIsNotDiscoveredInTheWorkingDirectory()
    {
        await WriteAsync(ValidYaml, "client.sitefactory.yml");

        CliResult result = await RunProcessAsync("validate");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("--config", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandHelpDoesNotRequireAConfiguration()
    {
        CliResult result = await RunPublicAsync("validate", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: sitefactory validate --config <path>", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("15 schéma non supporté", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task RealCommandHelpPreservesAccentsInUtf8Output()
    {
        CliResult result = await RunProcessAsync("validate", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Aucune modification de fichier, génération ou opération Git.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task PlainBooleanDisplayNameIsATypeErrorButItsQuotedFormIsValidText()
    {
        string yaml = ValidYaml.Replace("\"Société Démo — 東京 🚀\"", "true", StringComparison.Ordinal);
        string path = await WriteAsync(yaml);

        CliResult invalid = await RunPublicAsync("validate", "--config", path);

        AssertFailure(invalid, 3, "SF-CONFIG-SCHEMA-005 client.displayName Le type de valeur ne correspond pas au contrat de configuration V1.\n");
        await WriteAsync(yaml.Replace("displayName: true", "displayName: \"true\"", StringComparison.Ordinal));
        AssertSuccess(await RunPublicAsync("validate", "--config", path));
    }

    [Theory]
    [InlineData(@"\UFFFFFFFF")]
    [InlineData(@"\U80000000")]
    [InlineData(@"\U00110000")]
    [InlineData(@"\uD800")]
    public async Task InvalidUnicodeEscapesReturnThreeWithoutParserDetails(string escape)
    {
        string yaml = ValidYaml.Replace("\"Société Démo — 東京 🚀\"", $"\"{escape}\"", StringComparison.Ordinal);
        string path = await WriteAsync(yaml);
        const string Expected = "SF-CONFIG-YAML-004 - La syntaxe du document YAML est invalide.\n";

        CliResult entrypoint = await RunPublicAsync("validate", "--config", path);
        CliResult process = await RunProcessAsync("validate", "--config", path);

        AssertFailure(entrypoint, 3, Expected);
        AssertFailure(process, 3, Expected);
        Assert.Equal(entrypoint, process);
        Assert.Equal(yaml, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task MissingFileReturnsThreeWithoutExposingThePhysicalPath()
    {
        CliResult result = await RunPublicAsync("validate", "--config", Path.Combine(directory, "PRIVATE_INPUT_SENTINEL.yml"));

        AssertFailure(result, 3, "SF-CONFIG-YAML-001 - Le fichier de configuration est introuvable.\n");
    }

    [Fact]
    public async Task DirectoryInsteadOfFileReturnsAConfigurationReadDiagnostic()
    {
        CliResult result = await RunPublicAsync("validate", "--config", directory);

        AssertFailure(result, 3, "SF-CONFIG-YAML-002 - Le fichier de configuration ne peut pas être lu.\n");
    }

    [Theory]
    [InlineData("schemaVersion: [\nPRIVATE_INPUT_SENTINEL: [", "SF-CONFIG-YAML-004 - La syntaxe du document YAML est invalide.\n")]
    [InlineData("", "SF-CONFIG-YAML-004 - Le fichier de configuration est vide.\n")]
    [InlineData("schemaVersion: 2", "SF-CONFIG-SCHEMA-002 schemaVersion Seule la version 1 du schéma est prise en charge.\n")]
    public async Task LoadingFailuresKeepTheExistingDiagnostic(string yaml, string expected)
    {
        string path = await WriteAsync(yaml);

        CliResult result = await RunPublicAsync("validate", "--config", path);

        AssertFailure(result, yaml == "schemaVersion: 2" ? 15 : 3, expected);
    }

    [Fact]
    public async Task UnknownPropertyDoesNotEchoItsNameOrValue()
    {
        string path = await WriteAsync(ValidYaml + "\nPRIVATE_INPUT_SENTINEL: <PRIVATE_VALUE_SENTINEL>\n");

        CliResult result = await RunPublicAsync("validate", "--config", path);

        AssertFailure(result, 3, "SF-CONFIG-SCHEMA-004 - Une propriété inconnue est présente dans la configuration.\n");
    }

    [Theory]
    [InlineData("schemaVersion: !include PRIVATE_INPUT_SENTINEL", "SF-CONFIG-YAML-008")]
    [InlineData("schemaVersion: &PRIVATE_INPUT_SENTINEL 1", "SF-CONFIG-YAML-007")]
    public async Task RestrictedYamlIsRejectedByTheExistingLoader(string yaml, string diagnosticCode)
    {
        string path = await WriteAsync(yaml);

        CliResult result = await RunPublicAsync("validate", "--config", path);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.StartsWith(diagnosticCode + " - ", result.StandardError, StringComparison.Ordinal);
        AssertSafeError(result.StandardError);
    }

    [Fact]
    public async Task SemanticDiagnosticsAreStableAndDoNotEchoTheDocument()
    {
        string yaml = ValidYaml
            .Replace("acme-services", "PRIVATE_INPUT_SENTINEL", StringComparison.Ordinal)
            .Replace("documentation", "<PRIVATE_VALUE_SENTINEL>", StringComparison.Ordinal);
        string path = await WriteAsync(yaml);
        const string Expected = """
            SF-CONFIG-SEMANTIC-001 client.slug Le slug doit contenir de 3 à 80 caractères ASCII en kebab-case, sans nom réservé.

            """;

        CliResult first = await RunPublicAsync("validate", "--config", path);
        CliResult second = await RunPublicAsync("validate", "--config", path);

        AssertFailure(first, 3, Expected);
        Assert.Equal(first, second);
        Assert.Equal(yaml, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task CancellationReturnsTwelveWithoutLoadingAFile()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter standardOutput = CreateWriter();
        using StringWriter standardError = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(
            ["validate", "--config", Path.Combine(directory, "missing.yml")],
            new(new ModuleCatalog([]), []),
            standardOutput, standardError, cancellation.Token);

        AssertFailure(new(exitCode, standardOutput.ToString(), standardError.ToString()), 12, "Opération annulée.\n");
    }

    [Fact]
    public async Task CancellationDuringDiagnosticOutputReturnsTwelve()
    {
        string path = await WriteAsync(ValidYaml.Replace("documentation", "unknown", StringComparison.Ordinal));
        using CancellationTokenSource cancellation = new();
        using StringWriter standardOutput = CreateWriter();
        using CancellingWriter standardError = new(cancellation);

        int exitCode = await SiteFactoryCli.RunAsync(
            ["validate", "--config", path], CreatePublicComposition(), standardOutput, standardError, cancellation.Token);

        AssertFailure(new(exitCode, standardOutput.ToString(), standardError.ToString()), 12, "Opération annulée.\n");
    }

    [Fact]
    public async Task UnexpectedFailureReturnsOneWithoutExceptionDetails()
    {
        string path = await WriteAsync(ValidYaml);
        using FailingWriter standardOutput = new();
        using StringWriter standardError = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(
            ["validate", "--config", path], CreatePublicComposition(), standardOutput, standardError, TestContext.Current.CancellationToken);

        AssertFailure(new(exitCode, standardOutput.ToString(), standardError.ToString()), 1,
            "Erreur : la commande a échoué de manière inattendue.\n");
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 12)]
    public async Task ClosedErrorStreamStillReturnsAControlledExitCode(bool cancelled, int expected)
    {
        using CancellationTokenSource cancellation = new();
        if (cancelled)
            cancellation.Cancel();
        using StringWriter standardOutput = CreateWriter();
        using FailingWriter standardError = new();

        int exitCode = await SiteFactoryCli.RunAsync(
            ["validate"], new(new ModuleCatalog([]), []), standardOutput, standardError, cancellation.Token);

        Assert.Equal(expected, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Theory]
    [InlineData("valid", 0, "")]
    [InlineData("arguments", 2, "Erreur :")]
    [InlineData("missing", 3, "SF-CONFIG-YAML-001 - ")]
    [InlineData("yaml", 3, "SF-CONFIG-YAML-004 - ")]
    [InlineData("semantic", 4, "SF-TEMPLATE-002 site.product ")]
    [InlineData("unknown", 2, "Erreur :")]
    [InlineData("version", 15, "SF-CONFIG-SCHEMA-002 schemaVersion ")]
    public async Task RealProcessUsesTheSameExitCodesAndStreams(string scenario, int exitCode, string errorPrefix)
    {
        string yaml = scenario switch
        {
            "yaml" => "schemaVersion: [",
            "semantic" => ValidYaml.Replace("product: documentation", "product: PRIVATE_INPUT_SENTINEL", StringComparison.Ordinal),
            "version" => "schemaVersion: 2",
            _ => ValidYaml,
        };
        string path = await WriteAsync(yaml);
        string[] arguments = scenario switch
        {
            "arguments" => ["validate", "--config"],
            "missing" => ["validate", "--config", "PRIVATE_INPUT_SENTINEL.yml"],
            "unknown" => ["validate", "--config", Path.GetFileName(path), "--unknown"],
            _ => ["validate", "--config", Path.GetFileName(path)],
        };

        CliResult result = await RunProcessAsync(arguments);

        Assert.Equal(exitCode, result.ExitCode);
        if (exitCode == 0)
            AssertSuccess(result);
        else
        {
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.StartsWith(errorPrefix, result.StandardError, StringComparison.Ordinal);
            AssertSafeError(result.StandardError);
        }

        Assert.Equal(yaml, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    [Theory]
    [InlineData("valid", 0, "")]
    [InlineData("unknown-template", 4, "SF-TEMPLATE-001 site.template Le template demandé est absent du catalogue.\n")]
    [InlineData("unknown-product", 4, "SF-TEMPLATE-002 site.product Le template ne prend pas en charge le produit demandé.\n")]
    [InlineData("business-template", 4, "SF-TEMPLATE-001 site.template Le template demandé est absent du catalogue.\n")]
    public async Task PublicValidateResolvesOnlyTheExplicitDocumentationComposition(string scenario, int exitCode, string error)
    {
        string yaml = DocumentationYaml();
        yaml = scenario switch
        {
            "unknown-template" => yaml.Replace("documentation-minimal", "PRIVATE_INPUT_SENTINEL", StringComparison.Ordinal),
            "unknown-product" => yaml.Replace("product: documentation", "product: PRIVATE_INPUT_SENTINEL", StringComparison.Ordinal),
            "business-template" => yaml.Replace("documentation-minimal", "unregistered-template", StringComparison.Ordinal),
            _ => yaml,
        };
        string path = await WriteAsync(yaml);
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationComposition composition = DocumentationComposition(module, module.CreateContribution());
        using StringWriter output = CreateWriter();
        using StringWriter errors = CreateWriter();

        int code = await SiteFactoryCli.RunAsync(["validate", "--config", path], composition,
            output, errors, TestContext.Current.CancellationToken);
        CliResult injected = new(code, output.ToString(), errors.ToString());
        CliResult actualPublicProcess = await CliProcessRunner.RunAsync(directory,
            ["validate", "--config", Path.GetFileName(path)], TestContext.Current.CancellationToken);

        Assert.Equal(injected, actualPublicProcess);
        if (exitCode == 0)
            AssertSuccess(actualPublicProcess);
        else
            AssertFailure(actualPublicProcess, exitCode, error);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task EmptyInjectedCatalogCannotValidateAWellFormedPublicTarget()
    {
        string path = await WriteAsync(ValidYaml);
        GenerationComposition empty = new(new ModuleCatalog([]), []);
        using StringWriter output = CreateWriter();
        using StringWriter error = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(["validate", "--config", path], empty,
            output, error, TestContext.Current.CancellationToken);

        AssertFailure(new(exitCode, output.ToString(), error.ToString()), 4,
            "SF-TEMPLATE-001 site.template Le template demandé est absent du catalogue.\n");
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task PublicValidationPropagatesCancellationDuringCatalogResolution()
    {
        string path = await WriteAsync(DocumentationYaml());
        using CancellationTokenSource cancellation = new();
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationContribution source = module.CreateContribution();
        GenerationContribution contribution = new(source.ModuleId, source.ModuleVersion, source.TemplateId,
            new CancellingCatalog(cancellation), source.Content);
        using StringWriter output = CreateWriter();
        using StringWriter error = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(["validate", "--config", path],
            DocumentationComposition(module, contribution), output, error, cancellation.Token);

        AssertFailure(new(exitCode, output.ToString(), error.ToString()), 12, "Opération annulée.\n");
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    private static string DocumentationYaml() => ValidYaml;

    private static GenerationComposition DocumentationComposition(DocumentationModule module, GenerationContribution contribution) =>
        new(new ModuleCatalog([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]), [contribution]);

    private sealed class CancellingCatalog(CancellationTokenSource cancellation) : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Assert.Equal(cancellation.Token, cancellationToken);
            cancellation.Cancel();
            return Task.FromCanceled<TemplateCatalogReadResult>(cancellationToken);
        }
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task<string> WriteAsync(string yaml, string fileName = "configuration démo.sitefactory.yml")
    {
        string path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, yaml, new UTF8Encoding(false), TestContext.Current.CancellationToken);
        return path;
    }

    private static async Task<CliResult> RunPublicAsync(params string[] args)
    {
        using StringWriter standardOutput = CreateWriter();
        using StringWriter standardError = CreateWriter();
        int exitCode = await SiteFactoryCli.RunAsync(args, CreatePublicComposition(), standardOutput, standardError, TestContext.Current.CancellationToken);
        return new(exitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static GenerationComposition CreatePublicComposition()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        return new(new ModuleCatalog([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]),
            [module.CreateContribution()]);
    }

    private Task<CliResult> RunProcessAsync(params string[] args) =>
        CliProcessRunner.RunAsync(directory, args, TestContext.Current.CancellationToken);

    private static void AssertSuccess(CliResult result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("La configuration SiteFactory est valide.\n", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private void AssertFailure(CliResult result, int exitCode, string error)
    {
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(error, result.StandardError);
        AssertSafeError(result.StandardError);
    }

    private void AssertSafeError(string error)
    {
        Assert.DoesNotContain(directory, error, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_INPUT_SENTINEL", error, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_VALUE_SENTINEL", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", error, StringComparison.Ordinal);
        Assert.DoesNotContain("schemaVersion:", error, StringComparison.Ordinal);
    }

    private static StringWriter CreateWriter() => new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    private sealed class CancellingWriter(CancellationTokenSource cancellation) : StringWriter(CultureInfo.InvariantCulture)
    {
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            NewLine = "\n";
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed class FailingWriter : StringWriter
    {
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("PRIVATE_INPUT_SENTINEL: <PRIVATE_VALUE_SENTINEL>"));

        public override Task WriteLineAsync(string? value) =>
            Task.FromException(new IOException("PRIVATE_INPUT_SENTINEL: <PRIVATE_VALUE_SENTINEL>"));
    }
}
