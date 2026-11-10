using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class SiteFactoryLockTests : IDisposable
{
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-lock-tests-").FullName;

    private GenerationRoot Root => new(Path.Combine(sandbox, "generation"));

    [Fact]
    public async Task RealResolvedTemplateProducesTheGoldenCanonicalDocument()
    {
        TemplateManifest template = await Resolve();
        // The golden input pins a fictitious generator version; it must not vary with the checkout commit.
        SiteFactoryLock candidate = new(new("0.1.0-dev"), Configuration(), template);
        byte[] expected = await System.IO.File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "sitefactory-lock", "valid-minimal", "sitefactory.lock.json"),
            TestContext.Current.CancellationToken);

        byte[] actual = SiteFactoryLockJsonSerializer.Serialize(candidate);

        Assert.Equal(expected, actual);
        Assert.Equal(actual, SiteFactoryLockJsonSerializer.Serialize(candidate));
        Assert.Equal((byte)'{', actual[0]);
        Assert.Equal((byte)'}', actual[^2]);
        Assert.Equal((byte)'\n', actual[^1]);
        string json = new UTF8Encoding(false, true).GetString(actual);
        Assert.DoesNotContain('\r', json);
        Assert.DoesNotContain('\t', json);
        using JsonDocument document = JsonDocument.Parse(actual);
        Assert.Equal(["schemaVersion", "siteFactoryVersion", "configurationSchemaVersion", "product", "template", "modules"],
            document.RootElement.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(["id", "version", "manifestSchemaVersion"],
            document.RootElement.GetProperty("template").EnumerateObject().Select(static property => property.Name));
        Assert.Empty(document.RootElement.GetProperty("modules").EnumerateArray());
        Assert.False(Directory.Exists(Root.FullPath));
    }

    [Fact]
    public async Task CandidateFactoryRetainsTheExecutingAssemblyVersionIncludingBuildIdentity()
    {
        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
        string? actualVersion = typeof(SiteFactoryLockWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.NotNull(actualVersion);
        Assert.Equal(actualVersion, candidate.SiteFactoryVersion.Value);
        using JsonDocument document = JsonDocument.Parse(SiteFactoryLockJsonSerializer.Serialize(candidate));
        Assert.Equal(actualVersion, document.RootElement.GetProperty("siteFactoryVersion").GetString());
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public async Task CultureAndNonStructuralConfigurationDoNotAffectCanonicalBytes(string cultureName)
    {
        TemplateManifest template = await Resolve();
        SiteFactoryLock first = new(new("0.1.0-dev+abc123"), Configuration(), template);
        ClientConfigurationV1 different = new(1, new("different-client", sandbox),
            new("documentation", "documentation-minimal", "en-US", "https://other.example.test/private-marker"),
            new([]), new("docker-compose-vps"), new(true, false));
        TemplateManifest reordered = new(template.TemplateId, template.TemplateVersion, template.SupportedProductIds.Reverse(), template.Files.Reverse());
        SiteFactoryLock second = new(first.SiteFactoryVersion, different, reordered);
        byte[] expected = SiteFactoryLockJsonSerializer.Serialize(first);
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            byte[] actual = SiteFactoryLockJsonSerializer.Serialize(second);
            Assert.Equal(expected, actual);
            string json = Encoding.UTF8.GetString(actual);
            Assert.DoesNotContain(sandbox, json, StringComparison.Ordinal);
            Assert.DoesNotContain("different-client", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-marker", json, StringComparison.Ordinal);
            Assert.DoesNotContain("en-US", json, StringComparison.Ordinal);
            Assert.DoesNotContain("initializeLocalRepository", json, StringComparison.Ordinal);
            Assert.DoesNotContain("timestamp", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public async Task SecondGenerationKeepsBytesIdentityAndModificationTimeWithoutStaging()
    {
        GenerationRoot root = Root;
        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
        FileGenerationResult first = await new SiteFactoryLockWriter().WriteAsync(root, candidate, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Created, first.Status);
        Assert.Equal("sitefactory-lock", first.FileId.Value);
        Assert.Null(first.Error);
        byte[] expected = SiteFactoryLockJsonSerializer.Serialize(candidate);
        Assert.Equal(expected, await Read(root));
        DateTime sentinel = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(root.Resolve(SiteFactoryLockWriter.FileName), sentinel);
        using AtomicWriteSession inspection = AtomicWriteSession.Open(root, SiteFactoryLockWriter.FileName, TestContext.Current.CancellationToken, createDirectories: false);
        using AtomicFileSnapshot? before = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        AtomicFileWriter noWrites = new(stage => Assert.DoesNotContain(stage,
            new[] { AtomicWriteCheckpoint.TemporaryCreated, AtomicWriteCheckpoint.ChunkWritten, AtomicWriteCheckpoint.ContentFlushed }));

        FileGenerationResult second = await new SiteFactoryLockWriter(new GeneratedFileApplier(noWrites)).WriteAsync(root, candidate,
            PlannedFileAction.Create, Hash(candidate), TestContext.Current.CancellationToken);

        using AtomicFileSnapshot? after = await inspection.ReadDestinationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(FileGenerationStatus.Unchanged, second.Status);
        Assert.True(AtomicFileSnapshot.Matches(before, after));
        Assert.Equal(expected, await Read(root));
        Assert.Equal(sentinel, System.IO.File.GetLastWriteTimeUtc(root.Resolve(SiteFactoryLockWriter.FileName)));
        AssertOnlyLock(root);
    }

    [Fact]
    public async Task DifferentPhysicalRootsProduceExactlyTheSameLockBytes()
    {
        GenerationRoot first = Root;
        GenerationRoot second = new(Path.Combine(sandbox, "other-root"));
        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
        SiteFactoryLockWriter writer = new();
        Assert.Equal(FileGenerationStatus.Created, (await writer.WriteAsync(first, candidate, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken)).Status);
        Assert.Equal(FileGenerationStatus.Created, (await writer.WriteAsync(second, candidate, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken)).Status);

        Assert.Equal(await Read(first), await Read(second));
        string json = Encoding.UTF8.GetString(await Read(first));
        Assert.DoesNotContain(sandbox, json, StringComparison.Ordinal);
        Assert.DoesNotContain("other-root", json, StringComparison.Ordinal);
        AssertOnlyLock(first);
        AssertOnlyLock(second);
    }

    [Fact]
    public async Task AResolvedTemplateVersionChangeUpdatesOnlyWithAnExplicitActionAndTrustedReference()
    {
        GenerationRoot root = Root;
        TemplateManifest original = await Resolve();
        TemplateManifest changed = new(original.TemplateId, new("9.9.9"), original.SupportedProductIds, original.Files);
        SiteFactoryLock first = SiteFactoryLockWriter.CreateCandidate(Configuration(), original);
        SiteFactoryLock second = SiteFactoryLockWriter.CreateCandidate(Configuration(), changed);
        SiteFactoryLockWriter writer = new();
        await writer.WriteAsync(root, first, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken);
        byte[] before = await Read(root);
        byte[] after = SiteFactoryLockJsonSerializer.Serialize(second);
        Assert.NotEqual(before, after);

        FileGenerationResult noReference = await writer.WriteAsync(root, second, PlannedFileAction.Update, cancellationToken: TestContext.Current.CancellationToken);
        AssertConflict(noReference, "SF-GEN-FILE-014");
        Assert.Equal(before, await Read(root));
        FileGenerationResult noUpdate = await writer.WriteAsync(root, second, PlannedFileAction.Create, Hash(first), TestContext.Current.CancellationToken);
        AssertConflict(noUpdate, "SF-GEN-FILE-014");
        Assert.Equal(before, await Read(root));

        FileGenerationResult updated = await writer.WriteAsync(root, second, PlannedFileAction.Update, Hash(first), TestContext.Current.CancellationToken);

        Assert.Equal(FileGenerationStatus.Updated, updated.Status);
        Assert.Equal(after, await Read(root));
        using JsonDocument document = JsonDocument.Parse(after);
        Assert.Equal("9.9.9", document.RootElement.GetProperty("template").GetProperty("version").GetString());
        AssertOnlyLock(root);
    }

    [Fact]
    public async Task ManualLockEditsAreNotOverwritten()
    {
        GenerationRoot root = Root;
        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
        SiteFactoryLockWriter writer = new();
        await writer.WriteAsync(root, candidate, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken);
        byte[] edited = Encoding.UTF8.GetBytes("{\r\n  \"manualChange\": true\r\n}\r\n");
        await System.IO.File.WriteAllBytesAsync(root.Resolve(SiteFactoryLockWriter.FileName), edited, TestContext.Current.CancellationToken);

        FileGenerationResult result = await writer.WriteAsync(root, candidate, PlannedFileAction.Update, Hash(candidate), TestContext.Current.CancellationToken);

        AssertConflict(result, "SF-GEN-FILE-004");
        Assert.Equal(edited, await Read(root));
        AssertOnlyLock(root);
    }

    [Fact]
    public async Task UnresolvedModulesFailBeforeAnyLockOrDirectoryCanBeCreated()
    {
        GenerationRoot root = Root;
        TemplateManifest template = await Resolve();
        ClientConfigurationV1 configuration = Configuration(["contact-form"]);
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(nameof(configuration), () =>
            new SiteFactoryLockWriter().WriteAsync(root, SiteFactoryLockWriter.CreateCandidate(configuration, template),
                PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("unresolved modules", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task AFalseGeneratorVersionCannotBeWritten()
    {
        GenerationRoot root = Root;
        SiteFactoryLock candidate = new(new("999.0.0"), Configuration(), await Resolve());
        await Assert.ThrowsAsync<ArgumentException>("candidate", () => new SiteFactoryLockWriter().WriteAsync(root, candidate, PlannedFileAction.Create,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(PlannedFileAction.Preserve)]
    [InlineData(PlannedFileAction.Conflict)]
    public async Task NonWritingActionsAreNotRecalculatedAsCreation(PlannedFileAction action)
    {
        GenerationRoot root = Root;
        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
        FileGenerationResult result = await new SiteFactoryLockWriter().WriteAsync(root, candidate, action,
            cancellationToken: TestContext.Current.CancellationToken);
        AssertConflict(result, "SF-GEN-FILE-014");
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public async Task PreCancelledWriteDoesNotCreateAnything()
    {
        GenerationRoot root = Root;
        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SiteFactoryLockWriter().WriteAsync(root, candidate,
            PlannedFileAction.Create, cancellationToken: cancellation.Token));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationBeforePromotionKeepsTheOriginalAndCleansTemporary(bool cancel)
    {
        GenerationRoot root = Root;
        TemplateManifest original = await Resolve();
        SiteFactoryLock first = SiteFactoryLockWriter.CreateCandidate(Configuration(), original);
        SiteFactoryLock second = SiteFactoryLockWriter.CreateCandidate(Configuration(),
            new(original.TemplateId, new("9.9.9"), original.SupportedProductIds, original.Files));
        await new SiteFactoryLockWriter().WriteAsync(root, first, PlannedFileAction.Create, cancellationToken: TestContext.Current.CancellationToken);
        byte[] before = await Read(root);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        AtomicFileWriter failing = new(stage =>
        {
            if (stage != AtomicWriteCheckpoint.ContentFlushed) return;
            if (cancel) cancellation.Cancel();
            else throw new IOException("Private failure detail must not be returned.");
        });
        SiteFactoryLockWriter writer = new(new GeneratedFileApplier(failing));

        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(root, second,
                PlannedFileAction.Update, Hash(first), cancellation.Token));
        }
        else
        {
            FileGenerationResult result = await writer.WriteAsync(root, second, PlannedFileAction.Update, Hash(first), cancellation.Token);
            Assert.Equal(FileGenerationStatus.Failed, result.Status);
            Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
            Assert.DoesNotContain("Private failure detail", result.Error!.Message, StringComparison.Ordinal);
        }

        Assert.Equal(before, await Read(root));
        AssertOnlyLock(root);
    }

    [Fact]
    public async Task LockDestinationLinksAreRefusedWithoutTouchingTheirTarget()
    {
        GenerationRoot root = Root;
        Directory.CreateDirectory(root.FullPath);
        string outside = Path.Combine(sandbox, "outside");
        Directory.CreateDirectory(outside);
        string marker = Path.Combine(outside, "marker.txt");
        await System.IO.File.WriteAllTextAsync(marker, "Outside content.", TestContext.Current.CancellationToken);
        string link = root.Resolve(SiteFactoryLockWriter.FileName);
        DirectoryLinkFixture.Create(link, outside);
        try
        {
            SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(Configuration(), await Resolve());
            FileGenerationResult result = await new SiteFactoryLockWriter().WriteAsync(root, candidate, PlannedFileAction.Create,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(FileGenerationStatus.Failed, result.Status);
            Assert.Equal("SF-GEN-FILE-015", result.Error?.Code.Value);
            Assert.Equal([marker], Directory.GetFileSystemEntries(outside));
            Assert.Equal("Outside content.", await System.IO.File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(link, recursive: false);
        }
    }

    [Fact]
    public void NullSerializationInputIsRejected() =>
        Assert.Throws<ArgumentNullException>("candidate", () => SiteFactoryLockJsonSerializer.Serialize(null!));

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-lock-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }

    private static ClientConfigurationV1 Configuration(IEnumerable<string>? modules = null) =>
        new(1, new("acme-services", "Acme Services"), new("documentation", "documentation-minimal", "fr-FR", "https://www.example.test"),
            new(modules ?? []), new("docker-compose-vps"), new(true, false));

    private static async Task<TemplateManifest> Resolve()
    {
        TemplateResolutionResult resolution = await new TemplateResolver(new DocumentationModule(new TemplateManifestJsonReader())).ResolveAsync(Configuration(), TestContext.Current.CancellationToken);
        Assert.True(resolution.IsSuccess);
        Assert.NotNull(resolution.Template);
        return resolution.Template;
    }

    private static GeneratedContentHash Hash(SiteFactoryLock candidate) =>
        new GeneratedFileContent(Encoding.UTF8.GetString(SiteFactoryLockJsonSerializer.Serialize(candidate))).Hash;

    private static Task<byte[]> Read(GenerationRoot root) =>
        System.IO.File.ReadAllBytesAsync(root.Resolve(SiteFactoryLockWriter.FileName), TestContext.Current.CancellationToken);

    private static void AssertOnlyLock(GenerationRoot root)
    {
        Assert.Equal([root.Resolve(SiteFactoryLockWriter.FileName)], Directory.GetFiles(root.FullPath, "*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetDirectories(root.FullPath, "*", SearchOption.AllDirectories));
    }

    private static void AssertConflict(FileGenerationResult result, string code)
    {
        Assert.Equal(FileGenerationStatus.Conflict, result.Status);
        Assert.Equal(code, result.Error?.Code.Value);
    }
}
