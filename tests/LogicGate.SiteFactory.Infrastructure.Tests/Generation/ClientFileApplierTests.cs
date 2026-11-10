using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class ClientFileApplierTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-client-tests-").FullName;

    private GenerationRoot Root => new(Path.Combine(sandbox, "generation"));

    [Fact]
    public async Task CreatesOnceWithCanonicalUtf8AndNoSidecars()
    {
        GenerationRoot root = Root;
        PlannedFile file = File();
        FileGenerationResult result = await new ClientFileApplier().ApplyAsync(root, file, "Équipe 東京 🧪\r\nSecond\rLast\n",
            TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Created, result.Status);
        Assert.Equal(file.Id, result.FileId);
        Assert.Null(result.Error);
        Assert.Equal(Encoding.UTF8.GetBytes("Équipe 東京 🧪\nSecond\nLast\n"), await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Create, FileGenerationStatus.Preserved)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Preserve, FileGenerationStatus.Preserved)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Preserve, FileGenerationStatus.Protected)]
    public async Task RegenerationKeepsManualChangesByteForByteWithoutReplacingOrTouchingTheFile(
        FileGenerationStrategy strategy, PlannedFileAction action, FileGenerationStatus expected)
    {
        GenerationRoot root = Root;
        // Initial examples belong to create-once, never to custom/protected (ADR-0005).
        FileGenerationResult first = await new ClientFileApplier().ApplyAsync(root, File(), "Initial example.\n",
            TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Created, first.Status);
        byte[] edited = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Customization client.\r\nNo normalization.\r\n")];
        string path = root.Resolve(File().RelativePath);
        await System.IO.File.WriteAllBytesAsync(path, edited, TestContext.Current.CancellationToken);
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(path, sentinel);
        using AtomicWriteSession inspection = AtomicWriteSession.Open(root, File().RelativePath, TestContext.Current.CancellationToken, createDirectories: false);
        using AtomicFileSnapshot? before = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        ClientFileApplier applier = NoWriter();

        for (int iteration = 0; iteration < 2; iteration++)
        {
            FileGenerationResult result = await applier.ApplyAsync(root, File(strategy, action), "New template content.\n",
                TestContext.Current.CancellationToken);
            Assert.Equal(expected, result.Status);
            Assert.Null(result.Error);
        }

        using AtomicFileSnapshot? after = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        Assert.True(AtomicFileSnapshot.Matches(before, after));
        Assert.Equal(edited, await Read(root));
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(path));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public async Task AbsentPreserveIntentionDoesNotCreateARootOrMissingParents(FileGenerationStrategy strategy)
    {
        GenerationRoot root = Root;
        ClientFileApplier applier = NoWriter();
        PlannedFile file = File(strategy, PlannedFileAction.Preserve);
        FileGenerationStatus expected = strategy == FileGenerationStrategy.Protected ? FileGenerationStatus.Protected : FileGenerationStatus.Skipped;

        FileGenerationResult missingRoot = await applier.ApplyAsync(root, file, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected, missingRoot.Status);
        Assert.False(Directory.Exists(root.FullPath));

        Directory.CreateDirectory(root.FullPath);
        FileGenerationResult missingParent = await applier.ApplyAsync(root, file, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected, missingParent.Status);
        Assert.Empty(Directory.GetFileSystemEntries(root.FullPath));

        Directory.CreateDirectory(Path.GetDirectoryName(root.Resolve(file.RelativePath))!);
        FileGenerationResult missingFile = await applier.ApplyAsync(root, file, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected, missingFile.Status);
        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Update, "SF-GEN-FILE-014")]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Conflict, "SF-GEN-FILE-014")]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Create, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Update, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Conflict, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Create, "SF-GEN-FILE-009")]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Update, "SF-GEN-FILE-009")]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Conflict, "SF-GEN-FILE-009")]
    public async Task ForbiddenActionsConflictBeforeAnyWriteWhetherAbsentOrPresent(
        FileGenerationStrategy strategy, PlannedFileAction action, string code)
    {
        GenerationRoot root = Root;
        ClientFileApplier applier = NoWriter();
        PlannedFile file = File(strategy, action);

        FileGenerationResult missing = await applier.ApplyAsync(root, file, "Candidate.", TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Conflict, missing.Status);
        Assert.Equal(code, missing.Error?.Code.Value);
        Assert.False(Directory.Exists(root.FullPath));

        await Put(root, "Client content.");
        FileGenerationResult existing = await applier.ApplyAsync(root, file, "Candidate.", TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Conflict, existing.Status);
        Assert.Equal(code, existing.Error?.Code.Value);
        Assert.Equal(Encoding.UTF8.GetBytes("Client content."), await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Custom)]
    public async Task PreservationDoesNotRequireReadingClientContentOrEncodingACandidate(FileGenerationStrategy strategy)
    {
        GenerationRoot root = Root;
        await Put(root, "Private client content.");
        string path = root.Resolve(File().RelativePath);
        PlannedFile file = File(strategy, strategy == FileGenerationStrategy.CreateOnce ? PlannedFileAction.Create : PlannedFileAction.Preserve);
        UnixFileMode? originalMode = OperatingSystem.IsLinux() ? System.IO.File.GetUnixFileMode(path) : null;
        try
        {
            // Windows denies data reads through sharing; Linux removes file read permission.
            using FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(path, UnixFileMode.None);
            string invalidCandidate = new((char)0xD800, 1);
            FileGenerationResult result = await NoWriter().ApplyAsync(root, file, invalidCandidate, TestContext.Current.CancellationToken);
            Assert.Equal(FileGenerationStatus.Preserved, result.Status);
        }
        finally
        {
            if (OperatingSystem.IsLinux()) System.IO.File.SetUnixFileMode(path, originalMode!.Value);
        }

        Assert.Equal(Encoding.UTF8.GetBytes("Private client content."), await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public async Task ExistingReadOnlyFilesRemainIntact(FileGenerationStrategy strategy)
    {
        GenerationRoot root = Root;
        await Put(root, "Read-only client content.");
        string path = root.Resolve(File().RelativePath);
        FileAttributes original = System.IO.File.GetAttributes(path);
        try
        {
            System.IO.File.SetAttributes(path, original | FileAttributes.ReadOnly);
            // Compare the effective attributes: the OS can normalize the requested flags.
            FileAttributes before = System.IO.File.GetAttributes(path);
            Assert.True(before.HasFlag(FileAttributes.ReadOnly));
            FileGenerationResult result = await NoWriter().ApplyAsync(root, File(strategy, PlannedFileAction.Preserve),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(strategy == FileGenerationStrategy.Protected ? FileGenerationStatus.Protected : FileGenerationStatus.Preserved, result.Status);
            Assert.Equal(before, System.IO.File.GetAttributes(path));
        }
        finally
        {
            System.IO.File.SetAttributes(path, original);
        }

        Assert.Equal(Encoding.UTF8.GetBytes("Read-only client content."), await Read(root));
    }

    [Theory]
    [InlineData(0)] // directories opened
    [InlineData(4)] // destination inspected
    [InlineData(3)] // content flushed
    public async Task AFileAppearingDuringCreationIsPreservedAndTheTemporaryIsRemoved(int checkpoint)
    {
        GenerationRoot root = Root;
        AtomicFileWriter writer = new(stage =>
        {
            if ((int)stage == checkpoint) System.IO.File.WriteAllText(root.Resolve(File().RelativePath), "Concurrent customization.");
        });

        FileGenerationResult result = await new ClientFileApplier(writer).ApplyAsync(root, File(), "Candidate.", TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Preserved, result.Status);
        Assert.Equal(Encoding.UTF8.GetBytes("Concurrent customization."), await Read(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task ConcurrentCreatorsNeverReplaceTheWinningContent()
    {
        GenerationRoot root = Root;
        ClientFileApplier applier = new();
        Task<FileGenerationResult>[] attempts = Enumerable.Range(0, 8)
            .Select(index => applier.ApplyAsync(root, File(), $"Candidate {index}.", TestContext.Current.CancellationToken)).ToArray();

        FileGenerationResult[] results = await Task.WhenAll(attempts);

        int winner = Assert.Single(Enumerable.Range(0, results.Length), index => results[index].Status == FileGenerationStatus.Created);
        Assert.All(results, result => Assert.Contains(result.Status,
            new[] { FileGenerationStatus.Created, FileGenerationStatus.Preserved, FileGenerationStatus.Failed }));
        Assert.Equal(Encoding.UTF8.GetBytes($"Candidate {winner}."), await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CancellationBeforePromotionLeavesNoDestinationOrTemporary(int checkpoint)
    {
        GenerationRoot root = Root;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        AtomicFileWriter writer = new(stage => { if ((int)stage == checkpoint) cancellation.Cancel(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ClientFileApplier(writer).ApplyAsync(root, File(), "Candidate.", cancellation.Token));

        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public async Task PreCancelledCallsDoNotCreateAnything(FileGenerationStrategy strategy)
    {
        GenerationRoot root = Root;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NoWriter().ApplyAsync(root,
            File(strategy, PlannedFileAction.Preserve), cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task WriteFailuresAreBoundedAndUnexpectedExceptionsAreNotHidden()
    {
        GenerationRoot root = Root;
        AtomicFileWriter failed = new(stage => { if (stage == AtomicWriteCheckpoint.ContentFlushed) throw new IOException("Private path and data."); });
        FileGenerationResult result = await new ClientFileApplier(failed).ApplyAsync(root, File(), "Candidate.", TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
        Assert.Equal("The client file could not be inspected or created safely.", result.Error?.Message);
        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));

        AtomicFileWriter bug = new(stage => { if (stage == AtomicWriteCheckpoint.ContentFlushed) throw new InvalidOperationException("Bug."); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClientFileApplier(bug).ApplyAsync(root, File(), "Candidate.", TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, "ancestor")]
    [InlineData(FileGenerationStrategy.CreateOnce, "root")]
    [InlineData(FileGenerationStrategy.CreateOnce, "parent")]
    [InlineData(FileGenerationStrategy.CreateOnce, "destination")]
    [InlineData(FileGenerationStrategy.Custom, "ancestor")]
    [InlineData(FileGenerationStrategy.Custom, "root")]
    [InlineData(FileGenerationStrategy.Custom, "parent")]
    [InlineData(FileGenerationStrategy.Custom, "destination")]
    public async Task MetadataInspectionNeverFollowsLinks(FileGenerationStrategy strategy, string location)
    {
        GenerationRoot root = Root;
        string outside = Path.Combine(sandbox, "outside");
        Directory.CreateDirectory(outside);
        System.IO.File.WriteAllText(Path.Combine(outside, "marker.txt"), "Outside content.");
        string link;
        if (location == "ancestor")
        {
            link = Path.Combine(sandbox, "linked-ancestor");
            root = new(Path.Combine(link, "generation"));
        }
        else if (location == "root") link = root.FullPath;
        else
        {
            Directory.CreateDirectory(root.FullPath);
            link = location == "parent" ? root.Resolve("customizations") : root.Resolve(File().RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        }

        DirectoryLinkFixture.Create(link, outside);
        PlannedFile file = File(strategy, strategy == FileGenerationStrategy.CreateOnce ? PlannedFileAction.Create : PlannedFileAction.Preserve);
        FileGenerationResult result = await new ClientFileApplier().ApplyAsync(root, file, "Candidate.", TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
        Assert.Equal([Path.Combine(outside, "marker.txt")], Directory.GetFileSystemEntries(outside));
        Assert.Equal("Outside content.", System.IO.File.ReadAllText(Path.Combine(outside, "marker.txt")));
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData("nested/.GiT/hooks/pre-commit")]
    [InlineData("NUL.txt")]
    public async Task LogicalPathDefensePrecedesFilesystemAccess(string path)
    {
        GenerationRoot root = Root;
        PlannedFile file = new(new("unsafe-file"), path, new(PlannedFileOriginKind.Template, "fixture-template"),
            FileGenerationStrategy.CreateOnce, PlannedFileAction.Create);
        await Assert.ThrowsAsync<ArgumentException>(() => new ClientFileApplier().ApplyAsync(root, file, "Candidate.", TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task MissingOrInvalidCreationContentFailsBeforeCreatingDirectories()
    {
        GenerationRoot root = Root;
        ClientFileApplier applier = new();
        await Assert.ThrowsAsync<ArgumentNullException>("content", () => applier.ApplyAsync(root, File(), cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EncoderFallbackException>(() => applier.ApplyAsync(root, File(), new string((char)0xD800, 1), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task GeneratedFilesSentToTheClientApplierRemainAContractError()
    {
        GenerationRoot root = Root;
        await Assert.ThrowsAsync<ArgumentException>("file", () => new ClientFileApplier().ApplyAsync(root, File(FileGenerationStrategy.Generated), "Candidate.", TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-client-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        RemoveLinks(sandbox);
        Directory.Delete(sandbox, recursive: true);
    }

    private static void RemoveLinks(string path)
    {
        foreach (string child in Directory.EnumerateDirectories(path))
        {
            if ((System.IO.File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) Directory.Delete(child, recursive: false);
            else RemoveLinks(child);
        }
    }

    private static ClientFileApplier NoWriter() => new(new AtomicFileWriter(_ => Assert.Fail("Preservation must never enter the writer.")));

    private static PlannedFile File(FileGenerationStrategy strategy = FileGenerationStrategy.CreateOnce, PlannedFileAction action = PlannedFileAction.Create) =>
        new(new("client-file"), "customizations/file.txt", new(PlannedFileOriginKind.Template, "fixture-template"), strategy, action);

    private static async Task Put(GenerationRoot root, string content)
    {
        string path = root.Resolve(File().RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await System.IO.File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
    }

    private static Task<byte[]> Read(GenerationRoot root) => System.IO.File.ReadAllBytesAsync(root.Resolve(File().RelativePath), TestContext.Current.CancellationToken);

    private static void AssertOnlyDestination(GenerationRoot root) =>
        Assert.Equal([root.Resolve(File().RelativePath)], Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
}
