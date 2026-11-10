using System.Globalization;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Cli.Commands.Plan;
using LogicGate.SiteFactory.Infrastructure.Configuration;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

public sealed class PlanCommandTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("sitefactory-plan-tests-").FullName;

    public static TheoryData<string[]> InvalidArguments { get; } = new()
    {
        new[] { "plan" },
        new[] { "plan", "--config" },
        new[] { "plan", "--config", "" },
        new[] { "plan", "--config", " " },
        new[] { "plan", "--config", "bad\0path" },
        new[] { "plan", "--config", "bad\npath" },
        new[] { "plan", "--config", "--help" },
        new[] { "plan", "--config", "--unknown" },
        new[] { "plan", "--unknown" },
        new[] { "plan", "--help", "--config", "missing.yml" },
        new[] { "plan", "--config", "missing.yml", "extra" },
        new[] { "plan", "--config", "missing.yml", "--config", "second.yml" },
        new[] { "plan", "--config=missing.yml" },
        new[] { "plan", "-c", "missing.yml" },
        new[] { "plan", "--Config", "missing.yml" },
        new[] { "Plan", "--config", "missing.yml" },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealPipelineProducesExactJsonBytesWithoutChangingTheFilesystem(bool existingGit)
    {
        string path = CopyFixture("valid-minimal");
        string firstWorkingDirectory = Directory.CreateDirectory(Path.Combine(directory, "working one")).FullName;
        string secondWorkingDirectory = Directory.CreateDirectory(Path.Combine(directory, "working two")).FullName;
        string metadataDirectory = Directory.CreateDirectory(Path.Combine(directory, "src", "Generated")).FullName;
        await File.WriteAllTextAsync(Path.Combine(directory, "README.md"), "Fictitious existing content.", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(metadataDirectory, "SiteMetadata.cs"), "Fictitious protected content.", TestContext.Current.CancellationToken);
        if (existingGit)
        {
            string gitDirectory = Directory.CreateDirectory(Path.Combine(directory, ".git")).FullName;
            await File.WriteAllTextAsync(Path.Combine(gitDirectory, "config"), "Fictitious Git sentinel.", TestContext.Current.CancellationToken);
        }

        ConfigurationLoadResult loaded = await YamlConfigurationLoader.LoadAsync(path, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded.Configuration);
        Assert.True(ClientConfigurationV1Validator.Validate(loaded.Configuration).IsValid);
        Assert.True(loaded.Configuration.Git.InitializeLocalRepository); // Must remain a preference, not an executed action.
        TemplateResolutionResult resolved = await new TemplateResolver(new DocumentationModule(new TemplateManifestJsonReader()))
            .ResolveAsync(loaded.Configuration, TestContext.Current.CancellationToken);
        Assert.NotNull(resolved.Template);
        GenerationPlan expected = GenerationPlanBuilder.Build(new("plan:fictitious-documentation:documentation:documentation-minimal"),
            loaded.Configuration, resolved.Template);
        FileSystemEntry[] before = Snapshot();

        CliBytesResult first = await CliProcessRunner.RunBytesAsync(firstWorkingDirectory,
            ["plan", "--config", Path.GetRelativePath(firstWorkingDirectory, path)], TestContext.Current.CancellationToken);
        CliBytesResult second = await CliProcessRunner.RunBytesAsync(secondWorkingDirectory,
            ["plan", "--config", path], TestContext.Current.CancellationToken);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Empty(first.StandardError);
        Assert.Empty(second.StandardError);
        Assert.Equal(GenerationPlanJsonSerializer.Serialize(expected), first.StandardOutput);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal((byte)'{', first.StandardOutput[0]); // No BOM, banner or log prefix.
        Assert.Equal((byte)'}', first.StandardOutput[^2]);
        Assert.Equal((byte)'\n', first.StandardOutput[^1]);
        Assert.DoesNotContain((byte)'\r', first.StandardOutput);
        using JsonDocument document = JsonDocument.Parse(first.StandardOutput);
        Assert.Equal("fictitious-documentation", document.RootElement.GetProperty("client").GetString());
        Assert.Equal(["guide.html", "index.html"], document.RootElement.GetProperty("plannedFiles")
            .EnumerateArray().Select(static file => file.GetProperty("relativePath").GetString()));
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.DoesNotContain(directory, Encoding.UTF8.GetString(first.StandardOutput), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task DocumentedSampleWorksInARealProcess()
    {
        string sample = Path.Combine(AppContext.BaseDirectory, "samples", "configuration", "valid-minimal", "client.sitefactory.yml");
        byte[] before = await File.ReadAllBytesAsync(sample, TestContext.Current.CancellationToken);

        CliBytesResult result = await CliProcessRunner.RunBytesAsync(directory, ["plan", "--config", sample], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("plan:fictitious-documentation:documentation:documentation-minimal", document.RootElement.GetProperty("id").GetString());
        Assert.Equal(before, await File.ReadAllBytesAsync(sample, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Theory]
    [InlineData("invalid-schema-version", 15, "SF-CONFIG-SCHEMA-002")]
    [InlineData("invalid-unknown-property", 3, "SF-CONFIG-SCHEMA-004")]
    [InlineData("invalid-product", 4, "SF-TEMPLATE-002")]
    [InlineData("invalid-public-url", 3, "SF-CONFIG-SEMANTIC-011")]
    [InlineData("unknown-template", 4, "SF-TEMPLATE-001")]
    [InlineData("invalid-yaml", 3, "SF-CONFIG-YAML-004")]
    [InlineData("invalid-unicode", 3, "SF-CONFIG-YAML-004")]
    [InlineData("empty", 3, "SF-CONFIG-YAML-004")]
    [InlineData("missing", 3, "SF-CONFIG-YAML-001")]
    public async Task InvalidConfigurationMatchesValidateExactlyAndNeverEmitsAPlan(string scenario, int expectedExitCode, string code)
    {
        string path;
        if (scenario == "missing")
        {
            path = Path.Combine(directory, "missing-parent", "PRIVATE_INPUT_SENTINEL.yml");
        }
        else
        {
            path = CopyFixture(scenario.StartsWith("invalid-", StringComparison.Ordinal) && scenario is not ("invalid-yaml" or "invalid-unicode")
                ? scenario : "valid-minimal");
            string original = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            string yaml = scenario switch
            {
                "unknown-template" => original.Replace("documentation-minimal", "PRIVATE_INPUT_SENTINEL", StringComparison.Ordinal),
                "invalid-yaml" => "schemaVersion: [",
                "invalid-unicode" => "client: { displayName: \"\\UFFFFFFFF\" }",
                "empty" => string.Empty,
                _ => original,
            };
            await File.WriteAllTextAsync(path, yaml, TestContext.Current.CancellationToken);
        }

        FileSystemEntry[] before = Snapshot();
        CliResult validate = await CliProcessRunner.RunAsync(directory, ["validate", "--config", path], TestContext.Current.CancellationToken);
        CliResult plan = await CliProcessRunner.RunAsync(directory, ["plan", "--config", path], TestContext.Current.CancellationToken);

        Assert.Equal(expectedExitCode, plan.ExitCode);
        Assert.Equal(validate, plan);
        Assert.Equal(string.Empty, plan.StandardOutput);
        Assert.StartsWith(code + " ", plan.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_INPUT_SENTINEL", plan.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, plan.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", plan.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidArgumentsFailBeforeFileAccess(string[] arguments)
    {
        CliResult result = await RunPublicAsync(arguments);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.StartsWith("Erreur :", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("SF-CONFIG", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("--apply")]
    [InlineData("--force")]
    [InlineData("--upgrade")]
    [InlineData("--dry-run")]
    [InlineData("--plan-file")]
    [InlineData("--initialize-git")]
    [InlineData("--json")]
    public async Task ForbiddenOptionsAreRejectedWithoutSideEffects(string option)
    {
        string path = CopyFixture("valid-minimal");
        FileSystemEntry[] before = Snapshot();

        CliResult result = await CliProcessRunner.RunAsync(directory, ["plan", "--config", path, option], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Usage: sitefactory plan --config <path>", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task HelpAndMissingOptionNeverDiscoverAConfiguration()
    {
        string path = Path.Combine(directory, "client.sitefactory.yml");
        await File.WriteAllTextAsync(path, "invalid: [", TestContext.Current.CancellationToken);
        FileSystemEntry[] before = Snapshot();

        CliResult help = await CliProcessRunner.RunAsync(directory, ["plan", "--help"], TestContext.Current.CancellationToken);
        CliResult missingOption = await CliProcessRunner.RunAsync(directory, ["plan"], TestContext.Current.CancellationToken);
        CliResult rootHelp = await RunPublicAsync(["--help"]);

        Assert.Equal(0, help.ExitCode);
        Assert.Equal(string.Empty, help.StandardError);
        Assert.Contains("Usage: sitefactory plan --config <path>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Aucun dossier créé", help.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(2, missingOption.ExitCode);
        Assert.Equal(string.Empty, missingOption.StandardOutput);
        Assert.DoesNotContain("SF-CONFIG", missingOption.StandardError, StringComparison.Ordinal);
        Assert.Contains("sitefactory plan --config <path>", rootHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public async Task PlanRemainsIdenticalAcrossCulturesAndWriterNewlineSettings(string cultureName)
    {
        string path = CopyFixture("valid-minimal");
        CliResult expected = await RunPublicAsync(["plan", "--config", path]);
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            using StringWriter output = new() { NewLine = "\r\n" };
            using StringWriter error = new() { NewLine = "\r\n" };

            int exitCode = await SiteFactoryCli.RunAsync(["plan", "--config", path], CreatePublicComposition(), output, error, TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Equal(expected.StandardOutput, output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("absent", "SF-TEMPLATE-001")]
    [InlineData("incompatible", "SF-TEMPLATE-002")]
    [InlineData("invalid", "SF-TEMPLATE-003")]
    [InlineData("ambiguous", "SF-TEMPLATE-004")]
    public async Task CatalogFailuresUseTheRealResolverAndNeverProduceJson(string scenario, string code)
    {
        string path = CopyFixture("valid-minimal");
        TemplateCatalogReadResult catalog = scenario switch
        {
            "absent" => TemplateCatalogReadResult.Success([]),
            "incompatible" => TemplateCatalogReadResult.Success([Manifest("other-product")]),
            "invalid" => TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest),
            "ambiguous" => TemplateCatalogReadResult.Success([Manifest(), Manifest()]),
            _ => throw new InvalidOperationException(),
        };
        using StringWriter output = Writer();
        using StringWriter error = Writer();
        FileSystemEntry[] before = Snapshot();

        int exitCode = await PlanCommand.RunAsync(path, new FixedCatalog(catalog), null, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith(code + " ", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData(false, "SF-GEN-FILE-006")]
    [InlineData(true, "SF-GEN-FILE-012")]
    public async Task BlockedPlannerResultDoesNotPublishAPartialPlan(bool collision, string code)
    {
        string path = CopyFixture("valid-minimal");
        TemplateFile[] files = collision
            ? [new(new("a"), "a.md", "logical/a", FileGenerationStrategy.Generated),
                new(new("b"), "A.md", "logical/b", FileGenerationStrategy.Generated)]
            : [new(new("merge"), "merge.json", "logical/merge", FileGenerationStrategy.Merge),
                new(new("readme"), "README.md", "logical/readme", FileGenerationStrategy.Generated)];
        TemplateManifest manifest = new(new("documentation-minimal"), new("0.2.0"), [new("documentation")], files);
        using StringWriter output = Writer();
        using StringWriter error = Writer();
        FileSystemEntry[] before = Snapshot();

        int exitCode = await PlanCommand.RunAsync(path, new FixedCatalog(TemplateCatalogReadResult.Success([manifest])), null,
            output, error, TestContext.Current.CancellationToken);

        Assert.Equal(5, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith(code + " ", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task InvalidConfigurationStopsBeforeCatalogResolution()
    {
        string path = CopyFixture("invalid-public-url");
        using StringWriter output = Writer();
        using StringWriter error = Writer();

        int exitCode = await PlanCommand.RunAsync(path, new NeverReadCatalog(), null, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(3, exitCode);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task CancellationReturnsTwelveBeforeLoadingAFile()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter output = Writer();
        using StringWriter error = Writer();

        int exitCode = await SiteFactoryCli.RunAsync(["plan", "--config", Path.Combine(directory, "missing.yml")],
            new(new ModuleCatalog([]), []), output, error, cancellation.Token);

        Assert.Equal(12, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("Opération annulée.\n", error.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task CancellationIsPropagatedToCatalogResolutionWithoutPublishingJson()
    {
        string path = CopyFixture("valid-minimal");
        using CancellationTokenSource cancellation = new();
        using StringWriter output = Writer();
        using StringWriter error = Writer();
        FileSystemEntry[] before = Snapshot();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PlanCommand.RunAsync(path,
            new CancellingCatalog(cancellation), null, output, error, cancellation.Token));

        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task CancellationDuringOutputReturnsTwelveWithoutAnUnexpectedError()
    {
        string path = CopyFixture("valid-minimal");
        using CancellationTokenSource cancellation = new();
        using CancellingWriter output = new(cancellation);
        using StringWriter error = Writer();
        FileSystemEntry[] before = Snapshot();

        int exitCode = await SiteFactoryCli.RunAsync(["plan", "--config", path], CreatePublicComposition(), output, error, cancellation.Token);

        Assert.Equal(12, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("Opération annulée.\n", error.ToString());
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task OutputFailureUsesTheExistingSafeCliErrorBoundary()
    {
        string path = CopyFixture("valid-minimal");
        using FailingWriter output = new();
        using StringWriter error = Writer();
        FileSystemEntry[] before = Snapshot();

        int exitCode = await SiteFactoryCli.RunAsync(["plan", "--config", path], CreatePublicComposition(), output, error, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("Erreur : la commande a échoué de manière inattendue.\n", error.ToString());
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task PublicDocumentationPlansAreByteIdenticalAndUseOnlyTheInjectedTemplate()
    {
        string path = CopyFixture("valid-minimal");
        string yaml = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))
            .Replace("documentation", "documentation", StringComparison.Ordinal)
            .Replace("documentation-minimal", "documentation-minimal", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, yaml, TestContext.Current.CancellationToken);
        FileSystemEntry[] before = Snapshot();

        CliBytesResult first = await CliProcessRunner.RunBytesAsync(directory,
            ["plan", "--config", path], TestContext.Current.CancellationToken);
        CliBytesResult second = await CliProcessRunner.RunBytesAsync(directory,
            ["plan", "--config", Path.GetFileName(path)], TestContext.Current.CancellationToken);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Empty(first.StandardError);
        Assert.Empty(second.StandardError);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.DoesNotContain((byte)'\r', first.StandardOutput);
        using JsonDocument document = JsonDocument.Parse(first.StandardOutput);
        Assert.Equal("documentation", document.RootElement.GetProperty("product").GetString());
        Assert.Equal("documentation-minimal", document.RootElement.GetProperty("template").GetString());
        Assert.Equal(["guide.html", "index.html"], document.RootElement.GetProperty("plannedFiles")
            .EnumerateArray().Select(static file => file.GetProperty("relativePath").GetString()));
        Assert.Equal(before, Snapshot());
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string CopyFixture(string scenario)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "fixtures", "configuration", scenario, "client.sitefactory.yml");
        string target = Path.Combine(directory, "configuration démo.sitefactory.yml");
        File.Copy(source, target);
        return target;
    }

    private FileSystemEntry[] Snapshot() => Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
        .Prepend(directory)
        .Order(StringComparer.Ordinal)
        .Select(path => new FileSystemEntry(Path.GetRelativePath(directory, path), File.GetAttributes(path),
            File.GetLastWriteTimeUtc(path), Directory.Exists(path) ? null : Convert.ToBase64String(File.ReadAllBytes(path))))
        .ToArray();

    private static async Task<CliResult> RunPublicAsync(string[] args)
    {
        using StringWriter output = Writer();
        using StringWriter error = Writer();
        int exitCode = await SiteFactoryCli.RunAsync(args, CreatePublicComposition(), output, error, TestContext.Current.CancellationToken);
        return new(exitCode, output.ToString(), error.ToString());
    }

    private static GenerationComposition CreatePublicComposition()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        return new(new ModuleCatalog([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]),
            [module.CreateContribution()]);
    }

    private static StringWriter Writer() => new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    private static TemplateManifest Manifest(string product = "documentation") =>
        new(new("documentation-minimal"), new("0.2.0"), [new(product)],
            [new(new("readme"), "README.md", "logical/readme", FileGenerationStrategy.Generated)]);

    private sealed record FileSystemEntry(string Path, FileAttributes Attributes, DateTime LastWriteTimeUtc, string? Content);

    private sealed class FixedCatalog(TemplateCatalogReadResult result) : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class NeverReadCatalog : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The catalog must not be read for invalid configuration.");
    }

    private sealed class FailingWriter : StringWriter
    {
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("PRIVATE_INPUT_SENTINEL"));
    }

    private sealed class CancellingCatalog(CancellationTokenSource cancellation) : ITemplateCatalog
    {
        public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Assert.Equal(cancellation.Token, cancellationToken);
            cancellation.Cancel();
            return Task.FromCanceled<TemplateCatalogReadResult>(cancellationToken);
        }
    }

    private sealed class CancellingWriter(CancellationTokenSource cancellation) : StringWriter
    {
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            Assert.Equal(cancellation.Token, cancellationToken);
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }
}
