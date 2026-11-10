using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Tests.Fixtures;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class InitialGenerationExecutorTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-initial-tests-").FullName;
    private string Output => Path.Combine(sandbox, "output");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PreparationDoesNotWriteAndExecutionReportsActualFilesIncludingPublication()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, preparation.Status);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));

        GenerationExecutionResult execution = await preparation.Executor!.ExecuteAsync(request, Token);

        Assert.Equal(GenerationExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(request.Plan.Files.Select(static file => file.Id.Value).Concat(["sitefactory-lock", "generation-report", "generation-state"]),
            execution.Files.Select(static file => file.FileId.Value));
        Assert.All(execution.Files, static file => Assert.Equal(FileGenerationStatus.Created, file.Status));
        Assert.Equal(10, Directory.GetFiles(Output, "*", SearchOption.AllDirectories).Length);
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(Output, GenerationReportWriter.FileName), Token));
        Assert.Equal("success", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(request.Plan.Files.Select(static file => file.RelativePath).Append("sitefactory.lock.json"), report.RootElement.GetProperty("createdFiles")
            .EnumerateArray().Select(static path => path.GetString()));
        Assert.False(Directory.Exists(Path.Combine(Output, ".git")));
        Assert.True(File.Exists(Path.Combine(Output, ".sitefactory", "generation-state.json")));
    }

    [Theory]
    [InlineData("sitefactory.lock.json")]
    [InlineData("SITEFACTORY.LOCK.JSON")]
    [InlineData("sitefactory.lock.json/child.txt")]
    [InlineData("generation-report.json")]
    [InlineData("Generation-Report.json/child.txt")]
    [InlineData(".sitefactory/generation-state.json")]
    [InlineData(".SITEFACTORY/other.txt")]
    [InlineData(".sitefactory")]
    public async Task ArtifactCollisionsAreDetectedBeforeAnyWrite(string collision)
    {
        (ClientConfigurationV1 config, TemplateManifest original, _) = await Inputs();
        TemplateManifest template = WithFiles(original, [.. original.Files, new(new("collision"), collision, "structure/readme", FileGenerationStrategy.Generated)]);
        GenerationExecutionRequest request = Request(config, template);

        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);

        Assert.Equal(InitialGenerationPreparationStatus.InvalidPlan, result.Status);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Null(result.Executor);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData("sitefactory-lock")]
    [InlineData("generation-report")]
    [InlineData("generation-state")]
    public async Task ReservedArtifactIdentitiesCannotFailOnlyAfterWriting(string id)
    {
        (ClientConfigurationV1 config, TemplateManifest original, _) = await Inputs();
        TemplateManifest template = WithFiles(original, [.. original.Files, new(new(id), "extra.txt", "structure/readme", FileGenerationStrategy.Generated)]);
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(Request(config, template), config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidPlan, result.Status);
        Assert.Equal("SF-GEN-FILE-003", Assert.Single(result.Diagnostics).Code.Value);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData("ascii")]
    [InlineData("unicode")]
    [InlineData("full-path")]
    public async Task PhysicallyInvalidLastDestinationCannotLeaveEarlierFilesOrPublicationArtifacts(string scenario)
    {
        (ClientConfigurationV1 config, TemplateManifest original, _) = await Inputs();
        string invalidPath = "zz-last/deep/" + (scenario switch
        {
            "unicode" => new string('é', 128),
            "full-path" => string.Join('/', Enumerable.Repeat(new string('d', 200), 21)) + "/file.txt",
            _ => new string('x', 256),
        });
        TemplateManifest template = WithFiles(original,
            [.. original.Files, new(new("last-invalid"), invalidPath, "structure/readme", FileGenerationStrategy.Generated)]);
        GenerationExecutionRequest request = Request(config, template);
        Assert.True(request.Plan.Files.Count > 3);
        Assert.Equal(invalidPath, request.Plan.Files[^1].RelativePath);
        Assert.Empty(GenerationPlanPathValidator.Validate(request.Plan.Files));

        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        // Follow the real caller's contract: only a successful preparation exposes an executor.
        if (result.Executor is { } executor)
            await executor.ExecuteAsync(request, Token);

        // This includes directories, content, lock, report, state and any temporary file.
        Assert.Empty(Directory.EnumerateFileSystemEntries(sandbox, "*", SearchOption.AllDirectories));
        Assert.Equal(InitialGenerationPreparationStatus.InvalidRoot, result.Status);
        Assert.Null(result.Executor);
        Assert.Equal("SF-GEN-FILE-001", Assert.Single(result.Diagnostics).Code.Value);
    }

    [Fact]
    public async Task AllInvalidDestinationsAreReportedInPlanOrderWithoutExposingPhysicalPaths()
    {
        (ClientConfigurationV1 config, TemplateManifest original, _) = await Inputs();
        TemplateManifest template = WithFiles(original, [.. original.Files,
            new(new("invalid-first"), "zy/" + new string('x', 256), "structure/readme", FileGenerationStrategy.Generated),
            new(new("invalid-second"), "zz/deep/" + new string('é', 128) + "/file.txt", "structure/readme", FileGenerationStrategy.Generated)]);
        GenerationExecutionRequest request = Request(config, template);

        InitialGenerationPreparation first = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        InitialGenerationPreparation second = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);

        Assert.Equal(InitialGenerationPreparationStatus.InvalidRoot, first.Status);
        Assert.Null(first.Executor);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(2, first.Diagnostics.Count);
        Assert.Equal(new[]
        {
            FormattableString.Invariant($"plannedFiles[{request.Plan.Files.Count - 2}].relativePath"),
            FormattableString.Invariant($"plannedFiles[{request.Plan.Files.Count - 1}].relativePath"),
        }, first.Diagnostics.Select(static diagnostic => diagnostic.PropertyPath));
        Assert.All(first.Diagnostics, diagnostic =>
        {
            Assert.Equal("SF-GEN-FILE-001", diagnostic.Code.Value);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.DoesNotContain(sandbox, diagnostic.Message, StringComparison.Ordinal);
        });
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComponentAtPortableLimitCanActuallyBeGenerated(bool unicode)
    {
        (ClientConfigurationV1 config, TemplateManifest original, _) = await Inputs();
        string component = unicode ? new string('é', 127) + "a" : new string('x', 255);
        string relativePath = "zz-last/" + component + "/" + component;
        TemplateManifest template = WithFiles(original,
            [.. original.Files, new(new("at-limit"), relativePath, "structure/readme", FileGenerationStrategy.Generated)]);
        GenerationExecutionRequest request = Request(config, template);

        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, preparation.Status);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));

        GenerationExecutionResult result = await preparation.Executor!.ExecuteAsync(request, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(FileGenerationStatus.Created, Assert.Single(result.Files, static file => file.FileId.Value == "at-limit").Status);
        Assert.True(File.Exists(Path.Combine(Output, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Empty(Directory.GetFiles(Output, ".sitefactory-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PhysicalRootBudgetAlsoPreflightsEveryReservedPublication()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        string parent = sandbox;
        while (4095 - Encoding.UTF8.GetByteCount(parent) - 1 > 255)
            parent = Path.Combine(parent, new string('r', 200));
        int remaining = 4095 - Encoding.UTF8.GetByteCount(parent) - 1;
        Assert.InRange(remaining, 1, 255);
        string longRoot = Path.Combine(parent, new string('r', remaining));
        Assert.Equal(longRoot, new GenerationRoot(longRoot).FullPath); // The root itself fits; no child can fit.

        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, longRoot, new PublicGenerationFixture(), Token);

        Assert.Equal(InitialGenerationPreparationStatus.InvalidRoot, result.Status);
        Assert.Null(result.Executor);
        Assert.Equal(request.Plan.Files.Count + 3, result.Diagnostics.Count);
        Assert.Equal(new[] { SiteFactoryLockWriter.FileName, GenerationReportWriter.FileName, ".sitefactory/generation-state.json" },
            result.Diagnostics.TakeLast(3).Select(static diagnostic => diagnostic.PropertyPath));
        Assert.All(result.Diagnostics, static diagnostic => Assert.Equal("SF-GEN-FILE-001", diagnostic.Code.Value));
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownSourceOrVersionFailsBeforeEarlierFilesAreWritten(bool unknownVersion)
    {
        (ClientConfigurationV1 config, TemplateManifest original, _) = await Inputs();
        TemplateManifest template = unknownVersion
            ? new(original.TemplateId, new("9.9.9"), original.SupportedProductIds, original.Files)
            : WithFiles(original, [.. original.Files, new(new("last"), "zz-last.txt", "unavailable/source", FileGenerationStrategy.Generated)]);
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(Request(config, template), config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidTemplate, result.Status);
        Assert.Equal("SF-GEN-FILE-005", Assert.Single(result.Diagnostics).Code.Value);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData("modules", "SF-GEN-INITIAL-001")]
    public async Task UnsupportedRequestedCapabilitiesAreNotSilentlyIgnored(string scenario, string code)
    {
        (ClientConfigurationV1 original, TemplateManifest template, _) = await Inputs();
        ClientConfigurationV1 config = new(original.SchemaVersion, original.Client, original.Site,
            new(scenario == "modules" ? ["test-module"] : []), original.Deployment,
            new(true, false));
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(Request(config, template), config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidPlan, result.Status);
        Assert.Equal(code, Assert.Single(result.Diagnostics).Code.Value);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingRootIncludingEmptyIsRefused(bool populated)
    {
        Directory.CreateDirectory(Output);
        if (populated) await File.WriteAllTextAsync(Path.Combine(Output, ".sentinel"), "Client content", Token);
        string[] before = Snapshot();
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, result.Status);
        Assert.Equal("SF-GEN-INITIAL-003", Assert.Single(result.Diagnostics).Code.Value);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task NewRootBetweenPreparationAndExecutionIsRefusedWithoutAnyWrite()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        Directory.CreateDirectory(Output);
        await File.WriteAllTextAsync(Path.Combine(Output, "README.md"), "Client content", Token);
        string[] before = Snapshot();
        GenerationExecutionResult result = await ready.Executor!.ExecuteAsync(request, Token);
        Assert.Equal(GenerationExecutionStatus.Conflict, result.Status);
        Assert.Equal("SF-GEN-FILE-013", Assert.Single(result.Files).Error?.Code.Value);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task ExecutorCannotApplyADifferentRequestOrReuseItsPreparedRoot()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        await Assert.ThrowsAsync<ArgumentException>(() => ready.Executor!.ExecuteAsync(new(request.Plan, "another-client"), Token));
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
        await ready.Executor!.ExecuteAsync(request, Token);
        string[] before = Snapshot();
        Assert.Equal(GenerationExecutionStatus.Conflict, (await ready.Executor.ExecuteAsync(request, Token)).Status);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task ChangedPlanCannotBePreparedAgainstTheOriginalManifest()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest original) = await Inputs();
        PlannedFile first = original.Plan.Files[0];
        GenerationPlan changed = new(original.Plan.Id, original.Plan.ClientSlug, original.Plan.ProductId, original.Plan.TemplateId,
            [new(first.Id, "other.txt", first.Origin, first.Strategy, first.Action), .. original.Plan.Files.Skip(1)], []);
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(new(changed, changed.ClientSlug.Value), config, template, Output, new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidPlan, result.Status);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData(".git/child")]
    [InlineData("foo/.GiT/child")]
    [InlineData("safe/../escape")]
    [InlineData("CON/child")]
    public async Task UnsafePhysicalRootIsRejectedBeforeNormalization(string relative)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template,
            Path.Combine(sandbox, relative.Replace('/', Path.DirectorySeparatorChar)), new PublicGenerationFixture(), Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidRoot, result.Status);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Fact]
    public async Task ExistingAncestorLinkIsNotFollowedDuringPreflight()
    {
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        string link = Path.Combine(sandbox, "link");
        DirectoryLinkFixture.Create(link, outside);
        try
        {
            (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
            InitialGenerationPreparation result = await InitialGenerationExecutor.PrepareAsync(request, config, template, Path.Combine(link, "new-root"), new PublicGenerationFixture(), Token);
            Assert.Equal(InitialGenerationPreparationStatus.InvalidRoot, result.Status);
            Assert.Empty(Directory.GetFileSystemEntries(outside));
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData("content")]
    [InlineData("lock")]
    [InlineData("report")]
    public async Task FailureIsReportedFromActualOperationsIncludingLockAndReportFailures(string stageToFail)
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        int failingWrite = stageToFail switch { "content" => 2, "lock" => template.Files.Count + 1, _ => template.Files.Count + 2 };
        int writes = 0;
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed && ++writes == failingWrite)
                throw new IOException("<PRIVATE_PROVIDER_DETAIL>");
        });
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), writer, Token);

        GenerationExecutionResult result = await ready.Executor!.ExecuteAsync(request, Token);

        Assert.Equal(GenerationExecutionStatus.Failed, result.Status);
        Assert.Contains(result.Files, static file => file.Status == FileGenerationStatus.Failed);
        Assert.Empty(Directory.GetFiles(Output, ".sitefactory-*.tmp", SearchOption.AllDirectories));
        string reportPath = Path.Combine(Output, GenerationReportWriter.FileName);
        if (stageToFail == "report")
            Assert.False(File.Exists(reportPath));
        else
        {
            string json = await File.ReadAllTextAsync(reportPath, Token);
            using JsonDocument report = JsonDocument.Parse(json);
            Assert.Equal("failed", report.RootElement.GetProperty("status").GetString());
            Assert.DoesNotContain("PRIVATE_PROVIDER_DETAIL", json, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox, json, StringComparison.Ordinal);
            Assert.Equal(failingWrite, report.RootElement.GetProperty("operations").GetArrayLength());
        }
    }

    [Fact]
    public async Task CancellationBeforeExecutionWritesNothingAndDuringWritingNeverLeavesAPartialDestination()
    {
        (ClientConfigurationV1 config, TemplateManifest template, GenerationExecutionRequest request) = await Inputs();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), cancelled.Token));
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready.Executor!.ExecuteAsync(request, cancelled.Token));
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));

        using CancellationTokenSource duringWrite = CancellationTokenSource.CreateLinkedTokenSource(Token);
        AtomicFileWriter writer = new(stage => { if (stage == AtomicWriteCheckpoint.ContentFlushed) duringWrite.Cancel(); });
        ready = await InitialGenerationExecutor.PrepareAsync(request, config, template, Output, new PublicGenerationFixture(), writer, duringWrite.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready.Executor!.ExecuteAsync(request, duringWrite.Token));
        Assert.Empty(Directory.GetFiles(Output, "*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-initial-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }

    private string[] Snapshot() => Directory.EnumerateFileSystemEntries(sandbox, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
        .Select(path => $"{Path.GetRelativePath(sandbox, path)}|{File.GetLastWriteTimeUtc(path):O}|{(Directory.Exists(path) ? "directory" : Convert.ToBase64String(File.ReadAllBytes(path)))}")
        .ToArray();

    private static TemplateManifest WithFiles(TemplateManifest template, IEnumerable<TemplateFile> files) =>
        new(template.TemplateId, template.TemplateVersion, template.SupportedProductIds, files);

    private static GenerationExecutionRequest Request(ClientConfigurationV1 config, TemplateManifest template) =>
        new(GenerationPlanBuilder.Build(new("initial-plan"), config, template), config.Client.Slug!);

    private static async Task<(ClientConfigurationV1, TemplateManifest, GenerationExecutionRequest)> Inputs()
    {
        ClientConfigurationV1 config = new(1, new("public-fixture", "Public Fixture"),
            new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"), new([]), new("docker-compose-vps"), new(true, false));
        TemplateResolutionResult result = await new TemplateResolver(new PublicGenerationFixture()).ResolveAsync(config, Token);
        TemplateManifest template = Assert.IsType<TemplateManifest>(result.Template);
        return (config, template, Request(config, template));
    }
}
