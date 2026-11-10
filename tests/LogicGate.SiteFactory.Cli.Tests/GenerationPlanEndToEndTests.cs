using System.Text;
using System.Text.Json;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

[Trait("Category", "EndToEnd")]
public sealed class GenerationPlanEndToEndTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("sitefactory-plan-e2e-").FullName;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealCommandMatchesIndependentGoldenBytesTwiceWithoutGeneratingFiles(bool documentedSample)
    {
        string source = documentedSample
            ? Path.Combine(AppContext.BaseDirectory, "samples", "configuration", "valid-minimal", "client.sitefactory.yml")
            : Fixture("valid-minimal", "client.sitefactory.yml");
        string path = PrepareWorkspace(source);
        byte[] golden = await File.ReadAllBytesAsync(Fixture("valid-minimal", "expected-plan.json"), TestContext.Current.CancellationToken);

        CliBytesResult result = await AssertStableReadOnlyCommandAsync(path, expectedExitCode: 0);

        // The oracle is a reviewed fixture, not a second invocation of the planner or serializer.
        Assert.Equal(golden, result.StandardOutput);
        Assert.Empty(result.StandardError);
        using JsonDocument json = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(["guide.html", "index.html"], json.RootElement.GetProperty("plannedFiles")
            .EnumerateArray().Select(static file => file.GetProperty("relativePath").GetString()));
        Assert.Empty(json.RootElement.GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public async Task RealReorderedEmbeddedManifestMatchesTheSameIndependentGolden()
    {
        string path = PrepareWorkspace(Fixture("valid-minimal", "client.sitefactory.yml"));
        using PublicPlanCliBuild build = new();
        await build.BuildAsync(Fixture("reordered-template", "template.json"), TestContext.Current.CancellationToken);
        byte[] golden = await File.ReadAllBytesAsync(Fixture("valid-minimal", "expected-plan.json"), TestContext.Current.CancellationToken);

        CliBytesResult result = await AssertStableReadOnlyCommandAsync(path, 0, build.CliAssemblyPath);

        Assert.Equal(golden, result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Theory]
    [InlineData("invalid-yaml", 3, "SF-CONFIG-YAML-004 - La syntaxe du document YAML est invalide.\n")]
    [InlineData("invalid-configuration", 3,
        "SF-CONFIG-SEMANTIC-001 client.slug Le slug doit contenir de 3 à 80 caractères ASCII en kebab-case, sans nom réservé.\n")]
    [InlineData("unknown-template", 4, "SF-TEMPLATE-001 site.template Le template demandé est absent du catalogue.\n")]
    public async Task RealConfigurationFailuresNeverProduceAPartialPlan(string scenario, int exitCode, string expectedError)
    {
        string path = PrepareWorkspace(Fixture(scenario, "client.sitefactory.yml"));

        CliBytesResult result = await AssertStableReadOnlyCommandAsync(path, expectedExitCode: exitCode);

        Assert.Empty(result.StandardOutput);
        Assert.Equal(expectedError, DecodeDiagnostics(result.StandardError));
    }

    [Theory]
    [InlineData("file-collision", 5, "SF-GEN-FILE-012 README.md Des chemins logiques ne se distinguent que par la casse.\n")]
    [InlineData("path-traversal", 4, "SF-TEMPLATE-003 templates Le catalogue contient un manifest de template invalide.\n")]
    [InlineData("git-internal-path", 5,
        "SF-GEN-FILE-026 .git/config Un segment du chemin cible est réservé aux données internes Git.\n" +
        "SF-GEN-FILE-026 foo/.Git/hooks/pre-commit Un segment du chemin cible est réservé aux données internes Git.\n")]
    public async Task RealEmbeddedManifestFailuresAreRejectedByTheFullCli(string scenario, int exitCode, string expectedError)
    {
        string path = PrepareWorkspace(Fixture("valid-minimal", "client.sitefactory.yml"));
        string fixture = Fixture(scenario, "template.json");
        byte[] fixtureBefore = await File.ReadAllBytesAsync(fixture, TestContext.Current.CancellationToken);
        string productionManifest = Path.Combine(PublicPlanCliBuild.FindRepositoryRoot(), "templates", "documentation-minimal", "template.json");
        byte[] productionBefore = await File.ReadAllBytesAsync(productionManifest, TestContext.Current.CancellationToken);
        using PublicPlanCliBuild build = new();
        await build.BuildAsync(fixture, TestContext.Current.CancellationToken);

        CliBytesResult result = await AssertStableReadOnlyCommandAsync(path, exitCode, build.CliAssemblyPath);

        Assert.Empty(result.StandardOutput);
        Assert.Equal(expectedError, DecodeDiagnostics(result.StandardError));
        Assert.Equal(fixtureBefore, await File.ReadAllBytesAsync(fixture, TestContext.Current.CancellationToken));
        Assert.Equal(productionBefore, await File.ReadAllBytesAsync(productionManifest, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories),
            static entry => Path.GetFileName(entry).Equals(".git", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string PrepareWorkspace(string source)
    {
        string input = Directory.CreateDirectory(Path.Combine(directory, "input")).FullName;
        Directory.CreateDirectory(Path.Combine(directory, "working"));
        string path = Path.Combine(input, "configuration démo.sitefactory.yml");
        File.Copy(source, path);
        // These are test sentinels, never generated client content; traversal must not touch the sibling.
        File.WriteAllText(Path.Combine(directory, "outside.txt"), "Fictitious outside sentinel.", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(input, "README.md"), "Fictitious existing sentinel.", new UTF8Encoding(false));
        return path;
    }

    private async Task<CliBytesResult> AssertStableReadOnlyCommandAsync(string path, int expectedExitCode, string? cliAssemblyPath = null)
    {
        string workingDirectory = Path.Combine(directory, "working");
        FileSystemEntry[] before = Snapshot();
        CliBytesResult first = await CliProcessRunner.RunBytesAsync(workingDirectory,
            ["plan", "--config", Path.GetRelativePath(workingDirectory, path)], TestContext.Current.CancellationToken, cliAssemblyPath);
        Assert.Equal(before, Snapshot());
        CliBytesResult second = await CliProcessRunner.RunBytesAsync(workingDirectory,
            ["plan", "--config", path], TestContext.Current.CancellationToken, cliAssemblyPath);
        Assert.Equal(before, Snapshot());

        Assert.Equal(expectedExitCode, first.ExitCode);
        Assert.Equal(expectedExitCode, second.ExitCode);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(first.StandardError, second.StandardError);
        Assert.DoesNotContain(directory, Encoding.UTF8.GetString(first.StandardOutput), StringComparison.Ordinal);
        Assert.DoesNotContain(directory, Encoding.UTF8.GetString(first.StandardError), StringComparison.Ordinal);
        return first;
    }

    private FileSystemEntry[] Snapshot() => Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
        .Prepend(directory)
        .Order(StringComparer.Ordinal)
        .Select(path => new FileSystemEntry(Path.GetRelativePath(directory, path), File.GetAttributes(path),
            File.GetLastWriteTimeUtc(path), Directory.Exists(path) ? null : Convert.ToBase64String(File.ReadAllBytes(path))))
        .ToArray();

    private static string Fixture(string scenario, string file) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "generation-plan", "end-to-end", scenario, file);

    // Only diagnostics follow platform newlines; stdout comparisons above always use the raw bytes.
    private static string DecodeDiagnostics(byte[] bytes) =>
        new UTF8Encoding(false, true).GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record FileSystemEntry(string Path, FileAttributes Attributes, DateTime LastWriteTimeUtc, string? Content);
}
