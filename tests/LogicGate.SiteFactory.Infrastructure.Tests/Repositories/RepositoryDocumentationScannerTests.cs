using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Application.Repositories;
using LogicGate.SiteFactory.Infrastructure.Repositories;
using LogicGate.SiteFactory.Infrastructure.Tests.Generation;
using LogicGate.SiteFactory.Modules.Documentation.Model;
using LogicGate.SiteFactory.Modules.Documentation.Scanning;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Repositories;

public sealed partial class RepositoryDocumentationScannerTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-repository-tests-").FullName;
    private readonly List<string> links = [];
    private string Root => Path.Combine(sandbox, "repository");
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static RepositoryDocumentationScanner Scanner() => new(new LocalGitRepositoryReader());

    [Fact]
    public async Task RealScannerAndConfinedReaderBuildADeterministicModelWithoutGeneratingASite()
    {
        await InitAsync();
        Put("README.md", "Introduction\r\n\r\n# Accueil\r\n[Guide](docs/guide.md)");
        Put("docs/guide.md", "Guide\n=====\n[Home](../README.md#accueil)");
        Put("docs/architecture/overview.md", "Content without a heading.");
        Dictionary<string, string> before = Snapshot();
        DocumentationScanResult scan = await Scanner().ScanAsync(Root, Token);
        Assert.True(scan.IsSuccess);
        using IReadOnlyRepository repository = await new LocalGitRepositoryReader().OpenAsync(Root, Token);
        DocumentationModelResult first = await DocumentationModelLoader.LoadAsync(scan, repository, Token);
        DocumentationModelResult second = await DocumentationModelLoader.LoadAsync(scan, repository, Token);
        Assert.True(first.IsSuccess, first.Diagnostic?.Code);
        Assert.True(second.IsSuccess, second.Diagnostic?.Code);
        Assert.Equal("Accueil", first.Site!.Home!.Title); // Not the scanner's first-line-only candidate.
        Assert.Equal(["/", "/architecture/overview", "/guide"], first.Site.Pages.Select(static page => page.Route));
        Assert.Equal("/guide", Assert.Single(first.Site.Home.InternalLinks).TargetRoute);
        Assert.Equal("Architecture", Assert.Single(first.Site.Navigation.Sections).Title);
        Assert.Equal(first.Site.Pages.Select(static page => (page.Route, page.Title, page.Markdown)),
            second.Site!.Pages.Select(static page => (page.Route, page.Title, page.Markdown)));
        Assert.Equal(before.OrderBy(static pair => pair.Key), Snapshot().OrderBy(static pair => pair.Key));
    }

    [Fact]
    public async Task ModelLoadingRejectsASourceReplacedByALinkAfterTheScan()
    {
        await InitAsync();
        Put("README.md", "# Valid first");
        Put("docs/page.md", "# Inside");
        DocumentationScanResult scan = await Scanner().ScanAsync(Root, Token);
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "page.md"), "# OUTSIDE-SENTINEL");
        Directory.Move(Path.Combine(Root, "docs"), Path.Combine(Root, "parked"));
        Link(Path.Combine(Root, "docs"), outside);
        using IReadOnlyRepository repository = await new LocalGitRepositoryReader().OpenAsync(Root, Token);
        DocumentationModelResult result = await DocumentationModelLoader.LoadAsync(scan, repository, Token);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Site);
        Assert.Equal("SF-DOC-MODEL-006", result.Diagnostic!.Code);
        Assert.DoesNotContain("OUTSIDE-SENTINEL", result.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedInventoryOrCancelledReadDoesNotProduceAPartialModel()
    {
        DocumentationScanResult failed = await Scanner().ScanAsync(Root, Token);
        await InitAsync();
        Put("README.md", "# Inside");
        using IReadOnlyRepository repository = await new LocalGitRepositoryReader().OpenAsync(Root, Token);
        Assert.Equal("SF-DOC-MODEL-005", (await DocumentationModelLoader.LoadAsync(failed, repository, Token)).Diagnostic!.Code);
        DocumentationScanResult scan = await Scanner().ScanAsync(Root, Token);
        using CancellationTokenSource cancellation = new();
        using IReadOnlyRepository cancelling = new InterceptingRepository(repository, cancellation.Cancel, null);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DocumentationModelLoader.LoadAsync(scan, cancelling, cancellation.Token));
    }

    [Fact]
    public async Task ReadmeOnlyHasStableLogicalMetadataAndLeavesAllFilesIncludingGitUnchanged()
    {
        await InitAsync();
        Put("README.md", "\uFEFF\n# Équipe 東京\r\n\nBody, not returned.");
        Dictionary<string, string> before = Snapshot();
        DocumentationScanResult result = await Scanner().ScanAsync(Root, Token);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Diagnostic);
        Assert.Equal(new("README.md", "README.md", "Équipe 東京", DocumentationSourceType.RootReadme), Assert.Single(result.Documents));
        Assert.Throws<NotSupportedException>(() => ((IList<RepositoryDocument>)result.Documents).Clear());
        Assert.Equal(before.OrderBy(static pair => pair.Key), Snapshot().OrderBy(static pair => pair.Key));
    }

    [Fact]
    public async Task NestedDocsAndExplicitReadmeVariantsHaveAnOrdinalCultureIndependentOrder()
    {
        await InitAsync();
        foreach (string name in new[] { "docs/z/last.md", "README.fr-FR.md", "docs/a/README.md", "docs/intro.md", "README.md", "README.eng.md" })
            Put(name, "# Title\n");
        foreach (string name in new[] { "README", "README.txt", "README.md.bak", "README.backup.md", "README..md",
                     "README.fr-.md", "elsewhere/page.md", "docs/plain.txt", "docs/page.MD", "docs-old/page.md" })
            Put(name, "Must not be read.");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            DocumentationScanResult first = await Scanner().ScanAsync(Root, Token);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            DocumentationScanResult second = await Scanner().ScanAsync(Root, Token);
            Assert.True(first.IsSuccess);
            Assert.Equal(first.Documents, second.Documents);
            Assert.Equal(["README.eng.md", "README.fr-FR.md", "README.md", "docs/a/README.md", "docs/intro.md", "docs/z/last.md"],
                first.Documents.Select(static item => item.RelativePath));
            Assert.All(first.Documents.Where(static item => item.RelativePath.StartsWith("docs/", StringComparison.Ordinal)),
                static item => Assert.Equal(DocumentationSourceType.DocumentationPage, item.SourceType));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("readme.md")]
    [InlineData("README.MD")]
    [InlineData("DOCS/other.md")]
    public async Task SelectionUsesTheActualOrdinalNameOnEveryPlatform(string path)
    {
        await InitAsync();
        Put(path, "# Not selected");
        DocumentationScanResult result = await Scanner().ScanAsync(Root, Token);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Documents);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("BIN")]
    [InlineData("node_modules")]
    [InlineData(".sitefactory")]
    [InlineData(".secrets")]
    [InlineData("secrets")]
    public async Task ExcludedDirectoriesAreNeverTraversedEvenWhenTheyContainLinks(string excluded)
    {
        await InitAsync();
        Put("README.md", "# Only source");
        if (excluded != ".git") Put(excluded + "/private.md", "Sensitive fixture.");
        Put(".git/hidden.md", "Not documentation.");
        Put("docs/nested/" + excluded + "/private.md", "Sensitive fixture.");
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "leak.md"), "Secret sentinel.");
        Link(Path.Combine(Root, "docs", excluded), outside);
        DocumentationScanResult result = await Scanner().ScanAsync(Root, Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("README.md", Assert.Single(result.Documents).RelativePath);
    }

    [Fact]
    public async Task AnInitializedRepositoryWithoutDocumentsIsAValidEmptyInventory()
    {
        await InitAsync();
        DocumentationScanResult result = await Scanner().ScanAsync(Root, Token);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Documents);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("not-git")]
    [InlineData("empty-marker")]
    [InlineData("gitfile")]
    [InlineData("invalid-head")]
    [InlineData("subdirectory")]
    public async Task InvalidRootsFailWithoutDiscoveringAnotherRepositoryOrFollowingAGitfile(string scenario)
    {
        string input = Root;
        if (scenario != "absent") Directory.CreateDirectory(Root);
        if (scenario == "empty-marker") Directory.CreateDirectory(Path.Combine(Root, ".git"));
        if (scenario == "gitfile") Put(".git", "gitdir: ../outside\n");
        if (scenario is "invalid-head" or "subdirectory")
        {
            await InitAsync();
            if (scenario == "invalid-head") Put(".git/HEAD", "ref: refs/heads/../../outside\n");
            else input = Directory.CreateDirectory(Path.Combine(Root, "child")).FullName;
        }
        AssertFailure(await Scanner().ScanAsync(input, Token), "SF-DOC-SCAN-001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    [InlineData("../outside")]
    [InlineData("\\\\server\\share")]
    public async Task RootMustBeExplicitAbsoluteAndLocal(string input) =>
        AssertFailure(await Scanner().ScanAsync(input, Token), "SF-DOC-SCAN-001");

    [Fact]
    public async Task RootTraversalIsRejectedBeforeCanonicalizationCanEraseIt()
    {
        await InitAsync();
        AssertFailure(await Scanner().ScanAsync(Path.Combine(Root, "..", "repository"), Token), "SF-DOC-SCAN-001");
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("docs/../../outside.md")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\temp\\secret.md")]
    [InlineData("docs\\secret.md")]
    [InlineData("docs//secret.md")]
    [InlineData(".git/HEAD")]
    public async Task ReadBoundaryIndependentlyRejectsTraversalAbsolutePathsAndGitInternals(string path)
    {
        await InitAsync();
        using IReadOnlyRepository repository = await new LocalGitRepositoryReader().OpenAsync(Root, Token);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => repository.ReadUtf8Async(path, 1024, Token));
        Assert.ThrowsAny<ArgumentException>(() => repository.List(path, 10, Token));
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("docs/escape")]
    [InlineData("README.md")]
    [InlineData(".git/HEAD")]
    public async Task SelectedLinksAreRejectedWithoutReturningAnyOutsideDocumentOrPartialResult(string relative)
    {
        await InitAsync();
        Put("README.fr.md", "# Valid source before failure");
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "leak.md"), "# OUTSIDE-SENTINEL");
        string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) File.Delete(path);
        Link(path, outside);
        DocumentationScanResult result = await Scanner().ScanAsync(Root, Token);
        AssertFailure(result, relative.StartsWith(".git", StringComparison.Ordinal) ? "SF-DOC-SCAN-001" : "SF-DOC-SCAN-002");
        Assert.DoesNotContain("OUTSIDE-SENTINEL", result.Diagnostic!.Message, StringComparison.Ordinal);
        Assert.Equal("# OUTSIDE-SENTINEL", File.ReadAllText(Path.Combine(outside, "leak.md")));
    }

    [Fact]
    public async Task ALinkedRootIsNotAcceptedEvenIfTheTargetIsAValidRepository()
    {
        await InitAsync();
        string alias = Path.Combine(sandbox, "alias");
        Link(alias, Root);
        AssertFailure(await Scanner().ScanAsync(alias, Token), "SF-DOC-SCAN-001");
    }

    [Fact]
    public async Task ADirectoryReplacedByALinkAfterDiscoveryCannotEscapeDuringRead()
    {
        await InitAsync();
        Put("docs/page.md", "# Inside");
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "page.md"), "# OUTSIDE-SENTINEL");
        bool replaced = false;
        RepositoryDocumentationScanner scanner = new(new InterceptingReader(beforeRead: () =>
        {
            Directory.Move(Path.Combine(Root, "docs"), Path.Combine(Root, "parked"));
            Link(Path.Combine(Root, "docs"), outside);
            replaced = true;
        }));
        AssertFailure(await scanner.ScanAsync(Root, Token), "SF-DOC-SCAN-002");
        Assert.True(replaced);
    }

    [Theory]
    [InlineData("plain text\n# Not the first line", "page")]
    [InlineData("```\n# Not a heading candidate\n```", "page")]
    [InlineData("\n# Candidate\nbody", "Candidate")]
    [InlineData("", "page")]
    [InlineData("#   \nbody", "page")]
    public async Task TitleIsOnlyAFirstNonblankLineCandidateOrTheFileStem(string content, string expected)
    {
        await InitAsync();
        Put("docs/page.md", content);
        DocumentationScanResult result = await Scanner().ScanAsync(Root, Token);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, Assert.Single(result.Documents).TitleCandidate);
    }

    [Fact]
    public async Task InvalidUtf8OrAnOversizedSourceFailsWithoutAProviderMessage()
    {
        await InitAsync();
        File.WriteAllBytes(Path.Combine(Root, "README.md"), [0xFF, 0xFE]);
        AssertFailure(await Scanner().ScanAsync(Root, Token), "SF-DOC-SCAN-002");
        using (FileStream stream = File.Create(Path.Combine(Root, "README.md"))) stream.SetLength(RepositoryDocumentationScanner.MaximumDocumentBytes + 1);
        AssertFailure(await Scanner().ScanAsync(Root, Token), "SF-DOC-SCAN-002");
    }

    [Fact]
    public async Task DepthAndDocumentCountAreBoundedWithoutPartialSuccess()
    {
        await InitAsync();
        string deep = "docs/" + string.Join('/', Enumerable.Repeat("d", RepositoryDocumentationScanner.MaximumDepth)) + "/page.md";
        Put(deep, "# Too deep");
        AssertFailure(await Scanner().ScanAsync(Root, Token), "SF-DOC-SCAN-003");
        Directory.Delete(Path.Combine(Root, "docs"), recursive: true);
        for (int index = 0; index <= RepositoryDocumentationScanner.MaximumDocuments; index++) Put($"docs/page-{index:D4}.md", "# Page");
        AssertFailure(await Scanner().ScanAsync(Root, Token), "SF-DOC-SCAN-003");
    }

    [Fact]
    public async Task CancellationBeforeAndDuringDiscoveryIsPropagatedAndReleasesTheRoot()
    {
        await InitAsync();
        Put("README.md", "# Page");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Scanner().ScanAsync(Root, cancellation.Token));
        using CancellationTokenSource during = new();
        RepositoryDocumentationScanner scanner = new(new InterceptingReader(afterList: during.Cancel));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync(Root, during.Token));
        Directory.Move(Root, Path.Combine(sandbox, "released"));
    }

    private void AssertFailure(DocumentationScanResult result, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Documents);
        Assert.Equal(code, result.Diagnostic!.Code);
        Assert.DoesNotContain(sandbox, result.Diagnostic.Message, StringComparison.Ordinal);
    }

    private async Task InitAsync()
    {
        ProcessStartInfo start = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        // A developer's GIT_DIR or config must never redirect this fixture outside its temporary root.
        foreach (string key in start.Environment.Keys.Where(static key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(sandbox, "absent-global-config");
        foreach (string argument in new[] { "-c", "init.templateDir=", "init", "--quiet", "--initial-branch=main", Root }) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(Token);
        Task<string> error = process.StandardError.ReadToEndAsync(Token);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(await error);
        _ = await output;
    }

    private void Put(string relativePath, string text)
    {
        string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private void Link(string path, string target)
    {
        DirectoryLinkFixture.Create(path, target);
        links.Add(path);
    }

    private Dictionary<string, string> Snapshot() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(Root, path), static path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    public void Dispose()
    {
        foreach (string link in links) Directory.Delete(link);
        Directory.Delete(sandbox, recursive: true);
    }

    private sealed class InterceptingReader(Action? beforeRead = null, Action? afterList = null) : ILocalRepositoryReader
    {
        public async Task<IReadOnlyRepository> OpenAsync(string rootPath, CancellationToken cancellationToken = default) =>
            new InterceptingRepository(await new LocalGitRepositoryReader().OpenAsync(rootPath, cancellationToken), beforeRead, afterList);
    }

    private sealed class InterceptingRepository(IReadOnlyRepository inner, Action? beforeRead, Action? afterList) : IReadOnlyRepository
    {
        public IReadOnlyList<RepositoryEntry> List(string relativeDirectory, int maximumEntries, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RepositoryEntry> entries = inner.List(relativeDirectory, maximumEntries, cancellationToken);
            afterList?.Invoke();
            return entries.Reverse().ToArray(); // Scanner must not rely on enumeration order.
        }
        public Task<string> ReadUtf8Async(string relativePath, int maximumBytes, CancellationToken cancellationToken = default)
        {
            beforeRead?.Invoke();
            return inner.ReadUtf8Async(relativePath, maximumBytes, cancellationToken);
        }
        public void Dispose() => inner.Dispose();
    }
}
