using System.Reflection;
using System.Security.Cryptography;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class BinaryFileApplierTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-binary-tests-").FullName;
    private GenerationRoot Root => new(Path.Combine(sandbox, "generation"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void BinaryEntriesAreInternalAndDoNotExposeMutableContent()
    {
        MethodInfo? factory = typeof(GeneratedFileContent).GetMethod("FromBytes", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(factory);
        Assert.True(factory.IsAssembly);
        Assert.Equal(typeof(ReadOnlySpan<byte>), Assert.Single(factory.GetParameters()).ParameterType);
        MethodInfo? apply = typeof(ClientFileApplier).GetMethod("ApplyPreparedAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(apply);
        Assert.True(apply.IsAssembly);
        Assert.Equal([nameof(GeneratedFileContent.Hash)], typeof(GeneratedFileContent).GetProperties().Select(static property => property.Name));
    }

    [Theory]
    [InlineData("arbitrary", false)]
    [InlineData("utf8-bom", false)]
    [InlineData("utf16-bom", false)]
    [InlineData("line-endings", false)]
    [InlineData("empty", false)]
    [InlineData("large", false)]
    [InlineData("arbitrary", true)]
    [InlineData("utf8-bom", true)]
    [InlineData("utf16-bom", true)]
    [InlineData("line-endings", true)]
    [InlineData("empty", true)]
    [InlineData("large", true)]
    public async Task PreparedSnapshotSurvivesInputMutationAndWritesExactPhysicalBytes(string scenario, bool client)
    {
        byte[] expected = Payload(scenario);
        byte[] source = expected.ToArray();
        GeneratedFileContent content = GeneratedFileContent.FromBytes(source);
        GeneratedContentHash hash = content.Hash;
        Array.Fill(source, (byte)42);
        Assert.Equal(expected, content.Bytes.ToArray());
        Assert.Equal(Hash(expected), hash);

        GenerationRoot root = Root;
        FileGenerationResult result = await ApplyAsync(root, File(client), content, client);

        Assert.Equal(FileGenerationStatus.Created, result.Status);
        Assert.Null(result.Error);
        Assert.Equal(expected, await ReadAsync(root));
        Assert.Equal(hash, Hash(await ReadAsync(root)));
        Assert.Equal(hash, content.Hash);
        Assert.Equal(hash, result.Observation?.ProposedHash);
        Assert.False(result.Observation?.DestinationExistedAtInspection);
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task GeneratedUpdateUsesTheTrustedHashOfThePreviousPhysicalBytes()
    {
        GenerationRoot root = Root;
        GeneratedFileContent previous = GeneratedFileContent.FromBytes(Payload("arbitrary"));
        GeneratedFileContent next = GeneratedFileContent.FromBytes(Payload("utf8-bom"));
        Assert.Equal(FileGenerationStatus.Created, (await ApplyAsync(root, File(false), previous, false)).Status);

        FileGenerationResult result = await new GeneratedFileApplier().ApplyAsync(root,
            File(false, PlannedFileAction.Update), next, previous.Hash, Token);

        Assert.Equal(FileGenerationStatus.Updated, result.Status);
        Assert.Equal(Payload("utf8-bom"), await ReadAsync(root));
        Assert.Equal(next.Hash, Hash(await ReadAsync(root)));
        Assert.Equal(previous.Hash, result.Observation?.PreviousHash);
        Assert.Equal(previous.Hash, result.Observation?.CurrentHash);
        Assert.Equal(next.Hash, result.Observation?.ProposedHash);
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task IdenticalGeneratedBytesDoNotStageReplaceOrTouchTheDestination()
    {
        GenerationRoot root = Root;
        GeneratedFileContent content = GeneratedFileContent.FromBytes(Payload("arbitrary"));
        await ApplyAsync(root, File(false), content, false);
        string path = root.Resolve(File(false).RelativePath);
        System.IO.File.SetLastWriteTimeUtc(path, new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        DateTime before = System.IO.File.GetLastWriteTimeUtc(path);
        AtomicFileWriter writer = NoWriting();

        FileGenerationResult result = await new GeneratedFileApplier(writer).ApplyAsync(root,
            File(false, PlannedFileAction.Update), GeneratedFileContent.FromBytes(Payload("arbitrary")), content.Hash, Token);

        Assert.Equal(FileGenerationStatus.Unchanged, result.Status);
        Assert.Equal(before, System.IO.File.GetLastWriteTimeUtc(path));
        Assert.Equal(Payload("arbitrary"), await ReadAsync(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALocalBinaryEditIsRefusedEvenWhenItMatchesTheNewCandidate(bool matchesCandidate)
    {
        GenerationRoot root = Root;
        GeneratedFileContent previous = GeneratedFileContent.FromBytes(Payload("arbitrary"));
        GeneratedFileContent next = GeneratedFileContent.FromBytes(Payload("utf8-bom"));
        await ApplyAsync(root, File(false), previous, false);
        byte[] edit = Payload(matchesCandidate ? "utf8-bom" : "utf16-bom");
        await System.IO.File.WriteAllBytesAsync(root.Resolve(File(false).RelativePath), edit, Token);

        FileGenerationResult result = await new GeneratedFileApplier(NoWriting()).ApplyAsync(root,
            File(false, PlannedFileAction.Update), next, previous.Hash, Token);

        Assert.Equal(FileGenerationStatus.Conflict, result.Status);
        Assert.Equal("SF-GEN-FILE-004", result.Error?.Code.Value);
        Assert.Equal(edit, await ReadAsync(root));
        Assert.Equal(Hash(edit), result.Observation?.CurrentHash);
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce, FileGenerationStatus.Preserved)]
    [InlineData(FileGenerationStrategy.Custom, FileGenerationStatus.Preserved)]
    [InlineData(FileGenerationStrategy.Protected, FileGenerationStatus.Protected)]
    public async Task PreparedClientContentNeverReplacesOrTouchesAnExistingClientFile(
        FileGenerationStrategy strategy, FileGenerationStatus expected)
    {
        GenerationRoot root = Root;
        await PutAsync(root, Payload("arbitrary"));
        string path = root.Resolve(File(true).RelativePath);
        System.IO.File.SetLastWriteTimeUtc(path, new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        DateTime before = System.IO.File.GetLastWriteTimeUtc(path);
        PlannedFile file = File(true, strategy == FileGenerationStrategy.CreateOnce ? PlannedFileAction.Create : PlannedFileAction.Preserve, strategy);
        FileGenerationResult result;
        using (FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = await new ClientFileApplier(NoWriting()).ApplyPreparedAsync(root, file,
                GeneratedFileContent.FromBytes(Payload("utf16-bom")), Token);
        }

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Observation?.ProposedHash);
        Assert.Null(result.Observation?.CurrentHash);
        Assert.Null(result.Observation?.PreviousHash);
        Assert.Equal(before, System.IO.File.GetLastWriteTimeUtc(path));
        Assert.Equal(Payload("arbitrary"), await ReadAsync(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(FileGenerationStrategy.CreateOnce)]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    public async Task PreservationNeedsNeitherPreparedContentNorTextEncoding(FileGenerationStrategy strategy)
    {
        GenerationRoot root = Root;
        PlannedFile file = File(true, PlannedFileAction.Preserve, strategy);
        ClientFileApplier applier = new(NoWriting());
        FileGenerationStatus expected = strategy == FileGenerationStrategy.Protected ? FileGenerationStatus.Protected : FileGenerationStatus.Skipped;

        Assert.Equal(expected, (await applier.ApplyPreparedAsync(root, file, cancellationToken: Token)).Status);
        Assert.Equal(expected, (await applier.ApplyAsync(root, file, new string((char)0xD800, 1), Token)).Status);
        Assert.False(Directory.Exists(root.FullPath));
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
    public async Task PreparedContentCannotAuthorizeAForbiddenClientAction(FileGenerationStrategy strategy, PlannedFileAction action, string code)
    {
        GenerationRoot root = Root;
        PlannedFile file = File(true, action, strategy);
        ClientFileApplier applier = new(NoWriting());
        FileGenerationResult absent = await applier.ApplyPreparedAsync(root, file, cancellationToken: Token);
        Assert.Equal(FileGenerationStatus.Conflict, absent.Status);
        Assert.Equal(code, absent.Error?.Code.Value);
        Assert.Null(absent.Observation?.DestinationExistedAtInspection);
        Assert.False(Directory.Exists(root.FullPath));

        await PutAsync(root, Payload("arbitrary"));
        FileGenerationResult existing = await applier.ApplyPreparedAsync(root, file,
            GeneratedFileContent.FromBytes(Payload("large")), Token);
        Assert.Equal(absent.Status, existing.Status);
        Assert.Equal(code, existing.Error?.Code.Value);
        Assert.Equal(Payload("arbitrary"), await ReadAsync(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task MissingPreparedContentAndWrongStrategyRemainContractErrorsWithoutCreatingDirectories()
    {
        GenerationRoot root = Root;
        ClientFileApplier applier = new();
        await Assert.ThrowsAsync<ArgumentNullException>("content", () => applier.ApplyPreparedAsync(root, File(true), cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>("file", () => applier.ApplyPreparedAsync(root, File(false),
            GeneratedFileContent.FromBytes(Payload("arbitrary")), Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData((int)AtomicWriteCheckpoint.DirectoriesOpened)]
    [InlineData((int)AtomicWriteCheckpoint.DestinationInspected)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task LateClientCreationPreservesTheConcurrentBinaryContentAndCleansTemporary(int checkpoint)
    {
        GenerationRoot root = Root;
        byte[] winner = Payload("utf16-bom");
        AtomicFileWriter writer = new(stage =>
        {
            if ((int)stage == checkpoint) System.IO.File.WriteAllBytes(root.Resolve(File(true).RelativePath), winner);
        });

        FileGenerationResult result = await new ClientFileApplier(writer).ApplyPreparedAsync(root, File(true),
            GeneratedFileContent.FromBytes(Payload("large")), Token);

        Assert.Equal(FileGenerationStatus.Preserved, result.Status);
        Assert.Equal(winner, await ReadAsync(root));
        AssertOnlyDestination(root);
    }

    [Fact]
    public async Task ConcurrentBinaryCreatorsNeverReplaceTheWinningContent()
    {
        GenerationRoot root = Root;
        ClientFileApplier applier = new();
        Task<FileGenerationResult>[] tasks = Enumerable.Range(0, 8).Select(index => applier.ApplyPreparedAsync(root, File(true),
            GeneratedFileContent.FromBytes(new byte[] { 0, (byte)index, 255 }), Token)).ToArray();
        FileGenerationResult[] results = await Task.WhenAll(tasks);

        int winner = Assert.Single(Enumerable.Range(0, results.Length), index => results[index].Status == FileGenerationStatus.Created);
        Assert.All(results, result => Assert.Contains(result.Status,
            new[] { FileGenerationStatus.Created, FileGenerationStatus.Preserved, FileGenerationStatus.Failed }));
        Assert.Equal(new byte[] { 0, (byte)winner, 255 }, await ReadAsync(root));
        AssertOnlyDestination(root);
    }

    [Theory]
    [InlineData(false, (int)AtomicWriteCheckpoint.DirectoriesOpened)]
    [InlineData(false, (int)AtomicWriteCheckpoint.DestinationInspected)]
    [InlineData(false, (int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData(false, (int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData(false, (int)AtomicWriteCheckpoint.ContentFlushed)]
    [InlineData(true, (int)AtomicWriteCheckpoint.DirectoriesOpened)]
    [InlineData(true, (int)AtomicWriteCheckpoint.DestinationInspected)]
    [InlineData(true, (int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData(true, (int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData(true, (int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task BinaryCancellationNeverPromotesPartialContentAndCleansTemporary(bool client, int checkpoint)
    {
        GenerationRoot root = Root;
        byte[] original = Payload("arbitrary");
        if (!client) await PutAsync(root, original);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        AtomicFileWriter writer = new(stage => { if ((int)stage == checkpoint) cancellation.Cancel(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ApplyAsync(root,
            File(client, client ? PlannedFileAction.Create : PlannedFileAction.Update), GeneratedFileContent.FromBytes(Payload("large")),
            client, writer, client ? null : Hash(original), cancellation.Token));

        if (client) Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
        else { Assert.Equal(original, await ReadAsync(root)); AssertOnlyDestination(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledBinaryApplicationCreatesNothing(bool client)
    {
        GenerationRoot root = Root;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ApplyAsync(root, File(client),
            GeneratedFileContent.FromBytes(Payload("large")), client, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(false, (int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData(false, (int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData(false, (int)AtomicWriteCheckpoint.ContentFlushed)]
    [InlineData(true, (int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData(true, (int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData(true, (int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task BinaryIoFailureIsSanitizedAndCleansTemporaryWithoutPromotingContent(bool client, int checkpoint)
    {
        GenerationRoot root = Root;
        byte[] original = Payload("arbitrary");
        if (!client) await PutAsync(root, original);
        AtomicFileWriter writer = new(stage => { if ((int)stage == checkpoint) throw new IOException("sensitive-fixture-detail"); });

        FileGenerationResult result = await ApplyAsync(root, File(client, client ? PlannedFileAction.Create : PlannedFileAction.Update),
            GeneratedFileContent.FromBytes(Payload("large")), client, writer, client ? null : Hash(original));

        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
        Assert.DoesNotContain("sensitive-fixture-detail", result.Error!.Message, StringComparison.Ordinal);
        if (client) Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
        else { Assert.Equal(original, await ReadAsync(root)); AssertOnlyDestination(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BinaryProgrammingErrorsAreNotDisguisedAndStillCleanTemporary(bool client)
    {
        GenerationRoot root = Root;
        AtomicFileWriter writer = new(stage => { if (stage == AtomicWriteCheckpoint.ContentFlushed) throw new InvalidOperationException("fixture defect"); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(root, File(client),
            GeneratedFileContent.FromBytes(Payload("large")), client, writer));
        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false, ".git/config")]
    [InlineData(false, "nested/.GiT/hooks/pre-commit")]
    [InlineData(false, "NUL.txt")]
    [InlineData(true, ".git/config")]
    [InlineData(true, "nested/.GiT/hooks/pre-commit")]
    [InlineData(true, "NUL.txt")]
    public async Task BinaryEntryCannotBypassDestinationValidation(bool client, string path)
    {
        GenerationRoot root = Root;
        PlannedFile file = new(new("binary-file"), path, new(PlannedFileOriginKind.Template, "fixture-template"),
            client ? FileGenerationStrategy.CreateOnce : FileGenerationStrategy.Generated, PlannedFileAction.Create);
        await Assert.ThrowsAsync<ArgumentException>(() => ApplyAsync(root, file, GeneratedFileContent.FromBytes(Payload("arbitrary")), client));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(false, "ancestor")]
    [InlineData(false, "root")]
    [InlineData(false, "parent")]
    [InlineData(false, "destination")]
    [InlineData(true, "ancestor")]
    [InlineData(true, "root")]
    [InlineData(true, "parent")]
    [InlineData(true, "destination")]
    public async Task BinaryEntryNeverFollowsExistingDirectoryLinks(bool client, string location)
    {
        GenerationRoot root = Root;
        string outside = Path.Combine(sandbox, "outside");
        Directory.CreateDirectory(outside);
        string marker = Path.Combine(outside, "marker.bin");
        await System.IO.File.WriteAllBytesAsync(marker, Payload("arbitrary"), Token);
        string link;
        if (location == "ancestor")
        {
            link = Path.Combine(sandbox, "linked-ancestor");
            root = new(Path.Combine(link, "generation"));
        }
        else if (location == "root") link = root.FullPath;
        else
        {
            link = root.Resolve(location == "parent" ? "output" : File(client).RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        }
        DirectoryLinkFixture.Create(link, outside);

        FileGenerationResult result = await ApplyAsync(root, File(client), GeneratedFileContent.FromBytes(Payload("large")), client);

        Assert.Equal(FileGenerationStatus.Failed, result.Status);
        Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
        Assert.Equal([marker], Directory.GetFileSystemEntries(outside));
        Assert.Equal(Payload("arbitrary"), await System.IO.File.ReadAllBytesAsync(marker, Token));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-binary-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
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

    private static byte[] Payload(string scenario) => scenario switch
    {
        "arbitrary" => [0, 255, 128, 192, 175],
        "utf8-bom" => [239, 187, 191, 65, 13, 10, 66, 13],
        "utf16-bom" => [255, 254, 65, 0, 13, 0, 10, 0],
        "line-endings" => [65, 13, 10, 66, 13, 67, 10],
        "empty" => [],
        "large" => Enumerable.Range(0, 8193).Select(static value => (byte)(value % 256)).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
    };

    private static GeneratedContentHash Hash(byte[] bytes) => new(Convert.ToHexString(SHA256.HashData(bytes)));
    private static PlannedFile File(bool client, PlannedFileAction action = PlannedFileAction.Create, FileGenerationStrategy? strategy = null) =>
        new(new("binary-file"), "output/file.bin", new(PlannedFileOriginKind.Template, "fixture-template"),
            strategy ?? (client ? FileGenerationStrategy.CreateOnce : FileGenerationStrategy.Generated), action);

    private static Task<FileGenerationResult> ApplyAsync(GenerationRoot root, PlannedFile file, GeneratedFileContent content, bool client,
        AtomicFileWriter? writer = null, GeneratedContentHash? previous = null, CancellationToken? cancellationToken = null) => client
        ? (writer is null ? new ClientFileApplier() : new ClientFileApplier(writer)).ApplyPreparedAsync(root, file, content, cancellationToken ?? Token)
        : (writer is null ? new GeneratedFileApplier() : new GeneratedFileApplier(writer)).ApplyAsync(root, file, content, previous, cancellationToken ?? Token);

    private static AtomicFileWriter NoWriting() => new(stage => Assert.DoesNotContain(stage,
        new[] { AtomicWriteCheckpoint.TemporaryCreated, AtomicWriteCheckpoint.ChunkWritten, AtomicWriteCheckpoint.ContentFlushed }));

    private static async Task PutAsync(GenerationRoot root, byte[] bytes)
    {
        string path = root.Resolve(File(false).RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await System.IO.File.WriteAllBytesAsync(path, bytes, Token);
    }

    private static Task<byte[]> ReadAsync(GenerationRoot root) => System.IO.File.ReadAllBytesAsync(root.Resolve(File(false).RelativePath), Token);
    private static void AssertOnlyDestination(GenerationRoot root) =>
        Assert.Equal([root.Resolve(File(false).RelativePath)], Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
}
