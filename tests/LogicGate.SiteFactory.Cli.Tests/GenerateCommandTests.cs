using System.Globalization;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

public sealed class GenerateCommandTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-generate-cli-").FullName;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "samples", "generation", "initial-minimal", "client.sitefactory.yml");
    private string Output => Path.Combine(sandbox, "output");

    [Fact]
    public async Task RealDocumentedCommandCreatesMinimalFilesAndRejectsLocalGeneratedChangesWithoutWriting()
    {
        byte[] inputBefore = await File.ReadAllBytesAsync(Sample, Token);
        CliResult first = await CliProcessRunner.RunAsync(sandbox, ["generate", "--config", Sample, "--output", "./output"], Token);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal("SiteFactory minimal generation completed (no Git; client quality gates are separate).\n", first.StandardOutput);
        Assert.Equal([".sitefactory/generation-state.json", "generation-report.json", "guide.html", "index.html", "sitefactory.lock.json"],
            Directory.GetFiles(Output, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(Output, path).Replace('\\', '/')).Order(StringComparer.Ordinal));
        Assert.Contains("href=\"guide.html\"", await File.ReadAllTextAsync(Path.Combine(Output, "index.html"), Token), StringComparison.Ordinal);
        Assert.Contains("<html", await File.ReadAllTextAsync(Path.Combine(Output, "index.html"), Token), StringComparison.Ordinal);
        using JsonDocument candidate = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(Output, "sitefactory.lock.json"), Token));
        Assert.Equal(1, candidate.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("documentation-minimal", candidate.RootElement.GetProperty("template").GetProperty("id").GetString());
        Assert.Equal("0.1.0", candidate.RootElement.GetProperty("template").GetProperty("version").GetString());
        Assert.Empty(candidate.RootElement.GetProperty("modules").EnumerateArray());
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(Output, "generation-report.json"), Token));
        Assert.Equal("success", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, report.RootElement.GetProperty("createdFiles").GetArrayLength());

        // Unplanned client content and generated-file local changes must both survive a second command.
        await File.AppendAllTextAsync(Path.Combine(Output, "README.md"), "Client customization.\n", Token);
        await File.AppendAllTextAsync(Path.Combine(Output, "index.html"), "// Local change.\n", Token);
        string[] before = Snapshot();
        CliResult second = await CliProcessRunner.RunAsync(sandbox, ["generate", "--output", Output, "--config", Sample], Token);

        Assert.Equal(6, second.ExitCode);
        Assert.Equal(string.Empty, second.StandardOutput);
        Assert.StartsWith("SF-GEN-FILE-004 index.html ", second.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
        Assert.Equal(inputBefore, await File.ReadAllBytesAsync(Sample, Token));
    }

    [Fact]
    public async Task TwoFreshRootsProduceByteIdenticalUtf8LfArtifacts()
    {
        string secondRoot = Path.Combine(sandbox, "other root");
        foreach (string root in new[] { Output, secondRoot })
        {
            CliBytesResult result = await CliProcessRunner.RunBytesAsync(sandbox, ["generate", "--output", root, "--config", Sample], Token);
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.StandardError);
        }

        foreach (string first in Directory.GetFiles(Output, "*", SearchOption.AllDirectories))
        {
            byte[] bytes = await File.ReadAllBytesAsync(first, Token);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(secondRoot, Path.GetRelativePath(Output, first)), Token));
            string text = new UTF8Encoding(false, true).GetString(bytes);
            Assert.DoesNotContain('\r', text);
            Assert.DoesNotContain('\uFEFF', text);
            Assert.EndsWith("\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task CorruptStateUnicodeIsAConflictWithoutWrites()
    {
        string[] arguments = ["generate", "--config", Sample, "--output", Output];
        CliResult first = await CliProcessRunner.RunAsync(sandbox, arguments, Token);
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        string statePath = Path.Combine(Output, ".sitefactory", "generation-state.json");
        string original = await File.ReadAllTextAsync(statePath, Token);
        using JsonDocument document = JsonDocument.Parse(original);
        string hash = document.RootElement.GetProperty("reportSha256").GetString()!;
        string corrupted = original.Replace($"\"reportSha256\": \"{hash}\"", "\"reportSha256\": \"\\uD800\"", StringComparison.Ordinal);
        Assert.Contains("\"reportSha256\": \"\\uD800\"", corrupted, StringComparison.Ordinal);
        await File.WriteAllTextAsync(statePath, corrupted, Token);
        string[] before = Snapshot();

        const string expectedDiagnostic = "SF-GEN-STATE-001 .sitefactory/generation-state.json "
            + "The generation state is invalid or does not match this configuration and composition; no files were changed.\n";
        for (int attempt = 0; attempt < 2; attempt++)
        {
            CliResult result = await CliProcessRunner.RunAsync(sandbox, arguments, Token);
            Assert.Equal(6, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Equal(expectedDiagnostic, result.StandardError); // No exception, stack trace or physical path.
            Assert.Equal(before, Snapshot()); // Includes content, timestamps, state, report and directory entries.
        }
    }

    [Theory]
    [InlineData("invalid-schema-version", 15)]
    [InlineData("invalid-unknown-property", 3)]
    [InlineData("invalid-product", 4)]
    [InlineData("invalid-public-url", 3)]
    [InlineData("missing", 3)]
    public async Task InvalidConfigurationHasExactlyTheValidateDiagnosticsAndCreatesNothing(string scenario, int exitCode)
    {
        string config = scenario == "missing" ? Path.Combine(sandbox, "missing.yml")
            : Path.Combine(AppContext.BaseDirectory, "fixtures", "configuration", scenario, "client.sitefactory.yml");
        CliResult validation = await CliProcessRunner.RunAsync(sandbox, ["validate", "--config", config], Token);
        CliResult generation = await CliProcessRunner.RunAsync(sandbox, ["generate", "--config", config, "--output", Output], Token);

        Assert.Equal(exitCode, generation.ExitCode);
        Assert.Equal(validation.ExitCode, generation.ExitCode);
        Assert.Equal(validation.StandardError, generation.StandardError);
        Assert.Equal(string.Empty, generation.StandardOutput);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData("invalid-yaml", 3)]
    [InlineData("unknown-template", 4)]
    public async Task RealInputFailuresCreateNoOutput(string scenario, int exitCode)
    {
        string config = Path.Combine(AppContext.BaseDirectory, "fixtures", "generation-plan", "end-to-end", scenario, "client.sitefactory.yml");
        CliResult result = await CliProcessRunner.RunAsync(sandbox, ["generate", "--config", config, "--output", Output], Token);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.NotEmpty(result.StandardError);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData("file")]
    [InlineData("unsafe")]
    [InlineData("git")]
    [InlineData("volume")]
    public async Task InvalidOutputIsSanitizedAndCannotWrite(string scenario)
    {
        string output = scenario switch
        {
            "unsafe" => Path.Combine(sandbox, "child", "..", "escape"),
            "git" => Path.Combine(sandbox, ".GiT", "child"),
            "volume" => Path.GetPathRoot(sandbox)!,
            _ => Output,
        };
        if (scenario == "file") await File.WriteAllTextAsync(output, "Existing sentinel.", Token);
        string[] before = Snapshot();

        CliResult result = await CliProcessRunner.RunAsync(sandbox, ["generate", "--config", Sample, "--output", output], Token);

        Assert.Equal(11, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.StartsWith("SF-GEN-FILE-001 output ", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(sandbox, result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task ExistingEmptyOutputIsAConflictNotImplicitPermissionToGenerate()
    {
        Directory.CreateDirectory(Output);
        string[] before = Snapshot();
        CliResult result = await CliProcessRunner.RunAsync(sandbox, ["generate", "--config", Sample, "--output", Output], Token);
        Assert.Equal(6, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData("enabled: []", "enabled: [test-module]", "SF-GEN-INITIAL-001")]
    public async Task UnsupportedCapabilitiesBlockBeforeAnyWrites(string from, string to, string code)
    {
        string config = Path.Combine(sandbox, "input.yml");
        await File.WriteAllTextAsync(config, (await File.ReadAllTextAsync(Sample, Token)).Replace(from, to, StringComparison.Ordinal), Token);
        string[] before = Snapshot();
        CliResult result = await CliProcessRunner.RunAsync(sandbox, ["generate", "--config", config, "--output", Output], Token);
        Assert.Equal(5, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.StartsWith(code, result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
    }

    public static TheoryData<string[]> InvalidArguments { get; } = new()
    {
        new[] { "generate" },
        new[] { "generate", "--config", "CONFIG" },
        new[] { "generate", "--output", "out" },
        new[] { "generate", "--config", "CONFIG", "--output" },
        new[] { "generate", "--config", "CONFIG", "--output", "" },
        new[] { "generate", "--config", "CONFIG", "--output", " " },
        new[] { "generate", "--config", "CONFIG", "--output", "bad\0path" },
        new[] { "generate", "--config", "CONFIG", "--output", "--help" },
        new[] { "generate", "--config", "CONFIG", "--config", "CONFIG" },
        new[] { "generate", "--config", "CONFIG", "--output", "out", "--force" },
        new[] { "generate", "--config", "CONFIG", "--output", "out", "--execute" },
        new[] { "generate", "--config", "CONFIG", "--output", "out", "--dry-run" },
        new[] { "generate", "--config", "CONFIG", "--output", "out", "--unknown" },
        new[] { "generate", "--help", "--output", "out" },
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task InvalidArgumentsAreRejectedBeforeReadingOrWriting(string[] args)
    {
        string[] resolved = args.Select(argument => argument == "CONFIG" ? Sample : argument).ToArray();
        // An OS command line cannot transport NUL. Test the entrypoint for the full raw-argument contract.
        using StringWriter stdout = new(CultureInfo.InvariantCulture);
        using StringWriter stderr = new(CultureInfo.InvariantCulture);
        int exitCode = await SiteFactoryCli.RunAsync(resolved, new GenerationComposition(new ModuleCatalog([]), []), stdout, stderr, Token);
        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("Usage: sitefactory generate", stderr.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Fact]
    public async Task HelpAndPreCancellationHaveNoSideEffects()
    {
        CliResult help = await CliProcessRunner.RunAsync(sandbox, ["generate", "--help"], Token);
        Assert.Equal(0, help.ExitCode);
        Assert.Equal(string.Empty, help.StandardError);
        Assert.Contains("Régénération uniquement avec état valide", help.StandardOutput, StringComparison.Ordinal);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter stdout = new(CultureInfo.InvariantCulture);
        using StringWriter stderr = new(CultureInfo.InvariantCulture);
        int code = await SiteFactoryCli.RunAsync(["generate", "--config", Sample, "--output", Output],
            new GenerationComposition(new ModuleCatalog([]), []), stdout, stderr, cancellation.Token);
        Assert.Equal(12, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData("file-collision", 5)]
    [InlineData("path-traversal", 4)]
    [InlineData("git-internal-path", 5)]
    [InlineData("physical-path-limit", 11)]
    public async Task RealCliWithInvalidEmbeddedManifestFailsBeforeAnyWrite(string scenario, int exitCode)
    {
        string manifest = Path.Combine(AppContext.BaseDirectory, "fixtures", "generation-plan", "end-to-end", scenario, "template.json");
        using PublicPlanCliBuild build = new();
        await build.BuildAsync(manifest, Token);
        // A traversal must not overwrite an existing sibling of the proposed output, or create a .git tree.
        await File.WriteAllTextAsync(Path.Combine(sandbox, "outside.txt"), "Outside sentinel; never overwrite.", Token);
        string[] before = Snapshot();
        CliBytesResult result = await CliProcessRunner.RunBytesAsync(sandbox,
            ["generate", "--config", Sample, "--output", Output], Token, build.CliAssemblyPath);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.NotEmpty(result.StandardError);
        if (scenario == "physical-path-limit")
            Assert.Contains("SF-GEN-FILE-001 plannedFiles[3].relativePath ", Encoding.UTF8.GetString(result.StandardError), StringComparison.Ordinal);
        Assert.Equal(before, Snapshot());
        Assert.False(Directory.Exists(Output));
    }

    [Fact]
    public async Task PublicDocumentationGenerationAndRegenerationKeepCanonicalFilesAndTimestamps()
    {
        string config = Path.Combine(sandbox, "documentation.yml");
        string yaml = (await File.ReadAllTextAsync(Sample, Token))
            .Replace("documentation", "documentation", StringComparison.Ordinal)
            .Replace("documentation-minimal", "documentation-minimal", StringComparison.Ordinal);
        await File.WriteAllTextAsync(config, yaml, Token);
        byte[] inputBefore = await File.ReadAllBytesAsync(config, Token);
        string[] arguments = ["generate", "--config", config, "--output", Output];

        CliResult first = await CliProcessRunner.RunAsync(sandbox, arguments, Token);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        string[] canonical = ["guide.html", "index.html", "sitefactory.lock.json"];
        byte[][] content = await Task.WhenAll(canonical.Select(name => File.ReadAllBytesAsync(Path.Combine(Output, name), Token)));
        DateTime[] timestamps = canonical.Select(name => File.GetLastWriteTimeUtc(Path.Combine(Output, name))).ToArray();
        string index = await File.ReadAllTextAsync(Path.Combine(Output, "index.html"), Token);
        Assert.Contains("href=\"guide.html\"", index, StringComparison.Ordinal);
        Assert.Contains("<html", index, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(Output, "src")));
        Assert.False(Directory.Exists(Path.Combine(Output, ".git")));
        using (JsonDocument locked = JsonDocument.Parse(content[2]))
            Assert.Equal("documentation-minimal", locked.RootElement.GetProperty("template").GetProperty("id").GetString());

        CliResult second = await CliProcessRunner.RunAsync(sandbox, arguments, Token);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(first, second);
        for (int i = 0; i < canonical.Length; i++)
        {
            string file = Path.Combine(Output, canonical[i]);
            Assert.Equal(content[i], await File.ReadAllBytesAsync(file, Token));
            Assert.Equal(timestamps[i], File.GetLastWriteTimeUtc(file));
        }
        Assert.Equal(inputBefore, await File.ReadAllBytesAsync(config, Token));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-generate-cli-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }

    private string[] Snapshot() => Directory.EnumerateFileSystemEntries(sandbox, "*", SearchOption.AllDirectories).Prepend(sandbox).Order(StringComparer.Ordinal)
        .Select(path => $"{Path.GetRelativePath(sandbox, path)}|{File.GetLastWriteTimeUtc(path):O}|{(Directory.Exists(path) ? "directory" : Convert.ToBase64String(File.ReadAllBytes(path)))}")
        .ToArray();
}
