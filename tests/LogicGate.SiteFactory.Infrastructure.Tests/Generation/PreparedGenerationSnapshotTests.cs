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

public sealed class PreparedGenerationSnapshotTests : IDisposable
{
    private static readonly string[] ContentSources = ["source/a", "source/b"];
    private static readonly string[] SharedSources = ["source/shared", "source/shared"];
    private static readonly string[] SourcesWithLast = ["source/a", "source/b", "source/last"];
    private static readonly string[] FirstSource = ["source/a"];
    private readonly string sandbox = Directory.CreateTempSubdirectory("sitefactory-prepared-tests-").FullName;
    private string Output => Path.Combine(sandbox, "output");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public async Task DocumentationMatchesFrozenHistoricalFilesAndV1ControlBytes(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            ClientConfigurationV1 config = new(1, new("fictitious-documentation", "Fictitious Documentation"),
                new("documentation", "documentation-minimal", "fr-FR", "https://example.test"),
                new([]), new("docker-compose-vps"), new(true, false));
            DocumentationModule module = new(new TemplateManifestJsonReader());
            TemplateManifest manifest = Assert.IsType<TemplateManifest>((await new TemplateResolver(module).ResolveAsync(config, Token)).Template);
            GenerationExecutionRequest request = Request(config, manifest, "plan:fictitious-documentation:documentation:documentation-minimal");
            RecordingProvider provider = new(manifest, config, source => module.ReadAsync(manifest, config, source, Token));

            InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, config, manifest, Output, provider, Token);
            Assert.Equal(InitialGenerationPreparationStatus.Ready, ready.Status);
            Assert.Empty(Directory.GetFileSystemEntries(sandbox));
            Assert.Equal(manifest.Files.Select(static file => file.Source), provider.Sources);
            provider.ForbidReads = true;
            Assert.Equal(GenerationExecutionStatus.Succeeded, (await ready.Executor!.ExecuteAsync(request, Token)).Status);

            Dictionary<string, byte[]> expected = HistoricalFiles();
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), Files().Keys.Order(StringComparer.Ordinal));
            foreach ((string path, byte[] bytes) in expected)
            {
                byte[] actual = await File.ReadAllBytesAsync(Path.Combine(Output, path), Token);
                Assert.Equal(bytes, actual);
                Assert.Equal(Hash(bytes), Hash(actual));
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task EveryDeclaredFileIsReadOnceWithTheOriginalManifestAndNeverReadDuringExecution()
    {
        var inputs = Inputs();
        Dictionary<string, string?> contents = Contents();
        RecordingProvider provider = new(inputs.Template, inputs.Config, source => Task.FromResult(contents[source]));
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, Token);

        Assert.Equal(InitialGenerationPreparationStatus.Ready, ready.Status);
        Assert.Equal(ContentSources, provider.Sources);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
        contents["source/a"] = "Changed after preparation";
        contents["source/b"] = "Changed client seed";
        provider.ForbidReads = true;

        GenerationExecutionResult result = await ready.Executor!.ExecuteAsync(inputs.Request, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(Encoding.UTF8.GetBytes("Équipe 東京 🧪\nSecond\nLast\n"), await Read("a.txt"));
        Assert.Equal(Encoding.UTF8.GetBytes("Client seed\n"), await Read("b.txt"));
        Assert.Equal(ContentSources, provider.Sources);
        Assert.False(File.Exists(Path.Combine(Output, "custom/config.txt")));
        Assert.False(File.Exists(Path.Combine(Output, "protected/config.txt")));
        Assert.DoesNotContain(Files().Keys, static path => path.EndsWith(".tmp", StringComparison.Ordinal));
        using JsonDocument state = JsonDocument.Parse(await Read(GenerationState.FileName));
        Assert.Equal(1, state.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(Hash(await Read("a.txt")), Entry(state, "a").GetProperty("lastGeneratedSha256").GetString());
    }

    [Fact]
    public async Task SharedSourceKeysStillHaveOneReadAndOneSnapshotPerDeclaredFile()
    {
        var original = Inputs();
        TemplateManifest manifest = new(original.Template.TemplateId, original.Template.TemplateVersion, original.Template.SupportedProductIds,
            [new(new("a"), "a.txt", "source/shared", FileGenerationStrategy.Generated),
             new(new("b"), "b.txt", "source/shared", FileGenerationStrategy.Generated)]);
        GenerationExecutionRequest request = Request(original.Config, manifest);
        int reads = 0;
        RecordingProvider provider = new(manifest, original.Config, _ => Task.FromResult<string?>(++reads == 1 ? "First\r\n" : "Second\r"));
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(request, original.Config, manifest, Output, provider, Token);

        Assert.Equal(InitialGenerationPreparationStatus.Ready, ready.Status);
        Assert.Equal(SharedSources, provider.Sources);
        provider.ForbidReads = true;
        Assert.Equal(GenerationExecutionStatus.Succeeded, (await ready.Executor!.ExecuteAsync(request, Token)).Status);
        Assert.Equal(Encoding.UTF8.GetBytes("First\n"), await Read("a.txt"));
        Assert.Equal(Encoding.UTF8.GetBytes("Second\n"), await Read("b.txt"));
        using JsonDocument state = JsonDocument.Parse(await Read(GenerationState.FileName));
        Assert.Equal(Hash(Encoding.UTF8.GetBytes("First\n")), Entry(state, "a").GetProperty("lastGeneratedSha256").GetString());
        Assert.Equal(Hash(Encoding.UTF8.GetBytes("Second\n")), Entry(state, "b").GetProperty("lastGeneratedSha256").GetString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid-utf16")]
    [InlineData("exception")]
    [InlineData("cancelled")]
    public async Task LastSourceFailureOrCancellationHappensBeforeAnyWrite(string failure)
    {
        var original = Inputs();
        TemplateManifest manifest = new(original.Template.TemplateId, original.Template.TemplateVersion, original.Template.SupportedProductIds,
            [.. original.Template.Files, new(new("last"), "zz-last.txt", "source/last", FileGenerationStrategy.Generated)]);
        GenerationExecutionRequest request = Request(original.Config, manifest);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        RecordingProvider provider = new(manifest, original.Config, source =>
        {
            if (source != "source/last") return Task.FromResult(Contents()[source]);
            if (failure == "exception") throw new InvalidOperationException("fixture provider defect");
            if (failure == "cancelled") { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); }
            return Task.FromResult(failure == "missing" ? null : new string((char)0xD800, 1));
        });

        Task<InitialGenerationPreparation> prepare = InitialGenerationExecutor.PrepareAsync(
            request, original.Config, manifest, Output, provider, cancellation.Token);
        if (failure == "missing")
        {
            InitialGenerationPreparation rejected = await prepare;
            Assert.Equal(InitialGenerationPreparationStatus.InvalidTemplate, rejected.Status);
            Assert.Equal("SF-GEN-FILE-005", Assert.Single(rejected.Diagnostics).Code.Value);
        }
        else if (failure == "invalid-utf16") await Assert.ThrowsAsync<EncoderFallbackException>(() => prepare);
        else if (failure == "exception") await Assert.ThrowsAsync<InvalidOperationException>(() => prepare);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => prepare);
        Assert.Equal(SourcesWithLast, provider.Sources);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationPreservesContentBeforePhysicalRootValidation(bool invalidEncoding)
    {
        var inputs = Inputs();
        RecordingProvider provider = new(inputs.Template, inputs.Config,
            _ => Task.FromResult(invalidEncoding ? new string((char)0xD800, 1) : null));
        Task<InitialGenerationPreparation> prepare = InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Path.Combine(sandbox, "..", "unsafe"), provider, Token);
        if (invalidEncoding) await Assert.ThrowsAsync<EncoderFallbackException>(() => prepare);
        else
        {
            InitialGenerationPreparation rejected = await prepare;
            Assert.Equal(InitialGenerationPreparationStatus.InvalidTemplate, rejected.Status);
            Assert.Equal("SF-GEN-FILE-005", Assert.Single(rejected.Diagnostics).Code.Value);
        }
        Assert.Equal(FirstSource, provider.Sources);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Fact]
    public async Task InvalidPlanIsRejectedBeforeAnySourceRead()
    {
        var inputs = Inputs();
        RecordingProvider provider = new(inputs.Template, inputs.Config, _ => throw new InvalidOperationException("Source must not be read"));
        InitialGenerationPreparation rejected = await InitialGenerationExecutor.PrepareAsync(
            new(inputs.Request.Plan, "different-client"), inputs.Config, inputs.Template, Output, provider, Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidPlan, rejected.Status);
        Assert.Equal("SF-GEN-FILE-014", Assert.Single(rejected.Diagnostics).Code.Value);
        Assert.Empty(provider.Sources);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegenerationAppliesThePreparedCandidateAndPreservesClientBytes(bool identicalContent)
    {
        var inputs = Inputs();
        Dictionary<string, string?> contents = Contents();
        RecordingProvider provider = new(inputs.Template, inputs.Config, source => Task.FromResult(contents[source]));
        InitialGenerationPreparation first = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, (await first.Executor!.ExecuteAsync(inputs.Request, Token)).Status);
        byte[] customization = [255, 128, 13, 10, 13];
        await File.WriteAllBytesAsync(Path.Combine(Output, "b.txt"), customization, Token);
        File.SetLastWriteTimeUtc(Path.Combine(Output, "a.txt"), new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        DateTime generatedTime = File.GetLastWriteTimeUtc(Path.Combine(Output, "a.txt"));
        DateTime clientTime = File.GetLastWriteTimeUtc(Path.Combine(Output, "b.txt"));
        byte[] lockBefore = await Read(SiteFactoryLockWriter.FileName);
        contents["source/a"] = identicalContent ? "Équipe 東京 🧪\nSecond\nLast\n" : "Updated\r\nÉté\r";
        contents["source/b"] = "Unused new seed";
        byte[] expected = Encoding.UTF8.GetBytes(identicalContent ? "Équipe 東京 🧪\nSecond\nLast\n" : "Updated\nÉté\n");
        provider.Sources.Clear();

        InitialGenerationPreparation second = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, second.Status);
        Assert.Equal(ContentSources, provider.Sources);
        contents["source/a"] = "Poison after preparation";
        provider.ForbidReads = true;
        GenerationExecutionResult result = await second.Executor!.ExecuteAsync(inputs.Request, Token);

        Assert.Equal(GenerationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(identicalContent ? FileGenerationStatus.Unchanged : FileGenerationStatus.Updated,
            Assert.Single(result.Files, static file => file.FileId.Value == "a").Status);
        Assert.Equal(FileGenerationStatus.Preserved, Assert.Single(result.Files, static file => file.FileId.Value == "b").Status);
        Assert.Equal(expected, await Read("a.txt"));
        Assert.Equal(customization, await Read("b.txt"));
        Assert.Equal(clientTime, File.GetLastWriteTimeUtc(Path.Combine(Output, "b.txt")));
        if (identicalContent) Assert.Equal(generatedTime, File.GetLastWriteTimeUtc(Path.Combine(Output, "a.txt")));
        Assert.Equal(lockBefore, await Read(SiteFactoryLockWriter.FileName));
        using JsonDocument state = JsonDocument.Parse(await Read(GenerationState.FileName));
        Assert.Equal(Hash(expected), Entry(state, "a").GetProperty("lastGeneratedSha256").GetString());
        Assert.Equal(JsonValueKind.Null, Entry(state, "b").GetProperty("lastGeneratedSha256").ValueKind);
        Assert.Equal(Hash(await Read(GenerationReportWriter.FileName)), state.RootElement.GetProperty("reportSha256").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalGeneratedChangesCannotBeOverwrittenEvenIfTheyEqualTheNewCandidate(bool equalsCandidate)
    {
        var inputs = Inputs();
        Dictionary<string, string?> contents = Contents();
        RecordingProvider provider = new(inputs.Template, inputs.Config, source => Task.FromResult(contents[source]));
        InitialGenerationPreparation first = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, Token);
        await first.Executor!.ExecuteAsync(inputs.Request, Token);
        contents["source/a"] = "New candidate\n";
        await File.WriteAllBytesAsync(Path.Combine(Output, "a.txt"), Encoding.UTF8.GetBytes(equalsCandidate ? "New candidate\n" : "Local edit\n"), Token);
        Dictionary<string, string> before = Fingerprints();

        InitialGenerationPreparation rejected = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, Token);

        Assert.Equal(InitialGenerationPreparationStatus.Conflict, rejected.Status);
        Assert.Contains(rejected.Diagnostics, static diagnostic => diagnostic.Code.Value == "SF-GEN-FILE-004");
        Assert.Equal(before, Fingerprints());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ContentAndPublicationFailuresKeepTheirStatusesAndNeverPublishState(int failingWrite)
    {
        var inputs = Inputs();
        RecordingProvider provider = new(inputs.Template, inputs.Config, source => Task.FromResult(Contents()[source]));
        int writes = 0;
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed && ++writes == failingWrite) throw new IOException("fixture-sensitive-detail");
        });
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, writer, Token);
        provider.ForbidReads = true;

        GenerationExecutionResult result = await ready.Executor!.ExecuteAsync(inputs.Request, Token);

        Assert.Equal(GenerationExecutionStatus.Failed, result.Status);
        FileGenerationResult failure = Assert.Single(result.Files, static file => file.Status == FileGenerationStatus.Failed);
        Assert.Equal("SF-GEN-FILE-015", failure.Error?.Code.Value);
        Assert.DoesNotContain("fixture-sensitive-detail", failure.Error!.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Output, GenerationState.FileName)));
        Assert.Empty(Directory.GetFiles(Output, ".sitefactory-*.tmp", SearchOption.AllDirectories));
        if (failingWrite != 4)
        {
            using JsonDocument report = JsonDocument.Parse(await Read(GenerationReportWriter.FileName));
            Assert.Equal(failingWrite == 5 ? "success" : "failed", report.RootElement.GetProperty("status").GetString());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterPreparationDoesNotRereadOrPublishState(bool duringWrite)
    {
        var inputs = Inputs();
        RecordingProvider provider = new(inputs.Template, inputs.Config, source => Task.FromResult(Contents()[source]));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        AtomicFileWriter writer = new(stage => { if (duringWrite && stage == AtomicWriteCheckpoint.ContentFlushed) cancellation.Cancel(); });
        InitialGenerationPreparation ready = await InitialGenerationExecutor.PrepareAsync(
            inputs.Request, inputs.Config, inputs.Template, Output, provider, writer, cancellation.Token);
        provider.ForbidReads = true;
        if (!duringWrite) cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready.Executor!.ExecuteAsync(inputs.Request, cancellation.Token));

        Assert.Equal(ContentSources, provider.Sources);
        if (duringWrite) Assert.Empty(Directory.GetFiles(Output, "*", SearchOption.AllDirectories));
        else Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    public void Dispose()
    {
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-prepared-tests-", Path.GetFileName(sandbox), StringComparison.Ordinal);
        Directory.Delete(sandbox, recursive: true);
    }

    private static (ClientConfigurationV1 Config, TemplateManifest Template, GenerationExecutionRequest Request) Inputs()
    {
        ClientConfigurationV1 config = new(1, new("snapshot-client", "Snapshot Client"),
            new("snapshot-product", "snapshot-template", "fr-FR", "https://example.test"),
            new([]), new("docker-compose-vps"), new(true, false));
        TemplateManifest manifest = new(new("snapshot-template"), new("1.2.3"), [new("snapshot-product")],
            [new(new("a"), "a.txt", "source/a", FileGenerationStrategy.Generated),
             new(new("b"), "b.txt", "source/b", FileGenerationStrategy.CreateOnce),
             new(new("c"), "custom/config.txt", "source/custom", FileGenerationStrategy.Custom),
             new(new("d"), "protected/config.txt", "source/protected", FileGenerationStrategy.Protected)]);
        return (config, manifest, Request(config, manifest));
    }

    private static GenerationExecutionRequest Request(ClientConfigurationV1 config, TemplateManifest manifest, string id = "snapshot-plan") =>
        new(GenerationPlanBuilder.Build(new(id), config, manifest), config.Client.Slug);

    private static Dictionary<string, string?> Contents() => new(StringComparer.Ordinal)
    {
        ["source/a"] = "Équipe 東京 🧪\r\nSecond\rLast\n",
        ["source/b"] = "Client seed\r\n",
    };

    private sealed class RecordingProvider(TemplateManifest manifest, ClientConfigurationV1 configuration, Func<string, Task<string?>> read) : ITemplateContentProvider
    {
        internal List<string> Sources { get; } = [];
        internal bool ForbidReads { get; set; }

        public Task<string?> ReadAsync(TemplateManifest suppliedManifest, ClientConfigurationV1 suppliedConfiguration, string source,
            CancellationToken cancellationToken = default)
        {
            Assert.Same(manifest, suppliedManifest);
            Assert.Same(configuration, suppliedConfiguration);
            Assert.False(ForbidReads, "Execution must use the prepared snapshot.");
            cancellationToken.ThrowIfCancellationRequested();
            Sources.Add(source);
            return read(source);
        }
    }

    private Dictionary<string, byte[]> Files() => Directory.GetFiles(Output, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(Output, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);
    private Dictionary<string, string> Fingerprints() => Files().ToDictionary(pair => pair.Key,
        pair => Hash(pair.Value) + "|" + File.GetLastWriteTimeUtc(Path.Combine(Output, pair.Key)).ToString("O", CultureInfo.InvariantCulture),
        StringComparer.Ordinal);
    private Task<byte[]> Read(string path) => File.ReadAllBytesAsync(Path.Combine(Output, path), Token);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static JsonElement Entry(JsonDocument state, string id) =>
        Assert.Single(state.RootElement.GetProperty("files").EnumerateArray(), entry => entry.GetProperty("id").GetString() == id);

    private static Dictionary<string, byte[]> HistoricalFiles()
    {
        // Frozen from the unmodified 13ff760 CLI, before C1. Only the executing build version
        // and the two hashes depending on it are substituted; no production control serializer is reused.
        string version = JsonSerializer.Serialize(typeof(InitialGenerationExecutor).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
        string candidateLock = HistoricalLock.Replace("@@GENERATOR@@", version, StringComparison.Ordinal) + "\n";
        string report = HistoricalReport.Replace("@@GENERATOR@@", version, StringComparison.Ordinal)
            .Replace("@@LOCK_HASH@@", Hash(Encoding.UTF8.GetBytes(candidateLock)), StringComparison.Ordinal) + "\n";
        string state = HistoricalState.Replace("@@GENERATOR@@", version, StringComparison.Ordinal)
            .Replace("@@LOCK_HASH@@", Hash(Encoding.UTF8.GetBytes(candidateLock)), StringComparison.Ordinal)
            .Replace("@@REPORT_HASH@@", Hash(Encoding.UTF8.GetBytes(report)), StringComparison.Ordinal) + "\n";
        return new(StringComparer.Ordinal)
        {
            ["guide.html"] = Encoding.UTF8.GetBytes(HistoricalGuide + "\n"),
            ["index.html"] = Encoding.UTF8.GetBytes(HistoricalIndex + "\n"),
            ["sitefactory.lock.json"] = Encoding.UTF8.GetBytes(candidateLock),
            ["generation-report.json"] = Encoding.UTF8.GetBytes(report),
            [".sitefactory/generation-state.json"] = Encoding.UTF8.GetBytes(state),
        };
    }

    private const string HistoricalGuide = """
        <!DOCTYPE html>
        <html lang="fr-FR">
        <head>
        <meta charset="utf-8">
        <title>Fictitious Documentation — Guide</title>
        </head>
        <body>
        <header><h1>Fictitious Documentation</h1></header>
        <nav aria-label="Documentation"><a href="index.html">Accueil</a> | <a href="guide.html">Guide</a></nav>
        <main>
        <h1>Guide</h1>
        <p>Ces pages fictives constituent un point de d&#xE9;part minimal.</p>
        <h2>Format</h2>
        <p>Les titres et les paragraphes sont pr&#xE9;sent&#xE9;s sans contenu actif. Le HTML brut comme &lt;em&gt; reste du texte, pas une instruction de rendu.</p>
        </main>
        </body>
        </html>
        """;

    private const string HistoricalIndex = """
        <!DOCTYPE html>
        <html lang="fr-FR">
        <head>
        <meta charset="utf-8">
        <title>Fictitious Documentation — Accueil</title>
        </head>
        <body>
        <header><h1>Fictitious Documentation</h1></header>
        <nav aria-label="Documentation"><a href="index.html">Accueil</a> | <a href="guide.html">Guide</a></nav>
        <main>
        <h1>Bienvenue</h1>
        <p>Documentation de d&#xE9;monstration issue des sources Markdown du repository.</p>
        <h2>Navigation</h2>
        <p>Utilisez les liens Accueil et Guide pour parcourir les deux pages.</p>
        </main>
        </body>
        </html>
        """;

    private const string HistoricalLock = """
        {
          "schemaVersion": 1,
          "siteFactoryVersion": @@GENERATOR@@,
          "configurationSchemaVersion": 1,
          "product": "documentation",
          "template": {
            "id": "documentation-minimal",
            "version": "0.1.0",
            "manifestSchemaVersion": 2
          },
          "modules": []
        }
        """;

    private const string HistoricalReport = """
        {
          "schemaVersion": 1,
          "scope": "reported-file-operations",
          "status": "success",
          "siteFactoryVersion": @@GENERATOR@@,
          "createdFiles": [
            "guide.html",
            "index.html",
            "sitefactory.lock.json"
          ],
          "modifiedFiles": [],
          "unchangedFiles": [],
          "preservedFiles": [],
          "skippedFiles": [],
          "protectedFiles": [],
          "conflicts": [],
          "failedFiles": [],
          "operations": [
            {
              "path": "guide.html",
              "strategy": "generated",
              "plannedAction": "create",
              "sourceKind": "template",
              "sourceComponent": "documentation-minimal",
              "sourceVersion": null,
              "destinationExistedAtInspection": false,
              "previousHash": null,
              "currentHash": null,
              "proposedHash": "A62C64156584A9FE3E70FC1CC365A43F40A6B4E7E6501AA7A3355F0EF78A6338",
              "result": "created",
              "conflictCode": null
            },
            {
              "path": "index.html",
              "strategy": "generated",
              "plannedAction": "create",
              "sourceKind": "template",
              "sourceComponent": "documentation-minimal",
              "sourceVersion": null,
              "destinationExistedAtInspection": false,
              "previousHash": null,
              "currentHash": null,
              "proposedHash": "DBBB32383BF2910A735AA5D3E67FD5FFFB741AD49D836A01E3013BBE6AB55DF1",
              "result": "created",
              "conflictCode": null
            },
            {
              "path": "sitefactory.lock.json",
              "strategy": "generated",
              "plannedAction": "create",
              "sourceKind": "generator",
              "sourceComponent": "sitefactory",
              "sourceVersion": @@GENERATOR@@,
              "destinationExistedAtInspection": false,
              "previousHash": null,
              "currentHash": null,
              "proposedHash": "@@LOCK_HASH@@",
              "result": "created",
              "conflictCode": null
            }
          ],
          "diagnostics": []
        }
        """;

    private const string HistoricalState = """
        {
          "schemaVersion": 1,
          "generatorVersion": @@GENERATOR@@,
          "configurationSha256": "7E0E63232078E4B2A41071326D2452E26BADE6801B9368E733EE518D72486AFF",
          "planId": "plan:fictitious-documentation:documentation:documentation-minimal",
          "client": "fictitious-documentation",
          "product": "documentation",
          "template": {
            "id": "documentation-minimal",
            "version": "0.1.0"
          },
          "files": [
            {
              "id": "documentation-guide",
              "path": "guide.html",
              "strategy": "generated",
              "sourceComponent": "documentation-minimal",
              "source": "pages/guide",
              "sourceVersion": "0.1.0",
              "lastGeneratedSha256": "A62C64156584A9FE3E70FC1CC365A43F40A6B4E7E6501AA7A3355F0EF78A6338",
              "encoding": "utf-8",
              "lineEndings": "lf"
            },
            {
              "id": "documentation-index",
              "path": "index.html",
              "strategy": "generated",
              "sourceComponent": "documentation-minimal",
              "source": "pages/index",
              "sourceVersion": "0.1.0",
              "lastGeneratedSha256": "DBBB32383BF2910A735AA5D3E67FD5FFFB741AD49D836A01E3013BBE6AB55DF1",
              "encoding": "utf-8",
              "lineEndings": "lf"
            },
            {
              "id": "sitefactory-lock",
              "path": "sitefactory.lock.json",
              "strategy": "generated",
              "sourceComponent": "sitefactory",
              "source": "composition",
              "sourceVersion": @@GENERATOR@@,
              "lastGeneratedSha256": "@@LOCK_HASH@@",
              "encoding": "utf-8",
              "lineEndings": "lf"
            }
          ],
          "reportSha256": "@@REPORT_HASH@@"
        }
        """;

}
