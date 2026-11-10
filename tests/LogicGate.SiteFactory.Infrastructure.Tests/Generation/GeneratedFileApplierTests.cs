using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GeneratedFileApplierTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-generated-tests-").FullName;

    private GenerationRoot Root => new(Path.Combine(sandbox, "generation"));

    [Fact]
    public async Task CreatesCanonicalContentAndReportsThePlannedIdentity()
    {
        GenerationRoot root = Root;
        PlannedFile file = File();
        GeneratedFileContent content = new("Équipe 東京 🧪\r\nLine two\rLast line\n");

        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, file, content,
            cancellationToken: TestContext.Current.CancellationToken);

        byte[] expected = Encoding.UTF8.GetBytes("Équipe 東京 🧪\nLine two\nLast line\n");
        Assert.Equal(FileGenerationStatus.Created, result.Status);
        Assert.Equal(file.Id, result.FileId);
        Assert.Null(result.Error);
        Assert.Equal(expected, await System.IO.File.ReadAllBytesAsync(root.Resolve(file.RelativePath), TestContext.Current.CancellationToken));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)), content.Hash.Value);
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task SecondGenerationDoesNotStageWriteReplaceOrTouchTheFile()
    {
        GenerationRoot root = Root;
        GeneratedFileContent content = new("Initial\ncontent.\n");
        GeneratedFileApplier applier = new();
        await applier.ApplyAsync(root, File(), content, cancellationToken: TestContext.Current.CancellationToken);
        string destination = root.Resolve(File().RelativePath);
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(destination, sentinel);
        using AtomicWriteSession inspection = AtomicWriteSession.Open(root, File().RelativePath, TestContext.Current.CancellationToken, createDirectories: false);
        using AtomicFileSnapshot? before = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        AtomicFileWriter writer = new(stage => Assert.DoesNotContain(stage,
            new[] { AtomicWriteCheckpoint.TemporaryCreated, AtomicWriteCheckpoint.ChunkWritten, AtomicWriteCheckpoint.ContentFlushed }));

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root, File(), new("Initial\r\ncontent.\r\n"),
            content.Hash, TestContext.Current.CancellationToken);

        using AtomicFileSnapshot? after = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Unchanged, result.Status);
        Assert.True(AtomicFileSnapshot.Matches(before, after));
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(destination));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task NewExpectedContentUpdatesOnlyAnUnmodifiedGeneratedFile()
    {
        GenerationRoot root = Root;
        GeneratedFileContent previous = new("Old generation.\n");
        GeneratedFileContent next = new("New generation.\r\n");
        GeneratedFileApplier applier = new();
        await applier.ApplyAsync(root, File(), previous, cancellationToken: TestContext.Current.CancellationToken);

        FileGenerationResult result = await applier.ApplyAsync(root, File(PlannedFileAction.Update), next, previous.Hash, TestContext.Current.CancellationToken);
        FileGenerationResult repeated = await applier.ApplyAsync(root, File(PlannedFileAction.Update), next, next.Hash, TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Updated, result.Status);
        Assert.Equal(FileGenerationStatus.Unchanged, repeated.Status);
        Assert.Equal("New generation.\n", await Read(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task IdenticalUntrackedContentIsNotAdoptedOrUsedAsAnImplicitFutureReference()
    {
        GenerationRoot root = Root;
        await Put(root, "Untracked content.");
        GeneratedFileApplier applier = new();

        FileGenerationResult unchanged = await applier.ApplyAsync(root, File(), new("Untracked content."), cancellationToken: TestContext.Current.CancellationToken);
        FileGenerationResult different = await applier.ApplyAsync(root, File(PlannedFileAction.Update), new("Different."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Unchanged, unchanged.Status);
        AssertConflict(different, "SF-GEN-FILE-014");
        Assert.Equal("Untracked content.", await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalChangesAreNotOverwrittenEvenWhenTheyMatchTheNewCandidate(bool matchesCandidate)
    {
        GenerationRoot root = Root;
        GeneratedFileContent original = new("Original.");
        await Put(root, "User modification.");

        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, File(PlannedFileAction.Update),
            new(matchesCandidate ? "User modification." : "Next generation."), original.Hash, TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-004");
        Assert.Equal("User modification.", await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawExistingBytesAreNotNormalizedToHideAUserModification(bool withBom)
    {
        GenerationRoot root = Root;
        GeneratedFileContent previous = new("Original.\n");
        await Put(root, "Original.\r\n");
        byte[] expected = withBom ? [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Original.\n")] : Encoding.UTF8.GetBytes("Original.\r\n");
        await System.IO.File.WriteAllBytesAsync(root.Resolve(File().RelativePath), expected, TestContext.Current.CancellationToken);

        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, File(PlannedFileAction.Update),
            new("Next.\n"), previous.Hash, TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-004");
        Assert.Equal(expected, await System.IO.File.ReadAllBytesAsync(root.Resolve(File().RelativePath), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnUnchangedReadOnlyFileDoesNotRequireReplacementPermission()
    {
        GenerationRoot root = Root;
        await Put(root, "Unchanged.");
        string path = root.Resolve(File().RelativePath);
        FileAttributes original = System.IO.File.GetAttributes(path);
        try
        {
            System.IO.File.SetAttributes(path, original | FileAttributes.ReadOnly);
            // Compare the effective attributes: the OS can normalize the requested flags.
            FileAttributes before = System.IO.File.GetAttributes(path);
            Assert.True(before.HasFlag(FileAttributes.ReadOnly));
            FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, File(), new("Unchanged."),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(FileGenerationStatus.Unchanged, result.Status);
            Assert.Equal(before, System.IO.File.GetAttributes(path));
        }
        finally
        {
            System.IO.File.SetAttributes(path, original);
        }
    }

    [Fact]
    public async Task CreateDoesNotBecomeUpdateAndUpdateDoesNotRecreateADeletedFile()
    {
        GenerationRoot root = Root;
        GeneratedFileContent previous = new("Original.");
        await Put(root, "Original.");
        GeneratedFileApplier applier = new();
        FileGenerationResult wrongAction = await applier.ApplyAsync(root, File(), new("Next."), previous.Hash, TestContext.Current.CancellationToken);
        AssertConflict(wrongAction, "SF-GEN-FILE-014");
        Assert.Equal("Original.", await Read(root));

        System.IO.File.Delete(root.Resolve(File().RelativePath));
        FileGenerationResult missing = await applier.ApplyAsync(root, File(PlannedFileAction.Update), new("Next."), previous.Hash, TestContext.Current.CancellationToken);
        AssertConflict(missing, "SF-GEN-FILE-014");
        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Create)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Preserve)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Preserve)]
    public async Task SupportedIntentionsSentToTheWrongApplierRemainContractErrors(FileGenerationStrategy strategy, PlannedFileAction action)
    {
        GenerationRoot root = Root;
        await Assert.ThrowsAsync<ArgumentException>(() => new GeneratedFileApplier().ApplyAsync(root, File(action, strategy), new("Never written."),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(PlannedFileAction.Preserve)]
    [InlineData(PlannedFileAction.Conflict)]
    public async Task NonWritingActionsDoNotCreateTheRoot(PlannedFileAction action)
    {
        GenerationRoot root = Root;
        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, File(action), new("Never written."),
            cancellationToken: TestContext.Current.CancellationToken);
        AssertConflict(result, "SF-GEN-FILE-014");
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task AlreadyCancelledApplicationCreatesNothing()
    {
        GenerationRoot root = Root;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GeneratedFileApplier().ApplyAsync(root, File(), new("Never written."),
            cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData((int)AtomicWriteCheckpoint.DirectoriesOpened)]
    [InlineData((int)AtomicWriteCheckpoint.DestinationInspected)]
    [InlineData((int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData((int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task CancellationKeepsOriginalAndCleansTemporary(int checkpoint)
    {
        GenerationRoot root = Root;
        await Put(root, "Original.");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        AtomicFileWriter writer = new(stage => { if ((int)stage == checkpoint) cancellation.Cancel(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GeneratedFileApplier(writer).ApplyAsync(root, File(PlannedFileAction.Update),
            new(new string('x', 20000)), new GeneratedFileContent("Original.").Hash, cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal("Original.", await Read(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task WriteErrorIsBoundedAndDoesNotPublishPartialContent()
    {
        GenerationRoot root = Root;
        await Put(root, "Original.");
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed) throw new IOException(root.FullPath + " <PRIVATE-CONTENT>");
        });

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root, File(PlannedFileAction.Update),
            new("Replacement."), new GeneratedFileContent("Original.").Hash, TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
        Assert.Equal("The generated file could not be inspected or written safely.", result.Error?.Message);
        Assert.Equal("Original.", await Read(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task UnexpectedProgrammingErrorsAreNotConvertedToUserFailures()
    {
        AtomicFileWriter writer = new(_ => throw new InvalidOperationException("Test failure."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GeneratedFileApplier(writer).ApplyAsync(Root, File(), new("Not written."),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData((int)AtomicWriteCheckpoint.DestinationInspected)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task ALocalEditAfterInspectionCannotPassTheFinalPrecondition(int checkpoint)
    {
        GenerationRoot root = Root;
        await Put(root, "Original.");
        AtomicFileWriter writer = new(stage =>
        {
            if ((int)stage == checkpoint) System.IO.File.WriteAllText(root.Resolve(File().RelativePath), "Concurrent edit.");
        });

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root, File(PlannedFileAction.Update),
            new("Next generation."), new GeneratedFileContent("Original.").Hash, TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-013");
        Assert.Equal("Concurrent edit.", await Read(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCreationOrDeletionCannotSilentlyChangeTheAuthorizedAction(bool deleteExisting)
    {
        GenerationRoot root = Root;
        if (deleteExisting) await Put(root, "Original.");
        AtomicFileWriter writer = new(stage =>
        {
            if (stage != AtomicWriteCheckpoint.ContentFlushed) return;
            if (deleteExisting) System.IO.File.Delete(root.Resolve(File().RelativePath));
            else System.IO.File.WriteAllText(root.Resolve(File().RelativePath), "Concurrent creation.");
        });

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root,
            File(deleteExisting ? PlannedFileAction.Update : PlannedFileAction.Create), new("Candidate."),
            deleteExisting ? new GeneratedFileContent("Original.").Hash : null, TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-013");
        if (deleteExisting) Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
        else Assert.Equal("Concurrent creation.", await Read(root));
        Assert.Empty(Directory.GetFiles(root.FullPath, ".sitefactory-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SameContentOnADifferentFileIdentityIsStillAConcurrentChange()
    {
        GenerationRoot root = Root;
        await Put(root, "Original.");
        AtomicFileWriter writer = new(stage =>
        {
            if (stage != AtomicWriteCheckpoint.ContentFlushed) return;
            string path = root.Resolve(File().RelativePath);
            System.IO.File.Move(path, root.Resolve("displaced.txt"));
            System.IO.File.WriteAllText(path, "Original.");
        });

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root, File(PlannedFileAction.Update),
            new("Candidate."), new GeneratedFileContent("Original.").Hash, TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-013");
        Assert.Equal("Original.", await Read(root));
        Assert.Empty(Directory.GetFiles(root.FullPath, ".sitefactory-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task NoOpAlsoRechecksItsObservationWithoutStagingContent()
    {
        GenerationRoot root = Root;
        await Put(root, "Original.");
        AtomicFileWriter writer = new(stage =>
        {
            Assert.NotEqual(AtomicWriteCheckpoint.TemporaryCreated, stage);
            if (stage == AtomicWriteCheckpoint.DestinationInspected)
                System.IO.File.WriteAllText(root.Resolve(File().RelativePath), "Concurrent edit.");
        });

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root, File(), new("Original."),
            cancellationToken: TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-013");
        Assert.Equal("Concurrent edit.", await Read(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task UpdateWithMissingAncestorsDoesNotCreateThem()
    {
        GenerationRoot root = Root;
        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, File(PlannedFileAction.Update), new("Candidate."),
            new GeneratedFileContent("Original.").Hash, TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData("nested/.GiT/hooks/pre-commit")]
    [InlineData("NUL.txt")]
    public async Task LogicalPathDefenseRunsBeforeFilesystemAccess(string path)
    {
        GenerationRoot root = Root;
        PlannedFile file = new(new("unsafe-file"), path, new(PlannedFileOriginKind.Template, "fixture-template"),
            FileGenerationStrategy.Generated, PlannedFileAction.Create);

        await Assert.ThrowsAsync<ArgumentException>(() => new GeneratedFileApplier().ApplyAsync(root, file, new("Never written."),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData("ancestor")]
    [InlineData("root")]
    [InlineData("parent")]
    [InlineData("destination")]
    public async Task ComparisonRefusesLinksBeforeReadingTheirTarget(string location)
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
            link = location == "parent" ? root.Resolve("generated") : root.Resolve(File().RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        }

        DirectoryLinkFixture.Create(link, outside);
        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root, File(), new("Outside content."),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.Equal([Path.Combine(outside, "marker.txt")], Directory.GetFileSystemEntries(outside));
        Assert.Equal("Outside content.", System.IO.File.ReadAllText(Path.Combine(outside, "marker.txt")));
    }

    [Fact]
    public void CanonicalContentRetainsEmptyAndFinalNewlineSemanticsAndRejectsInvalidUtf16()
    {
        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", new GeneratedFileContent("").Hash.Value);
        Assert.Equal(new GeneratedFileContent("a\nb\n").Hash, new GeneratedFileContent("a\r\nb\r").Hash);
        Assert.NotEqual(new GeneratedFileContent("a").Hash, new GeneratedFileContent("a\n").Hash);
        Assert.Throws<EncoderFallbackException>(() => new GeneratedFileContent(new string((char)0xD800, 1)));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-generated-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
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

    private static PlannedFile File(PlannedFileAction action = PlannedFileAction.Create,
        FileGenerationStrategy strategy = FileGenerationStrategy.Generated) =>
        new(new("generated-file"), "generated/file.txt", new(PlannedFileOriginKind.Template, "fixture-template"), strategy, action);

    private static async Task Put(GenerationRoot root, string content)
    {
        string path = root.Resolve(File().RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await System.IO.File.WriteAllTextAsync(path, content, new UTF8Encoding(false), TestContext.Current.CancellationToken);
    }

    private static Task<string> Read(GenerationRoot root) => System.IO.File.ReadAllTextAsync(root.Resolve(File().RelativePath), TestContext.Current.CancellationToken);

    private static void AssertOnlyDestination(GenerationRoot root) =>
        Assert.Equal([root.Resolve(File().RelativePath)], Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));

    private static void AssertConflict(FileGenerationResult result, string code)
    {
        Assert.Equal(FileGenerationStatus.Conflict, result.Status);
        Assert.Equal(code, result.Error?.Code.Value);
    }
}
