using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Tests.Fixtures;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class RegenerationTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-regeneration-").FullName;
    private string Output => Path.Combine(sandbox, "client");
    private string StatePath => Path.Combine(Output, ".sitefactory", "generation-state.json");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SameCompositionPreservesClientBytesAndOnlyPublishesChangedOperationalDocuments()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        string readme = Path.Combine(Output, "README.md");
        byte[] customized = Encoding.UTF8.GetBytes("Client customization\r\nÉté\r\n");
        await File.WriteAllBytesAsync(readme, customized, Token);
        string[] canonical = Snapshot(includeControl: false);
        int writes = 0;
        AtomicFileWriter writer = new(stage => { if (stage == AtomicWriteCheckpoint.TemporaryCreated) writes++; });
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), writer, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, ready.Status);

        GenerationExecutionResult second = await ready.Executor!.ExecuteAsync(request, Token);

        Assert.Equal(GenerationExecutionStatus.Succeeded, second.Status);
        Assert.Equal(2, writes); // Report changed from created to unchanged/preserved; state records its new hash.
        Assert.Equal(canonical, Snapshot(includeControl: false));
        Assert.Equal(customized, await File.ReadAllBytesAsync(readme, Token));
        Assert.Equal(3, second.Files.Count(static file => file.Status == FileGenerationStatus.Preserved));
        Assert.Equal(5, second.Files.Count(static file => file.Status == FileGenerationStatus.Unchanged));
        Assert.Equal(2, second.Files.Count(static file => file.Status == FileGenerationStatus.Updated));
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(Output, "generation-report.json"), Token));
        Assert.Contains("README.md", report.RootElement.GetProperty("preservedFiles").EnumerateArray().Select(static value => value.GetString()));
        Assert.Empty(report.RootElement.GetProperty("modifiedFiles").EnumerateArray());

        string[] before = Snapshot();
        writes = 0;
        ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), writer, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, (await ready.Executor!.ExecuteAsync(request, Token)).Status);
        Assert.Equal(0, writes);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task AllDetectableConflictsAreReportedBeforeRecreatingAnEarlierMissingFile()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        File.Delete(Path.Combine(Output, "Client.slnx"));
        await File.AppendAllTextAsync(Path.Combine(Output, "Directory.Build.props"), "local change", Token);
        await File.AppendAllTextAsync(Path.Combine(Output, "tests", "Client.Tests", "packages.lock.json"), "local change", Token);
        string[] before = Snapshot();

        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);

        Assert.Equal(InitialGenerationPreparationStatus.Conflict, result.Status);
        Assert.Equal(["Client.slnx", "Directory.Build.props", "tests/Client.Tests/packages.lock.json"], result.Diagnostics.Select(static item => item.PropertyPath));
        Assert.All(result.Diagnostics, static item => Assert.Equal("SF-GEN-FILE-004", item.Code.Value));
        Assert.Null(result.Executor);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("unicode")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("schema")]
    [InlineData("template-version")]
    [InlineData("traversal")]
    [InlineData("hash")]
    [InlineData("missing-entry")]
    [InlineData("report-reference")]
    [InlineData("too-large")]
    public async Task InvalidStateCannotAuthorizeWrites(string scenario)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        string original = await File.ReadAllTextAsync(StatePath, Token);
        using JsonDocument state = JsonDocument.Parse(original);
        string generatedHash = state.RootElement.GetProperty("files").EnumerateArray()
            .First(static entry => entry.GetProperty("strategy").GetString() == "generated").GetProperty("lastGeneratedSha256").GetString()!;
        string changed = scenario switch
        {
            "invalid-json" => "{",
            "unicode" => original.Replace(state.RootElement.GetProperty("reportSha256").GetString()!, "\\uD800", StringComparison.Ordinal),
            "duplicate" => original.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,", StringComparison.Ordinal),
            "unknown" => original.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1,\n  \"unknown\": true,", StringComparison.Ordinal),
            "schema" => original.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal),
            "template-version" => original.Replace("1.0.0", "9.9.9", StringComparison.Ordinal),
            "traversal" => original.Replace("Client.slnx", "../outside.txt", StringComparison.Ordinal),
            "hash" => original.Replace(generatedHash, new string('0', 64), StringComparison.Ordinal),
            "missing-entry" => original.Replace("\"files\": [", "\"files\": [], \"removed\": [", StringComparison.Ordinal),
            "report-reference" => original.Replace(state.RootElement.GetProperty("reportSha256").GetString()!, new string('0', 64), StringComparison.Ordinal),
            _ => new string(' ', GenerationState.MaximumLength + 1),
        };
        await File.WriteAllTextAsync(StatePath, changed, Token);
        string[] before = Snapshot();

        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);

        Assert.Equal(InitialGenerationPreparationStatus.Conflict, result.Status);
        Assert.Equal(scenario is "report-reference" or "hash" ? "SF-GEN-FILE-004" : "SF-GEN-STATE-001",
            Assert.Single(result.Diagnostics).Code.Value);
        Assert.Null(result.Executor);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData("generation-report.json", false)]
    [InlineData("generation-report.json", true)]
    [InlineData("sitefactory.lock.json", false)]
    [InlineData("sitefactory.lock.json", true)]
    public async Task ModifiedOrMissingControlArtifactIsNotReplaced(string path, bool missing)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        if (missing) File.Delete(Path.Combine(Output, path));
        else await File.AppendAllTextAsync(Path.Combine(Output, path), "local edit", Token);
        string[] before = Snapshot();
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, result.Status);
        Assert.Equal(path, Assert.Single(result.Diagnostics).PropertyPath);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task DifferentConfigurationIsNotAnImplicitUpgradeEvenWhenRenderedContentWouldBeIdentical()
    {
        (ClientConfigurationV1 original, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        ClientConfigurationV1 changed = new(original.SchemaVersion, new(original.Client.Slug, "Another client name"),
            original.Site, original.Modules, original.Deployment, original.Git);
        string[] before = Snapshot();
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, changed, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, result.Status);
        Assert.Equal("SF-GEN-STATE-001", Assert.Single(result.Diagnostics).Code.Value);
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData(".sitefactory/generation-state.json")]
    [InlineData("src/Client.Web/Program.cs")]
    public async Task LateMutationIsRefusedBeforeAnyApplication(string path)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        await File.AppendAllTextAsync(Path.Combine(Output, path), "late mutation", Token);
        string[] before = Snapshot();
        GenerationExecutionResult result = await ready.Executor!.ExecuteAsync(request, Token);
        Assert.Equal(GenerationExecutionStatus.Conflict, result.Status);
        Assert.Equal("SF-GEN-FILE-013", Assert.Single(result.Files).Error?.Code.Value);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task MissingOwnedAndCreateOnceFilesAreNotImplicitlyRepairedOrAdopted()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        string[] paths = ["Client.slnx", "README.md"];
        foreach (string path in paths) File.Delete(Path.Combine(Output, path));
        string[] before = Snapshot();
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, ready.Status);
        Assert.Null(ready.Executor);
        Assert.Equal(paths, ready.Diagnostics.Select(static diagnostic => diagnostic.PropertyPath));
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData(".sitefactory")]
    [InlineData("src/Client.Web")]
    public async Task ExistingDirectoryLinkIsNotReadOrWrittenDuringRegeneration(string path)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        string original = Path.Combine(Output, path);
        string parked = Path.Combine(sandbox, "parked");
        Directory.Move(original, parked);
        DirectoryLinkFixture.Create(original, parked);
        byte[] sentinel = await File.ReadAllBytesAsync(Path.Combine(parked, path == ".sitefactory" ? "generation-state.json" : "Program.cs"), Token);
        try
        {
            InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
            Assert.Equal(InitialGenerationPreparationStatus.Conflict, result.Status);
            Assert.Null(result.Executor);
            Assert.Equal(sentinel, await File.ReadAllBytesAsync(Path.Combine(parked, path == ".sitefactory" ? "generation-state.json" : "Program.cs"), Token));
            Assert.Empty(Directory.GetFiles(parked, ".sitefactory-*.tmp", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(original); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PublicationFailureNeverAdvancesOwnershipOrReportsSuccess(int failedPublication)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await CreateAsync();
        byte[] stateBefore = await File.ReadAllBytesAsync(StatePath, Token);
        string[] canonical = Snapshot(includeControl: false);
        int writes = 0;
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed && ++writes == failedPublication) throw new IOException("private provider detail");
        });
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), writer, Token);
        GenerationExecutionResult result = await ready.Executor!.ExecuteAsync(request, Token);
        Assert.Equal(GenerationExecutionStatus.Failed, result.Status);
        Assert.Equal(stateBefore, await File.ReadAllBytesAsync(StatePath, Token));
        Assert.Equal(canonical, Snapshot(includeControl: false));
        Assert.Empty(Directory.GetFiles(Output, ".sitefactory-*.tmp", SearchOption.AllDirectories));
        InitialGenerationPreparation retry = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(failedPublication == 1 ? InitialGenerationPreparationStatus.Ready : InitialGenerationPreparationStatus.Conflict, retry.Status);
    }

    private async Task<(ClientConfigurationV1, TemplateManifest, GenerationExecutionRequest)> CreateAsync()
    {
        ClientConfigurationV1 config = new(1, new("public-fixture", "Public Fixture"),
            new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"), new([]), new("docker-compose-vps"), new(true, false));
        TemplateResolutionResult resolution = await new TemplateResolver(new PublicGenerationFixture()).ResolveAsync(config, Token);
        TemplateManifest template = Assert.IsType<TemplateManifest>(resolution.Template);
        GenerationExecutionRequest request = new(GenerationPlanBuilder.Build(new("regeneration-test"), config, template), config.Client.Slug);
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, (await ready.Executor!.ExecuteAsync(request, Token)).Status);
        return (config, template, request);
    }

    private string[] Snapshot(bool includeControl = true) => Directory.GetFiles(Output, "*", SearchOption.AllDirectories)
        .Where(path => includeControl || (path != StatePath && path != Path.Combine(Output, "generation-report.json")))
        .Order(StringComparer.Ordinal).Select(path => $"{Path.GetRelativePath(Output, path)}|{File.GetLastWriteTimeUtc(path):O}|{Convert.ToBase64String(File.ReadAllBytes(path))}").ToArray();

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-regeneration-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }
}
