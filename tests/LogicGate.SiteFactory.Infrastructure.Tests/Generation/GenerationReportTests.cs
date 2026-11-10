using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GenerationReportTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-report-tests-").FullName;

    private GenerationRoot Root => new(Path.Combine(sandbox, "generation"));

    [Fact]
    public async Task ARealCreationProducesTheGoldenReportWithoutExportingContent()
    {
        GenerationRoot root = Root;
        FileGenerationResult created = await new GeneratedFileApplier().ApplyAsync(root, File(), new("Hello\n"),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Created, created.Status);
        // Pin only the golden's generator-version input, independent of the checkout commit.
        GenerationReport report = new(new("0.1.0-dev"), Execution(created));
        byte[] expected = await System.IO.File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "generation-report", "first-generation.json"), TestContext.Current.CancellationToken);

        byte[] actual = GenerationReportJsonSerializer.Serialize(report);

        Assert.Equal(expected, actual);
        Assert.Equal(actual, GenerationReportJsonSerializer.Serialize(report));
        Assert.Equal((byte)'{', actual[0]);
        Assert.Equal((byte)'}', actual[^2]);
        Assert.Equal((byte)'\n', actual[^1]);
        Assert.DoesNotContain('\r', new UTF8Encoding(false, true).GetString(actual));
        Assert.DoesNotContain("Hello", Encoding.UTF8.GetString(actual), StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes("Hello\n"), await Read(root, "metadata.txt"));
        Assert.Equal([root.Resolve("metadata.txt")], Directory.GetFiles(root.FullPath));
    }

    [Fact]
    public async Task FirstAndSecondGenerationReportActualChangesWithoutChangingBusinessFilesOrTheLock()
    {
        GenerationRoot root = Root;
        GeneratedFileContent content = new("Generated structural content.\n");
        GeneratedFileApplier generated = new();
        ClientFileApplier client = new();
        PlannedFile clientFile = File("readme", "README.md", FileGenerationStrategy.CreateOnce);
        SiteFactoryLock candidate = await LockCandidate();
        SiteFactoryLockWriter lockWriter = new();
        FileGenerationResult[] first =
        [
            await generated.ApplyAsync(root, File(), content, cancellationToken: TestContext.Current.CancellationToken),
            await client.ApplyAsync(root, clientFile, "Initial example.\n", TestContext.Current.CancellationToken),
            await lockWriter.WriteAsync(root, candidate, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken),
        ];
        Assert.All(first, result => Assert.Equal(FileGenerationStatus.Created, result.Status));
        GenerationReport initialReport = GenerationReportWriter.CreateReport(Execution(first));
        GenerationReportWriter writer = new();
        Assert.Equal(FileGenerationStatus.Created, (await writer.WriteAsync(root, initialReport, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken)).Status);
        using JsonDocument firstJson = JsonDocument.Parse(await Read(root, GenerationReportWriter.FileName));
        AssertPaths(firstJson, "createdFiles", "metadata.txt", "README.md", SiteFactoryLockWriter.FileName);
        Assert.Equal(typeof(GenerationReportWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            firstJson.RootElement.GetProperty("siteFactoryVersion").GetString());
        string privateCustomization = "Fictitious personal content: <PRIVATE_CLIENT_CONTENT>\r\n";
        await System.IO.File.WriteAllTextAsync(root.Resolve("README.md"), privateCustomization, TestContext.Current.CancellationToken);
        byte[] lockBytes = await Read(root, SiteFactoryLockWriter.FileName);
        byte[] generatedBytes = await Read(root, "metadata.txt");
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(root.Resolve("metadata.txt"), sentinel);
        System.IO.File.SetLastWriteTimeUtc(root.Resolve(SiteFactoryLockWriter.FileName), sentinel);

        FileGenerationResult[] second =
        [
            await generated.ApplyAsync(root, File(), content, content.Hash, TestContext.Current.CancellationToken),
            await client.ApplyAsync(root, clientFile, "New template example.", TestContext.Current.CancellationToken),
            await lockWriter.WriteAsync(root, candidate, PlannedFileAction.Create, Hash(lockBytes), TestContext.Current.CancellationToken),
        ];
        GenerationReport nextReport = GenerationReportWriter.CreateReport(Execution(second));
        byte[] beforePublication = GenerationReportJsonSerializer.Serialize(nextReport);
        FileGenerationResult publication = await writer.WriteAsync(root, nextReport, PlannedFileAction.Update,
            Hash(GenerationReportJsonSerializer.Serialize(initialReport)), TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Updated, publication.Status);
        Assert.Equal(beforePublication, await Read(root, GenerationReportWriter.FileName));
        using JsonDocument nextJson = JsonDocument.Parse(beforePublication);
        Assert.Equal("success", nextJson.RootElement.GetProperty("status").GetString());
        AssertPaths(nextJson, "createdFiles");
        AssertPaths(nextJson, "modifiedFiles");
        AssertPaths(nextJson, "unchangedFiles", "metadata.txt", SiteFactoryLockWriter.FileName);
        AssertPaths(nextJson, "preservedFiles", "README.md");
        Assert.Equal(3, nextJson.RootElement.GetProperty("operations").GetArrayLength());
        Assert.Equal(PlannedFileAction.Create, nextReport.Operations[0].PlannedAction);
        Assert.Equal(FileGenerationStatus.Unchanged, nextReport.Operations[0].Result);
        Assert.Equal(generatedBytes, await Read(root, "metadata.txt"));
        Assert.Equal(lockBytes, await Read(root, SiteFactoryLockWriter.FileName));
        Assert.Equal(Encoding.UTF8.GetBytes(privateCustomization), await Read(root, "README.md"));
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(root.Resolve("metadata.txt")));
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(root.Resolve(SiteFactoryLockWriter.FileName)));
        Assert.DoesNotContain("PRIVATE_CLIENT_CONTENT", Encoding.UTF8.GetString(beforePublication), StringComparison.Ordinal);
        Assert.Equal(4, Directory.GetFiles(root.FullPath).Length);
        AssertNoTemporary(root);
    }

    [Fact]
    public async Task UpdateIncludesTheTrustedReferenceAndTheActualPreWriteInspection()
    {
        GenerationRoot root = Root;
        GeneratedFileContent first = new("First.\n");
        GeneratedFileContent second = new("Second.\n");
        GeneratedFileApplier applier = new();
        await applier.ApplyAsync(root, File(), first, cancellationToken: TestContext.Current.CancellationToken);
        FileGenerationResult changed = await applier.ApplyAsync(root, File(action: PlannedFileAction.Update), second, first.Hash,
            TestContext.Current.CancellationToken);
        GenerationReport report = GenerationReportWriter.CreateReport(Execution(changed));

        GenerationReportOperation operation = Assert.Single(report.Operations);
        Assert.Equal(FileGenerationStatus.Updated, operation.Result);
        Assert.True(operation.DestinationExistedAtInspection);
        Assert.Equal(first.Hash, operation.PreviousHash);
        Assert.Equal(first.Hash, operation.CurrentHash);
        Assert.Equal(second.Hash, operation.ProposedHash);
        Assert.Equal(second.Hash, Hash(await Read(root, "metadata.txt")));
        using JsonDocument json = JsonDocument.Parse(GenerationReportJsonSerializer.Serialize(report));
        AssertPaths(json, "modifiedFiles", "metadata.txt");
        AssertPaths(json, "createdFiles");
    }

    [Fact]
    public async Task ManualModificationIsReportedAsAConflictNotAsThePlannedUpdate()
    {
        GenerationRoot root = Root;
        GeneratedFileContent first = new("Generated.\n");
        GeneratedFileApplier applier = new();
        await applier.ApplyAsync(root, File(), first, cancellationToken: TestContext.Current.CancellationToken);
        byte[] edited = Encoding.UTF8.GetBytes("<PRIVATE_MANUAL_CONTENT>\r\n");
        await System.IO.File.WriteAllBytesAsync(root.Resolve("metadata.txt"), edited, TestContext.Current.CancellationToken);
        GeneratedFileContent expected = new("New generated content.\n");

        FileGenerationResult conflict = await applier.ApplyAsync(root, File(action: PlannedFileAction.Update), expected, first.Hash,
            TestContext.Current.CancellationToken);
        GenerationReport report = GenerationReportWriter.CreateReport(Execution(conflict));

        Assert.Equal(GenerationExecutionStatus.Conflict, report.Status);
        GenerationReportOperation operation = Assert.Single(report.Operations);
        Assert.Equal(FileGenerationStatus.Conflict, operation.Result);
        Assert.Equal("SF-GEN-FILE-004", operation.ConflictCode);
        Assert.Equal(Hash(edited), operation.CurrentHash);
        Assert.Equal(first.Hash, operation.PreviousHash);
        Assert.Equal(expected.Hash, operation.ProposedHash);
        Assert.Equal(edited, await Read(root, "metadata.txt"));
        using JsonDocument json = JsonDocument.Parse(GenerationReportJsonSerializer.Serialize(report));
        AssertPaths(json, "conflicts", "metadata.txt");
        AssertPaths(json, "modifiedFiles");
        Assert.DoesNotContain("PRIVATE_MANUAL_CONTENT", Encoding.UTF8.GetString(GenerationReportJsonSerializer.Serialize(report)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, FileGenerationStatus.Preserved)]
    [InlineData(FileGenerationStrategy.Custom, FileGenerationStatus.Preserved)]
    [InlineData(FileGenerationStrategy.Protected, FileGenerationStatus.Protected)]
    public async Task PreservedAndProtectedContentIsNotReadOrHashedForTheReport(FileGenerationStrategy strategy, FileGenerationStatus expected)
    {
        GenerationRoot root = Root;
        PlannedFile file = File(strategy: strategy, action: strategy == FileGenerationStrategy.CreateOnce ? PlannedFileAction.Create : PlannedFileAction.Preserve);
        await new AtomicFileWriter().WriteAsync(root, file.RelativePath, "<PRIVATE_CONTENT>", cancellationToken: TestContext.Current.CancellationToken);
        string path = root.Resolve(file.RelativePath);
        UnixFileMode? originalMode = OperatingSystem.IsLinux() ? System.IO.File.GetUnixFileMode(path) : null;
        try
        {
            using FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(path, UnixFileMode.None);
            ClientFileApplier applier = new(new AtomicFileWriter(_ => Assert.Fail("No staging is permitted.")));
            FileGenerationResult preserved = await applier.ApplyAsync(root, file, new string((char)0xD800, 1), TestContext.Current.CancellationToken);
            GenerationReport report = GenerationReportWriter.CreateReport(Execution(preserved));

            GenerationReportOperation operation = Assert.Single(report.Operations);
            Assert.Equal(expected, operation.Result);
            Assert.Equal(strategy == FileGenerationStrategy.Protected ? (bool?)null : true, operation.DestinationExistedAtInspection);
            Assert.Null(operation.CurrentHash);
            Assert.Null(operation.PreviousHash);
            Assert.Null(operation.ProposedHash);
            Assert.DoesNotContain("PRIVATE_CONTENT", Encoding.UTF8.GetString(GenerationReportJsonSerializer.Serialize(report)), StringComparison.Ordinal);
        }
        finally
        {
            if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(path, originalMode!.Value);
        }

        Assert.Equal(Encoding.UTF8.GetBytes("<PRIVATE_CONTENT>"), await Read(root, file.RelativePath));
        AssertNoTemporary(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, FileGenerationStatus.Skipped)]
    [InlineData(FileGenerationStrategy.Custom, FileGenerationStatus.Skipped)]
    [InlineData(FileGenerationStrategy.Protected, FileGenerationStatus.Protected)]
    public async Task AProtectedOrSkippedResultNeverPretendsThatAFileWasPreserved(FileGenerationStrategy strategy, FileGenerationStatus expected)
    {
        GenerationRoot root = Root;
        FileGenerationResult result = await new ClientFileApplier().ApplyAsync(root, File(strategy: strategy, action: PlannedFileAction.Preserve),
            cancellationToken: TestContext.Current.CancellationToken);
        GenerationReport report = GenerationReportWriter.CreateReport(Execution(result));

        Assert.Equal(expected, Assert.Single(report.Operations).Result);
        using JsonDocument json = JsonDocument.Parse(GenerationReportJsonSerializer.Serialize(report));
        AssertPaths(json, "preservedFiles");
        AssertPaths(json, expected == FileGenerationStatus.Skipped ? "skippedFiles" : "protectedFiles", "metadata.txt");
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Merge, "SF-GEN-FILE-006")]
    [InlineData(FileGenerationStrategy.Custom, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Protected, "SF-GEN-FILE-009")]
    public async Task GuardRefusalsReportNoContentObservation(FileGenerationStrategy strategy, string code)
    {
        GenerationRoot root = Root;
        PlannedFile file = File(strategy: strategy);
        FileGenerationResult generated = await new GeneratedFileApplier().ApplyAsync(root, file, new("Ignored candidate."),
            new(new string('A', 64)), TestContext.Current.CancellationToken);
        FileGenerationResult client = await new ClientFileApplier().ApplyAsync(root, file, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(generated, client);
        GenerationReport report = GenerationReportWriter.CreateReport(Execution(generated));
        GenerationReportOperation operation = Assert.Single(report.Operations);
        Assert.Equal(FileGenerationStatus.Conflict, operation.Result);
        Assert.Equal(code, operation.ConflictCode);
        Assert.Null(operation.DestinationExistedAtInspection);
        Assert.Null(operation.PreviousHash);
        Assert.Null(operation.CurrentHash);
        Assert.Null(operation.ProposedHash);
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWriteFailureKeepsEarlierSuccessAndDoesNotClaimTheFailedFileWasCreated(bool clientFile)
    {
        GenerationRoot root = Root;
        FileGenerationResult created = await new GeneratedFileApplier().ApplyAsync(root, File("first", "first.txt"), new("First.\n"),
            cancellationToken: TestContext.Current.CancellationToken);
        AtomicFileWriter failing = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed) throw new IOException("<PRIVATE_PROVIDER_DETAIL> " + sandbox);
        });
        FileGenerationResult failed = clientFile
            ? await new ClientFileApplier(failing).ApplyAsync(root, File(strategy: FileGenerationStrategy.CreateOnce), "Candidate.", TestContext.Current.CancellationToken)
            : await new GeneratedFileApplier(failing).ApplyAsync(root, File(), new("Candidate."), cancellationToken: TestContext.Current.CancellationToken);
        GenerationReport report = GenerationReportWriter.CreateReport(Execution(created, failed));

        Assert.Equal(GenerationExecutionStatus.Failed, report.Status);
        Assert.Equal(FileGenerationStatus.Failed, report.Operations[1].Result);
        Assert.False(report.Operations[1].DestinationExistedAtInspection);
        Assert.NotNull(report.Operations[1].ProposedHash);
        Assert.Equal("SF-GEN-FILE-015", Assert.Single(report.Diagnostics).Code);
        byte[] bytes = GenerationReportJsonSerializer.Serialize(report);
        Assert.DoesNotContain("PRIVATE_PROVIDER_DETAIL", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        using JsonDocument json = JsonDocument.Parse(bytes);
        AssertPaths(json, "createdFiles", "first.txt");
        AssertPaths(json, "failedFiles", "metadata.txt");
        Assert.False(System.IO.File.Exists(root.Resolve("metadata.txt")));
        Assert.Equal(FileGenerationStatus.Created, (await new GenerationReportWriter().WriteAsync(root, report, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken)).Status);
        Assert.Equal(bytes, await Read(root, GenerationReportWriter.FileName));
        AssertNoTemporary(root);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public async Task ReportsAreByteIdenticalAcrossCulturesAndPhysicalRoots(string cultureName)
    {
        GeneratedFileApplier applier = new();
        FileGenerationResult first = await applier.ApplyAsync(Root, File(), new("Same.\n"), cancellationToken: TestContext.Current.CancellationToken);
        byte[] expected = GenerationReportJsonSerializer.Serialize(GenerationReportWriter.CreateReport(Execution(first)));
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            FileGenerationResult second = await applier.ApplyAsync(new(Path.Combine(sandbox, "other-root")), File(), new("Same.\n"),
                cancellationToken: TestContext.Current.CancellationToken);
            byte[] actual = GenerationReportJsonSerializer.Serialize(GenerationReportWriter.CreateReport(Execution(second)));
            Assert.Equal(expected, actual);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void FreeFormIdentifiersCodesAndMessagesAreNotPublished()
    {
        string privateText = sandbox + " <PRIVATE_CREDENTIAL> fictitious-person@example.test";
        PlannedFile file = File(id: privateText);
        GenerationExecutionError error = new(new(privateText), privateText);
        GenerationExecutionResult execution = new(new(privateText),
            [new(file.Id, FileGenerationStatus.Failed, error, new(file))], [new(new("SF-GEN-FILE-015"), privateText)]);
        GenerationReport report = GenerationReportWriter.CreateReport(execution);

        string json = Encoding.UTF8.GetString(GenerationReportJsonSerializer.Serialize(report));

        Assert.DoesNotContain("PRIVATE_CREDENTIAL", json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sitefactory-report-tests-", json, StringComparison.Ordinal);
        Assert.Null(report.Diagnostics[0].Code);
        Assert.Equal("SF-GEN-FILE-015", report.Diagnostics[1].Code);
        Assert.Equal(GenerationExecutionStatus.Failed, report.Status);
        Assert.False(Directory.Exists(Root.FullPath));
    }

    [Fact]
    public async Task ReportReplacementRequiresExplicitUpdateAndTrustedReferenceAndProtectsManualEdits()
    {
        GenerationRoot root = Root;
        GenerationReport first = GenerationReportWriter.CreateReport(Execution());
        GenerationReport second = GenerationReportWriter.CreateReport(new(new("run"), [], [new(new("SF-GEN-FILE-015"), "Not exported.")]));
        GenerationReportWriter writer = new();
        Assert.Equal(FileGenerationStatus.Created, (await writer.WriteAsync(root, first, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken)).Status);
        byte[] original = await Read(root, GenerationReportWriter.FileName);

        FileGenerationResult noReference = await writer.WriteAsync(root, second, PlannedFileAction.Update, cancellationToken: TestContext.Current.CancellationToken);
        FileGenerationResult noAction = await writer.WriteAsync(root, second, PlannedFileAction.Create, Hash(original), TestContext.Current.CancellationToken);

        Assert.Equal("SF-GEN-FILE-014", noReference.Error?.Code.Value);
        Assert.Equal("SF-GEN-FILE-014", noAction.Error?.Code.Value);
        Assert.Equal(original, await Read(root, GenerationReportWriter.FileName));
        await System.IO.File.WriteAllTextAsync(root.Resolve(GenerationReportWriter.FileName), "Manual change.", TestContext.Current.CancellationToken);
        FileGenerationResult manual = await writer.WriteAsync(root, second, PlannedFileAction.Update, Hash(original), TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Conflict, manual.Status);
        Assert.Equal("SF-GEN-FILE-004", manual.Error?.Code.Value);
        Assert.Equal(Encoding.UTF8.GetBytes("Manual change."), await Read(root, GenerationReportWriter.FileName));
        AssertNoTemporary(root);
    }

    [Fact]
    public async Task PublishingTheSameReportTwiceDoesNotStageOrTouchItsDestination()
    {
        GenerationRoot root = Root;
        GenerationReport report = GenerationReportWriter.CreateReport(Execution());
        await new GenerationReportWriter().WriteAsync(root, report, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken);
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(root.Resolve(GenerationReportWriter.FileName), sentinel);
        GenerationReportWriter writer = new(new GeneratedFileApplier(new AtomicFileWriter(stage =>
            Assert.DoesNotContain(stage, new[] { AtomicWriteCheckpoint.TemporaryCreated, AtomicWriteCheckpoint.ChunkWritten, AtomicWriteCheckpoint.ContentFlushed }))));

        FileGenerationResult repeated = await writer.WriteAsync(root, report, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Unchanged, repeated.Status);
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(root.Resolve(GenerationReportWriter.FileName)));
        AssertNoTemporary(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationFailureOrCancellationIsSeparateAndKeepsThePreviousReport(bool cancel)
    {
        GenerationRoot root = Root;
        GenerationReport first = GenerationReportWriter.CreateReport(Execution());
        await new GenerationReportWriter().WriteAsync(root, first, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken);
        byte[] original = await Read(root, GenerationReportWriter.FileName);
        GenerationReport second = GenerationReportWriter.CreateReport(new(new("run"), [], [new(new("SF-GEN-FILE-015"), "Not exported.")]));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        GenerationReportWriter writer = new(new GeneratedFileApplier(new AtomicFileWriter(stage =>
        {
            if (stage != AtomicWriteCheckpoint.ContentFlushed) return;
            if (cancel) cancellation.Cancel();
            else throw new IOException("<PRIVATE_PUBLICATION_DETAIL>");
        })));

        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(root, second, PlannedFileAction.Update, Hash(original), cancellation.Token));
        else
        {
            FileGenerationResult publication = await writer.WriteAsync(root, second, PlannedFileAction.Update, Hash(original), cancellation.Token);
            Assert.Equal(FileGenerationStatus.Failed, publication.Status);
            Assert.Equal("SF-GEN-FILE-015", publication.Error?.Code.Value);
        }

        Assert.Equal(original, await Read(root, GenerationReportWriter.FileName));
        AssertNoTemporary(root);
    }

    [Fact]
    public async Task InvalidVersionAndPreCancellationCannotCreateAReportDirectory()
    {
        GenerationRoot root = Root;
        GenerationReportWriter writer = new();
        GenerationReport fake = new(new("999.0.0"), Execution());
        await Assert.ThrowsAsync<ArgumentException>("report", () => writer.WriteAsync(root, fake, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(root, GenerationReportWriter.CreateReport(Execution()),
            PlannedFileAction.Create, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public void NullSerializationInputIsRejected() =>
        Assert.Throws<ArgumentNullException>("report", () => GenerationReportJsonSerializer.Serialize(null!));

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-report-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }

    private static PlannedFile File(string id = "metadata", string path = "metadata.txt", FileGenerationStrategy strategy = FileGenerationStrategy.Generated,
        PlannedFileAction action = PlannedFileAction.Create) => new(new(id), path, new(PlannedFileOriginKind.Generator, "sitefactory"), strategy, action);

    private static GenerationExecutionResult Execution(params FileGenerationResult[] results) => new(new("run"), results, []);

    private static GeneratedContentHash Hash(byte[] bytes) => new(Convert.ToHexString(SHA256.HashData(bytes)));

    private static Task<byte[]> Read(GenerationRoot root, string path) => System.IO.File.ReadAllBytesAsync(root.Resolve(path), TestContext.Current.CancellationToken);

    private static void AssertPaths(JsonDocument document, string name, params string[] paths) =>
        Assert.Equal(paths, document.RootElement.GetProperty(name).EnumerateArray().Select(static value => value.GetString()));

    private static void AssertNoTemporary(GenerationRoot root) => Assert.Empty(Directory.GetFiles(root.FullPath, ".sitefactory-*.tmp", SearchOption.AllDirectories));

    private static async Task<SiteFactoryLock> LockCandidate()
    {
        ClientConfigurationV1 configuration = new(1, new("acme-services", "Acme Services"),
            new("documentation", "documentation-minimal", "fr-FR", "https://www.example.test"), new([]), new("docker-compose-vps"), new(true, false));
        TemplateResolutionResult resolution = await new TemplateResolver(new DocumentationModule(new TemplateManifestJsonReader())).ResolveAsync(configuration, TestContext.Current.CancellationToken);
        Assert.NotNull(resolution.Template);
        return SiteFactoryLockWriter.CreateCandidate(configuration, resolution.Template);
    }
}
