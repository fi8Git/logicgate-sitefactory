using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class FileGenerationStrategyGuardTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-strategy-guard-tests-").FullName;

    [Theory]
    [InlineData(FileGenerationStrategy.Merge, PlannedFileAction.Create, "SF-GEN-FILE-006")]
    [InlineData(FileGenerationStrategy.Merge, PlannedFileAction.Update, "SF-GEN-FILE-006")]
    [InlineData(FileGenerationStrategy.Merge, PlannedFileAction.Preserve, "SF-GEN-FILE-006")]
    [InlineData(FileGenerationStrategy.Merge, PlannedFileAction.Conflict, "SF-GEN-FILE-006")]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Create, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Update, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Conflict, "SF-GEN-FILE-008")]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Create, "SF-GEN-FILE-009")]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Update, "SF-GEN-FILE-009")]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Conflict, "SF-GEN-FILE-009")]
    public async Task BothAppliersReturnStableConflictsWithoutCreatingReadingOrReplacingFiles(
        FileGenerationStrategy strategy, PlannedFileAction action, string code)
    {
        GenerationRoot root = new(Path.Combine(sandbox, "generation"));
        PlannedFile file = File(strategy, action);
        AtomicFileWriter writer = new(_ => Assert.Fail("A blocked strategy must never enter the writer."));
        GeneratedFileApplier generated = new(writer);
        ClientFileApplier client = new(writer);

        async Task AssertBlocked()
        {
            FileGenerationResult first = await generated.ApplyAsync(root, file, new("Candidate that must never be written."),
                cancellationToken: TestContext.Current.CancellationToken);
            // No content is required for an unsupported operation, and no rendering/encoding is attempted.
            FileGenerationResult second = await client.ApplyAsync(root, file, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(first, second);
            Assert.Equal(file.Id, first.FileId);
            Assert.Equal(FileGenerationStatus.Conflict, first.Status);
            Assert.Equal(code, first.Error?.Code.Value);
            Assert.Equal(GenerationExecutionStatus.Conflict, new GenerationExecutionResult(new("guard-plan"), [first], []).Status);
        }

        await AssertBlocked();
        Assert.False(Directory.Exists(root.FullPath));

        string destination = root.Resolve(file.RelativePath);
        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        await AssertBlocked();
        Assert.Empty(Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
        Assert.Equal([parent], Directory.GetDirectories(root.FullPath, "*", SearchOption.AllDirectories));

        byte[] original = [0xEF, 0xBB, 0xBF, 0xFF, 0x00, 0x0D, 0x0A];
        await System.IO.File.WriteAllBytesAsync(destination, original, TestContext.Current.CancellationToken);
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(destination, sentinel);
        using AtomicWriteSession inspection = AtomicWriteSession.Open(root, file.RelativePath, TestContext.Current.CancellationToken, createDirectories: false);
        using AtomicFileSnapshot? before = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        await AssertBlocked();
        using AtomicFileSnapshot? after = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);

        Assert.True(AtomicFileSnapshot.Matches(before, after));
        Assert.Equal(original, await System.IO.File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(destination));
        Assert.Equal([destination], Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
        Assert.Equal([parent], Directory.GetDirectories(root.FullPath, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    public async Task UnknownStrategyCannotBeSmuggledIntoAnApplierThroughAPlannedFile(int strategy)
    {
        GenerationRoot root = new(Path.Combine(sandbox, "generation"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(nameof(strategy), () =>
            new ClientFileApplier().ApplyAsync(root, File((FileGenerationStrategy)strategy, PlannedFileAction.Create),
                "Never written.", TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStillPrecedesUnsupportedStrategyResults(bool useGeneratedApplier)
    {
        GenerationRoot root = new(Path.Combine(sandbox, "generation"));
        PlannedFile file = File(FileGenerationStrategy.Merge, PlannedFileAction.Create);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useGeneratedApplier
            ? new GeneratedFileApplier().ApplyAsync(root, file, new("Never written."), cancellationToken: cancellation.Token)
            : new ClientFileApplier().ApplyAsync(root, file, "Never written.", cancellation.Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-strategy-guard-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }

    private static PlannedFile File(FileGenerationStrategy strategy, PlannedFileAction action) =>
        new(new("guarded-file"), "client/file.txt", new(PlannedFileOriginKind.Template, "fixture-template"), strategy, action);
}
