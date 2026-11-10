using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using LogicGate.SiteFactory.Infrastructure.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-atomic-tests-").FullName;

    private GenerationRoot Root => new(Path.Combine(sandbox, "generation"));

    [Fact]
    public async Task CreatesTheRootAndParentsThenPromotesOneCompleteFile()
    {
        GenerationRoot root = Root;
        Assert.False(Directory.Exists(root.FullPath));

        AtomicFileWriteResult result = await new AtomicFileWriter().WriteAsync(root, "nested/docs/readme.txt", "Complete content.\n",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Complete content.\n", await File.ReadAllTextAsync(root.Resolve(result.RelativePath), TestContext.Current.CancellationToken));
        Assert.Equal(Encoding.UTF8.GetByteCount("Complete content.\n"), result.BytesWritten);
        Assert.Equal([root.Resolve("nested/docs/readme.txt")], Files(root));
    }

    [Fact]
    public async Task ExistingDestinationIsNeverOverwrittenByDefault()
    {
        GenerationRoot root = Root;
        await Put(root, "file.txt", "Original.");

        await Assert.ThrowsAsync<IOException>(() => new AtomicFileWriter().WriteAsync(root, "file.txt", "Unapproved replacement.",
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Original.", await File.ReadAllTextAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve("file.txt")], Files(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitCreateOrReplacePromotesCompleteContent(bool exists)
    {
        GenerationRoot root = Root;
        if (exists) await Put(root, "file.txt", "Original long content that must disappear entirely.");

        await new AtomicFileWriter().WriteAsync(root, "file.txt", "New.", AtomicFileWriteMode.CreateOrReplace, TestContext.Current.CancellationToken);

        Assert.Equal("New.", await File.ReadAllTextAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve("file.txt")], Files(root));
    }

    [Theory]
    [InlineData((int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData((int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task FailureBeforePromotionPreservesDestinationAndRemovesTemporary(int checkpointValue)
    {
        AtomicWriteCheckpoint stage = (AtomicWriteCheckpoint)checkpointValue;
        GenerationRoot root = Root;
        await Put(root, "file.txt", "Original.");
        bool reached = false;
        AtomicFileWriter writer = new(checkpoint =>
        {
            if (checkpoint == stage)
            {
                reached = true;
                Assert.Equal("Original.", File.ReadAllText(root.Resolve("file.txt")));
                Assert.Single(Directory.EnumerateFiles(root.FullPath, ".sitefactory-*.tmp"));
                throw new IOException("Injected pre-promotion failure.");
            }
        });

        IOException error = await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(root, "file.txt", new string('x', 20000),
            AtomicFileWriteMode.CreateOrReplace, TestContext.Current.CancellationToken));

        Assert.Equal("Injected pre-promotion failure.", error.Message);
        Assert.True(reached);
        Assert.Equal("Original.", await File.ReadAllTextAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve("file.txt")], Files(root));
    }

    [Fact]
    public async Task FailureDoesNotPublishAnInitiallyAbsentDestination()
    {
        GenerationRoot root = Root;
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed) throw new IOException("Injected failure.");
        });

        await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(root, "file.txt", "Complete temporary content.",
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(Files(root));
    }

    [Fact]
    public async Task CancellationBeforeStartingCreatesNothing()
    {
        GenerationRoot root = Root;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AtomicFileWriter().WriteAsync(root, "nested/file.txt", "Never written.",
            cancellationToken: cancellation.Token));

        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData((int)AtomicWriteCheckpoint.TemporaryCreated)]
    [InlineData((int)AtomicWriteCheckpoint.ChunkWritten)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed)]
    public async Task CancellationBeforePromotionPreservesOriginalAndCleansTemporary(int checkpointValue)
    {
        AtomicWriteCheckpoint stage = (AtomicWriteCheckpoint)checkpointValue;
        GenerationRoot root = Root;
        await Put(root, "file.txt", "Original.");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        AtomicFileWriter writer = new(checkpoint =>
        {
            if (checkpoint == stage) cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(root, "file.txt", new string('x', 20000),
            AtomicFileWriteMode.CreateOrReplace, cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal("Original.", await File.ReadAllTextAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve("file.txt")], Files(root));
    }

    [Fact]
    public async Task ConcurrentDestinationCreationCannotBypassCreateNew()
    {
        GenerationRoot root = Root;
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed) File.WriteAllText(root.Resolve("file.txt"), "Other complete file.");
        });

        await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(root, "file.txt", "Writer content.",
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Other complete file.", await File.ReadAllTextAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve("file.txt")], Files(root));
    }

    [Fact]
    public async Task Utf8HasNoBomAndLineEndingsAreDeterministic()
    {
        GenerationRoot root = Root;
        AtomicFileWriter writer = new();
        byte[] expected = Encoding.UTF8.GetBytes("Équipe 東京 🧪\nSecond line\nLast line\n");
        await writer.WriteAsync(root, "first.txt", "Équipe 東京 🧪\r\nSecond line\rLast line\n", cancellationToken: TestContext.Current.CancellationToken);
        await writer.WriteAsync(root, "second.txt", "Équipe 東京 🧪\nSecond line\nLast line\n", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, await File.ReadAllBytesAsync(root.Resolve("first.txt"), TestContext.Current.CancellationToken));
        Assert.Equal(expected, await File.ReadAllBytesAsync(root.Resolve("second.txt"), TestContext.Current.CancellationToken));
        Assert.Equal(2, Files(root).Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("No final newline")]
    [InlineData("Two final newlines\n\n")]
    public async Task FinalNewlinesAndEmptyContentAreNotInventedOrTrimmed(string content)
    {
        GenerationRoot root = Root;
        await new AtomicFileWriter().WriteAsync(root, "file.txt", content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.UTF8.GetBytes(content), await File.ReadAllBytesAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("équipe/東京.txt")]
    public async Task NativeNamesSupportShortAndUnicodeComponents(string path)
    {
        GenerationRoot root = Root;
        await new AtomicFileWriter().WriteAsync(root, path, "Complete.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Complete.", await File.ReadAllTextAsync(root.Resolve(path), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve(path)], Files(root));
    }

    [Fact]
    public async Task MalformedUtf16IsRejectedBeforeAnyFilesystemMutation()
    {
        GenerationRoot root = Root;
        await Assert.ThrowsAsync<EncoderFallbackException>(() => new AtomicFileWriter().WriteAsync(root, "file.txt", new string((char)0xD800, 1),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task ExistingDirectoryCannotBeReplacedWithAFile()
    {
        GenerationRoot root = Root;
        Directory.CreateDirectory(root.Resolve("directory"));
        await Assert.ThrowsAsync<IOException>(() => new AtomicFileWriter().WriteAsync(root, "directory", "Not a directory.",
            AtomicFileWriteMode.CreateOrReplace, TestContext.Current.CancellationToken));

        Assert.True(Directory.Exists(root.Resolve("directory")));
        Assert.Empty(Files(root));
    }

    [Fact]
    public async Task AnOpenReaderRetainsTheOldCompleteFileWhileThePathGetsTheNewCompleteFile()
    {
        GenerationRoot root = Root;
        string original = new('a', 16000);
        string replacement = new('b', 20000);
        await Put(root, "file.txt", original);
        await using FileStream reader = new(root.Resolve("file.txt"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        await new AtomicFileWriter().WriteAsync(root, "file.txt", replacement, AtomicFileWriteMode.CreateOrReplace, TestContext.Current.CancellationToken);

        using StreamReader textReader = new(reader, leaveOpen: true);
        Assert.Equal(original, await textReader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Equal(replacement, await File.ReadAllTextAsync(root.Resolve("file.txt"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("ancestor")]
    [InlineData("root")]
    [InlineData("parent")]
    [InlineData("destination")]
    public async Task ExistingLinksAreRejectedWithoutWritingTheirTargets(string location)
    {
        string outside = PrepareOutside();
        GenerationRoot root = Root;
        string logicalPath = "file.txt";
        string link;
        switch (location)
        {
            case "ancestor":
                link = Path.Combine(sandbox, "linked-ancestor");
                root = new(Path.Combine(link, "generation"));
                break;
            case "root":
                link = root.FullPath;
                break;
            case "parent":
                Directory.CreateDirectory(root.FullPath);
                link = root.Resolve("linked-parent");
                logicalPath = "linked-parent/file.txt";
                break;
            default:
                Directory.CreateDirectory(root.FullPath);
                link = root.Resolve("file.txt");
                break;
        }

        DirectoryLinkFixture.Create(link, outside);
        await Assert.ThrowsAsync<IOException>(() => new AtomicFileWriter().WriteAsync(root, logicalPath, "Must not escape.",
            AtomicFileWriteMode.CreateOrReplace, TestContext.Current.CancellationToken));

        AssertOutsideUnchanged(outside);
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
    }

    [Fact]
    public async Task ALinkInsertedAtTheDestinationBeforePromotionIsRejectedAndTemporaryIsRemoved()
    {
        GenerationRoot root = Root;
        string outside = PrepareOutside();
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed) DirectoryLinkFixture.Create(root.Resolve("file.txt"), outside);
        });

        await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(root, "file.txt", "Must not escape.",
            AtomicFileWriteMode.CreateOrReplace, TestContext.Current.CancellationToken));

        AssertOutsideUnchanged(outside);
        Assert.Empty(Directory.GetFiles(root.FullPath));
    }

    [Theory]
    [InlineData((int)AtomicWriteCheckpoint.DirectoriesOpened, false)]
    [InlineData((int)AtomicWriteCheckpoint.TemporaryCreated, false)]
    [InlineData((int)AtomicWriteCheckpoint.ChunkWritten, false)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed, false)]
    [InlineData((int)AtomicWriteCheckpoint.DirectoriesOpened, true)]
    [InlineData((int)AtomicWriteCheckpoint.TemporaryCreated, true)]
    [InlineData((int)AtomicWriteCheckpoint.ChunkWritten, true)]
    [InlineData((int)AtomicWriteCheckpoint.ContentFlushed, true)]
    public async Task DirectorySubstitutionIsBlockedOrDetectedWithoutFollowingTheReplacementLink(int checkpointValue, bool moveRoot)
    {
        GenerationRoot root = Root;
        string outside = PrepareOutside();
        string originalParent = moveRoot ? root.FullPath : root.Resolve("parent");
        string movedParent = Path.Combine(sandbox, "moved-directory");
        bool reached = false;
        bool moved = false;
        AtomicFileWriter writer = new(stage =>
        {
            if ((int)stage != checkpointValue) return;
            reached = true;
            try
            {
                Directory.Move(originalParent, movedParent);
                moved = true;
            }
            catch (IOException) when (OperatingSystem.IsWindows())
            {
                // Windows handles deny delete-sharing, so the rename itself must be blocked.
                Assert.True(Directory.Exists(originalParent));
            }

            if (moved) DirectoryLinkFixture.Create(originalParent, outside);
        });

        Exception? error = await Record.ExceptionAsync(() => writer.WriteAsync(root, "parent/file.txt", "Complete content.",
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(reached);
        AssertOutsideUnchanged(outside);
        if (OperatingSystem.IsWindows())
        {
            Assert.False(moved);
            Assert.Null(error);
            Assert.Equal("Complete content.", await File.ReadAllTextAsync(root.Resolve("parent/file.txt"), TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.True(moved);
            Assert.IsType<IOException>(error);
            Assert.Empty(Directory.GetFiles(movedParent, "*", SearchOption.AllDirectories));
        }
    }

    [Theory]
    [InlineData(".gitignore")]
    [InlineData(".gitattributes")]
    [InlineData(".github/readme.txt")]
    public async Task GitAdjacentNamesRemainWritable(string path)
    {
        GenerationRoot root = Root;
        await new AtomicFileWriter().WriteAsync(root, path, "Fixture only.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Fixture only.", await File.ReadAllTextAsync(root.Resolve(path), TestContext.Current.CancellationToken));
        Assert.Equal([root.Resolve(path)], Files(root));
    }

    [Fact]
    public async Task UnknownWriteModeIsRejectedBeforeCreatingTheRoot()
    {
        GenerationRoot root = Root;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new AtomicFileWriter().WriteAsync(root, "file.txt", "Not written.",
            (AtomicFileWriteMode)99, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task MissingAncestorsOutsideTheAuthorizedRootAreNotCreated()
    {
        string missingParent = Path.Combine(sandbox, "not-authorized");
        GenerationRoot root = new(Path.Combine(missingParent, "generation"));

        await Assert.ThrowsAsync<IOException>(() => new AtomicFileWriter().WriteAsync(root, "file.txt", "Not written.",
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(missingParent));
    }

    [Fact]
    public async Task GeneratedFilesAndDirectoriesDoNotGrantAccessToOtherUsers()
    {
        GenerationRoot root = Root;
        await new AtomicFileWriter().WriteAsync(root, "nested/file.txt", "Private content.", cancellationToken: TestContext.Current.CancellationToken);
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity user = WindowsIdentity.GetCurrent();
            FileSecurity permissions = new FileInfo(root.Resolve("nested/file.txt")).GetAccessControl();
            Assert.True(permissions.AreAccessRulesProtected);
            foreach (FileSystemAccessRule rule in permissions.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                Assert.Equal(user.User, rule.IdentityReference);
            }
            DirectorySecurity directoryPermissions = new DirectoryInfo(root.Resolve("nested")).GetAccessControl();
            Assert.True(directoryPermissions.AreAccessRulesProtected);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(root.Resolve("nested/file.txt")));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(root.Resolve("nested")));
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/outside.txt")]
    [InlineData("C:\\outside.txt")]
    [InlineData("nested\\file.txt")]
    [InlineData(".git/config")]
    [InlineData("nested/.GiT/hooks/pre-commit")]
    public async Task LogicalBoundaryRemainsEnforcedBeforeCreatingAnyDirectory(string path)
    {
        GenerationRoot root = Root;
        await Assert.ThrowsAsync<ArgumentException>(() => new AtomicFileWriter().WriteAsync(root, path, "Rejected.",
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(root.FullPath));
    }

    public void Dispose()
    {
        // Only this test's freshly created sandbox, never a computed client directory or shared temp root.
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-atomic-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        RemoveLinks(sandbox);
        Directory.Delete(sandbox, recursive: true);
    }

    private static void RemoveLinks(string directory)
    {
        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
            {
                // Remove only the junction/link itself, never recurse into its target or unmount a volume.
                Directory.Delete(child, recursive: false);
            }
            else
            {
                RemoveLinks(child);
            }
        }
    }

    private static string[] Files(GenerationRoot root) => Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories);

    private string PrepareOutside()
    {
        string path = Path.Combine(sandbox, "outside");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker.txt"), "Unchanged outside content.");
        return path;
    }

    private static void AssertOutsideUnchanged(string path)
    {
        Assert.Equal([Path.Combine(path, "marker.txt")], Directory.GetFileSystemEntries(path));
        Assert.Equal("Unchanged outside content.", File.ReadAllText(Path.Combine(path, "marker.txt")));
    }

    private static async Task Put(GenerationRoot root, string logicalPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(root.Resolve(logicalPath))!);
        await File.WriteAllTextAsync(root.Resolve(logicalPath), content, TestContext.Current.CancellationToken);
    }
}
