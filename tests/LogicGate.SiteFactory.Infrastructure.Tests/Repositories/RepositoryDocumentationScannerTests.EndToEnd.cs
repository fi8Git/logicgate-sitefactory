using System.Text.Json;
using System.Xml.Linq;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Repositories;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using LogicGate.SiteFactory.Modules.Documentation.Model;
using LogicGate.SiteFactory.Tests.Fixtures;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Repositories;

public sealed partial class RepositoryDocumentationScannerTests
{
    [Fact]
    public async Task RepositoryToDocumentationSiteThenFixtureUsesOneCompositionAndPreservesGit()
    {
        await InitAsync();
        Put("README.md", "# Home\n\nREADME presentation. [Getting Started](docs/getting-started.md#getting-started)\n");
        Put("docs/getting-started.md", "# Getting Started\n\nOriginal instructions. [Overview](architecture/overview.md#overview)\n");
        Put("docs/architecture/overview.md", "# Overview\n\n[Home](../../README.md#home)\n");
        foreach (string excluded in new[] { ".git/hidden.md", "docs/.git/hidden.md", "docs/bin/hidden.md",
                     "docs/obj/hidden.md", "other/private.md", "README.txt", "docs/private.txt" })
            Put(excluded, "EXCLUDED-SENTINEL");
        Dictionary<string, string> original = Snapshot();
        string[] gitDirectories = Directory.GetDirectories(Path.Combine(Root, ".git"), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var model = await ReadDocumentationModelAsync(Root);
        Assert.Equal(["README.md", "docs/architecture/overview.md", "docs/getting-started.md"], model.Pages.Select(static page => page.LogicalId));
        DocumentationModule documentation = new(new TemplateManifestJsonReader());
        PublicGenerationFixture fixture = new();
        ModuleCatalog catalog = new([
            new(fixture.Descriptor.Id.Value, fixture.Descriptor.Version.Value, fixture),
            new(documentation.Descriptor.Id.Value, documentation.Descriptor.Version.Value, documentation)]);
        Assert.True(catalog.IsValid);
        Assert.Equal(["documentation", "fixture-module"], catalog.Modules.Select(static module => module.Id.Value));
        GenerationComposition composition = new(catalog, [fixture.CreateContribution(), documentation.CreateContribution(model)]);
        string output = Path.Combine(sandbox, "documentation-site");
        GenerationExecutionResult initial = await GenerateWithCompositionAsync(composition, DocumentationConfiguration(), output);
        Assert.All(initial.Files, static file => Assert.Equal(FileGenerationStatus.Created, file.Status));
        Assert.Equal([".sitefactory/generation-state.json", "documentation.css", "generation-report.json", "index.html",
            "pages/architecture/overview.html", "pages/getting-started.html", "sitefactory.lock.json"], CaptureOutput(output).Keys);
        string home = await File.ReadAllTextAsync(Path.Combine(output, "index.html"), Token);
        Assert.Contains("README presentation.", home, StringComparison.Ordinal);
        Assert.Contains("<span>Architecture</span>", home, StringComparison.Ordinal);
        foreach (string pagePath in Directory.GetFiles(output, "*.html", SearchOption.AllDirectories))
        {
            var html = ParseGeneratedHtml(await File.ReadAllTextAsync(pagePath, Token));
            Assert.Contains(html.Descendants("a"), static anchor => anchor.Value == "Getting Started");
            Assert.Contains(html.Descendants("a"), static anchor => anchor.Value == "Overview");
            Assert.DoesNotContain("EXCLUDED-SENTINEL", html.Value, StringComparison.Ordinal);
            foreach (var href in html.Descendants().Attributes("href"))
            {
                string[] parts = href.Value.Split('#', 2);
                string target = parts[0].Length == 0 ? pagePath
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pagePath)!, Uri.UnescapeDataString(parts[0]).Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(target), href.Value);
                if (parts.Length == 2)
                    Assert.Contains(ParseGeneratedHtml(await File.ReadAllTextAsync(target, Token)).DescendantsAndSelf().Attributes("id"),
                        id => id.Value == parts[1]);
            }
        }
        Assert.Equal(original.OrderBy(static pair => pair.Key), Snapshot().OrderBy(static pair => pair.Key));

        // Modify a permitted source, not the generated output. Titles/navigation and structure stay fixed.
        foreach (string path in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(path, new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        var before = CaptureOutput(output);
        Put("docs/getting-started.md", "# Getting Started\n\nUpdated instructions. [Overview](architecture/overview.md#overview)\n");
        Dictionary<string, string> changedSource = Snapshot();
        composition = new(catalog, [documentation.CreateContribution(await ReadDocumentationModelAsync(Root)), fixture.CreateContribution()]);
        GenerationExecutionResult updated = await GenerateWithCompositionAsync(composition, DocumentationConfiguration(), output);
        Assert.Equal(3, updated.Files.Count(static file => file.Status == FileGenerationStatus.Updated));
        var after = CaptureOutput(output);
        Assert.Equal(before.Keys, after.Keys);
        Assert.Equal([".sitefactory/generation-state.json", "generation-report.json", "pages/getting-started.html"],
            before.Keys.Where(path => before[path] != after[path]));
        Assert.Contains("Updated instructions.", await File.ReadAllTextAsync(Path.Combine(output, "pages", "getting-started.html"), Token), StringComparison.Ordinal);
        using (JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "generation-report.json"), Token)))
        {
            Assert.Equal(["pages/getting-started.html"], report.RootElement.GetProperty("modifiedFiles").EnumerateArray().Select(static value => value.GetString()));
            Assert.Empty(report.RootElement.GetProperty("createdFiles").EnumerateArray());
        }
        GenerationExecutionResult unchanged = await GenerateWithCompositionAsync(composition, DocumentationConfiguration(), output);
        Assert.All(unchanged.Files.Where(static file => file.FileId.Value is not "generation-report" and not "generation-state"),
            static file => Assert.Equal(FileGenerationStatus.Unchanged, file.Status));
        var settled = CaptureOutput(output);
        await GenerateWithCompositionAsync(composition, DocumentationConfiguration(), output);
        Assert.Equal(settled, CaptureOutput(output));

        // Same catalogue, resolver, planner and executor, after Documentation: no privileged PME path.
        ClientConfigurationV1 fixtureConfiguration = new(1, new("public-fixture", "Public Fixture"),
            new("fixture-product", "fixture-template", "fr-FR", "https://example.test"), new([]), new("docker-compose-vps"), new(true, false));
        string fixtureOutput = Path.Combine(sandbox, "fixture-project");
        GenerationExecutionResult generatedFixture = await GenerateWithCompositionAsync(composition, fixtureConfiguration, fixtureOutput);
        Assert.All(generatedFixture.Files, static file => Assert.Equal(FileGenerationStatus.Created, file.Status));
        Assert.Equal(10, CaptureOutput(fixtureOutput).Count);
        Assert.True(File.Exists(Path.Combine(fixtureOutput, "Client.slnx")));
        using (JsonDocument fixtureLock = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtureOutput, "sitefactory.lock.json"), Token)))
            Assert.Equal("fixture-template", fixtureLock.RootElement.GetProperty("template").GetProperty("id").GetString());
        await File.WriteAllTextAsync(Path.Combine(fixtureOutput, "README.md"), "Client customization", Token);
        GenerationExecutionResult regeneratedFixture = await GenerateWithCompositionAsync(composition, fixtureConfiguration, fixtureOutput);
        Assert.Contains(regeneratedFixture.Files, static file => file.Status == FileGenerationStatus.Preserved);
        Assert.Equal("Client customization", await File.ReadAllTextAsync(Path.Combine(fixtureOutput, "README.md"), Token));
        Assert.Equal(settled, CaptureOutput(output));
        Assert.Equal(changedSource.OrderBy(static pair => pair.Key), Snapshot().OrderBy(static pair => pair.Key));
        Assert.Equal(gitDirectories, Directory.GetDirectories(Path.Combine(Root, ".git"), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("rename")]
    public async Task ContentRegenerationDoesNotAuthorizeChangesToTheDocumentSet(string change)
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        if (change == "add") Put("docs/new.md", "# New page");
        else if (change == "remove") File.Delete(Path.Combine(Root, "docs", "guide.md"));
        else File.Move(Path.Combine(Root, "docs", "guide.md"), Path.Combine(Root, "docs", "renamed.md"));
        var before = CaptureOutput(output);
        var inputs = await DocumentationInputsAsync(Root);
        InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, prepared.Status);
        Assert.Equal("SF-GEN-STATE-001", Assert.Single(prepared.Diagnostics).Code.Value);
        Assert.Null(prepared.Executor);
        Assert.Equal(before, CaptureOutput(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentUpdateRechecksLateConflictsBeforeWritingAnyEarlierCandidate(bool editState)
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        Put("README.md", "# Home\n\nUpdated source");
        var inputs = await DocumentationInputsAsync(Root);
        InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, prepared.Status);
        string changed = editState ? Path.Combine(output, ".sitefactory", "generation-state.json") : Path.Combine(output, "pages", "guide.html");
        await File.AppendAllTextAsync(changed, "local edit", Token);
        var before = CaptureOutput(output);
        GenerationExecutionResult result = await prepared.Executor!.ExecuteAsync(inputs.Request, Token);
        Assert.Equal(GenerationExecutionStatus.Conflict, result.Status);
        Assert.Equal("SF-GEN-FILE-013", Assert.Single(result.Files).Error!.Code.Value);
        Assert.Equal(before, CaptureOutput(output));
    }

    [Fact]
    public async Task ContentUpdateAppliesThePreparedSnapshotNotASilentlyRebuiltSource()
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        Put("README.md", "# Home\n\nPrepared content");
        var inputs = await DocumentationInputsAsync(Root);
        InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, prepared.Status);
        Put("README.md", "# Home\n\nLater source change");
        Assert.Equal(GenerationExecutionStatus.Succeeded, (await prepared.Executor!.ExecuteAsync(inputs.Request, Token)).Status);
        string html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"), Token);
        Assert.Contains("Prepared content", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Later source change", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutgoingSourceLinkStopsThePipelineBeforeAnySiteIsCreated()
    {
        await InitAsync();
        Put("README.md", "# Home");
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        string sentinel = Path.Combine(outside, "private.md");
        await File.WriteAllTextAsync(sentinel, "OUTSIDE-SENTINEL", Token);
        Link(Path.Combine(Root, "docs"), outside);
        var scan = await Scanner().ScanAsync(Root, Token);
        Assert.False(scan.IsSuccess);
        Assert.Empty(scan.Documents);
        using var repository = await new LocalGitRepositoryReader().OpenAsync(Root, Token);
        var model = await DocumentationModelLoader.LoadAsync(scan, repository, Token);
        Assert.Null(model.Site);
        Assert.False(Directory.Exists(Path.Combine(sandbox, "site")));
        Assert.Equal("OUTSIDE-SENTINEL", await File.ReadAllTextAsync(sentinel, Token));
        Assert.Single(Directory.GetFileSystemEntries(outside));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FailedContentUpdatesNeverAdvanceTheOwnershipState(int failedWrite)
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        byte[] previousState = await File.ReadAllBytesAsync(Path.Combine(output, ".sitefactory", "generation-state.json"), Token);
        Put("README.md", "# Home\n\nNew home");
        Put("docs/guide.md", "# Guide\n\nNew guide");
        var inputs = await DocumentationInputsAsync(Root);
        int writes = 0;
        AtomicFileWriter writer = new(stage =>
        {
            if (stage == AtomicWriteCheckpoint.ContentFlushed && ++writes == failedWrite) throw new IOException("private provider detail");
        });
        InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, writer, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, prepared.Status);
        GenerationExecutionResult result = await prepared.Executor!.ExecuteAsync(inputs.Request, Token);
        Assert.Equal(GenerationExecutionStatus.Failed, result.Status);
        Assert.Equal(previousState, await File.ReadAllBytesAsync(Path.Combine(output, ".sitefactory", "generation-state.json"), Token));
        Assert.Empty(Directory.GetFiles(output, ".sitefactory-*.tmp", SearchOption.AllDirectories));
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "generation-report.json"), Token));
        Assert.Single(report.RootElement.GetProperty("failedFiles").EnumerateArray());
        Assert.Equal(failedWrite - 1, report.RootElement.GetProperty("modifiedFiles").GetArrayLength());
        Assert.DoesNotContain("private provider detail", report.RootElement.GetRawText(), StringComparison.Ordinal);
        var beforeRetry = CaptureOutput(output);
        InitialGenerationPreparation retry = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, retry.Status);
        Assert.Equal(beforeRetry, CaptureOutput(output));
    }

    [Fact]
    public async Task CancellingAPreparedContentUpdateLeavesEveryOutputUntouched()
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        Put("README.md", "# Home\n\nNew home");
        var inputs = await DocumentationInputsAsync(Root);
        InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, prepared.Status);
        var before = CaptureOutput(output);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => prepared.Executor!.ExecuteAsync(inputs.Request, cancellation.Token));
        Assert.Equal(before, CaptureOutput(output));
    }

    private static async Task<GenerationExecutionResult> GenerateWithCompositionAsync(
        GenerationComposition composition, ClientConfigurationV1 configuration, string output)
    {
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateResolutionResult resolved = await new TemplateResolver(composition).ResolveAsync(configuration, Token);
        Assert.True(resolved.IsSuccess, resolved.Diagnostic?.Code.Value);
        GenerationPlan plan = GenerationPlanBuilder.Build(new("repository-e2e"), configuration, resolved.Template!);
        Assert.False(plan.HasErrors);
        GenerationExecutionRequest request = new(plan, configuration.Client.Slug);
        InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(request, configuration, resolved.Template!, output, composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, prepared.Status);
        GenerationExecutionResult result = await prepared.Executor!.ExecuteAsync(request, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, result.Status);
        return result;
    }

    private static SortedDictionary<string, (string Bytes, DateTime Modified)> CaptureOutput(string root) =>
        new(Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
            static path => (Convert.ToBase64String(File.ReadAllBytes(path)), File.GetLastWriteTimeUtc(path))), StringComparer.Ordinal);
}
