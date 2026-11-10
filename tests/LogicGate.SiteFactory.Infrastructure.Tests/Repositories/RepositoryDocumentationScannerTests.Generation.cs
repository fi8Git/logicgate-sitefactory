using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Repositories;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Repositories;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using LogicGate.SiteFactory.Modules.Documentation.Model;
using LogicGate.SiteFactory.Modules.Documentation.Scanning;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Repositories;

public sealed partial class RepositoryDocumentationScannerTests
{
    [Fact]
    public async Task DocumentationSiteUsesRealScannerPlannerExecutorLockReportAndSafeNavigation()
    {
        await DocumentationRepositoryAsync();
        Dictionary<string, string> original = Snapshot();
        string output = Path.Combine(sandbox, "site");
        var inputs = await DocumentationInputsAsync(Root);
        Assert.Equal("0.2.0", inputs.Template.TemplateVersion.Value);
        Assert.Equal(["documentation.css", "index.html", "pages/architecture/overview.html", "pages/guide.html"],
            inputs.Request.Plan.Files.Select(static file => file.RelativePath));
        Assert.All(inputs.Request.Plan.Files, static file => Assert.Equal(FileGenerationStrategy.Generated, file.Strategy));
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, preparation.Status);
        Assert.False(Directory.Exists(output));
        GenerationExecutionResult execution = await preparation.Executor!.ExecuteAsync(inputs.Request, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, execution.Status);
        Assert.All(execution.Files, static file => Assert.Equal(FileGenerationStatus.Created, file.Status));
        Assert.Equal(7, Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length);
        Assert.Equal(original.OrderBy(static pair => pair.Key), Snapshot().OrderBy(static pair => pair.Key));

        string home = await File.ReadAllTextAsync(Path.Combine(output, "index.html"), Token);
        Assert.Contains("<h1 id=\"home\">Home</h1>", home, StringComparison.Ordinal);
        Assert.Contains("<strong>Useful</strong>", home, StringComparison.Ordinal);
        Assert.Contains("<a href=\"./pages/guide.html#guide\">Read the guide</a>", home, StringComparison.Ordinal);
        Assert.Contains("<span>Architecture</span>", home, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", home, StringComparison.Ordinal);
        string nested = await File.ReadAllTextAsync(Path.Combine(output, "pages", "architecture", "overview.html"), Token);
        Assert.Contains("href=\"../.././index.html\"", nested, StringComparison.Ordinal);
        Assert.Contains("href=\"../.././pages/guide.html#guide\"", nested, StringComparison.Ordinal);
        Assert.Contains("<li>First</li>", nested, StringComparison.Ordinal);
        Assert.Contains("<code>", nested, StringComparison.Ordinal);
        foreach (string path in Directory.GetFiles(output, "*.html", SearchOption.AllDirectories))
        {
            string html = await File.ReadAllTextAsync(path, Token);
            Assert.DoesNotContain('\r', html);
            Assert.False((await File.ReadAllBytesAsync(path, Token)).AsSpan().StartsWith(Encoding.UTF8.Preamble));
            XElement page = ParseGeneratedHtml(html);
            Assert.Single(page.Descendants("nav"));
            Assert.Single(page.Descendants("main"));
            foreach (XAttribute href in page.Descendants().Attributes("href"))
            {
                string[] destination = href.Value.Split('#', 2);
                string target = destination[0].Length == 0 ? path
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, Uri.UnescapeDataString(destination[0]).Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(target), href.Value);
                if (destination.Length == 2)
                    Assert.Contains(ParseGeneratedHtml(await File.ReadAllTextAsync(target, Token)).DescendantsAndSelf().Attributes("id"),
                        id => id.Value == Uri.UnescapeDataString(destination[1]));
            }
        }
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "generation-report.json"), Token));
        Assert.Equal("success", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(5, report.RootElement.GetProperty("createdFiles").GetArrayLength());
        using JsonDocument composition = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "sitefactory.lock.json"), Token));
        Assert.Equal("documentation", composition.RootElement.GetProperty("product").GetString());
        Assert.Equal("0.2.0", composition.RootElement.GetProperty("template").GetProperty("version").GetString());
        Assert.True(File.Exists(Path.Combine(output, ".sitefactory", "generation-state.json")));
    }

    [Fact]
    public async Task DocumentationRegenerationPreservesCanonicalBytesAndModificationTimes()
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        string[] paths = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith("generation-report.json", StringComparison.Ordinal)
                && !path.EndsWith("generation-state.json", StringComparison.Ordinal)).ToArray();
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static path => path, File.ReadAllBytes, StringComparer.Ordinal);
        DateTime timestamp = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        foreach (string path in paths) File.SetLastWriteTimeUtc(path, timestamp);
        GenerationExecutionResult second = await GenerateDocumentationAsync(Root, output);
        Assert.All(second.Files.Where(static file => file.FileId.Value is not "generation-report" and not "generation-state"),
            static file => Assert.Equal(FileGenerationStatus.Unchanged, file.Status));
        foreach (string path in paths)
        {
            Assert.Equal(bytes[path], await File.ReadAllBytesAsync(path, Token));
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        }
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "generation-report.json"), Token));
        Assert.Empty(report.RootElement.GetProperty("createdFiles").EnumerateArray());
        Assert.Equal(paths.Length, report.RootElement.GetProperty("unchangedFiles").GetArrayLength());
        GenerationExecutionResult third = await GenerateDocumentationAsync(Root, output);
        Assert.All(third.Files, static file => Assert.Equal(FileGenerationStatus.Unchanged, file.Status));
    }

    [Fact]
    public async Task IdenticalRepositoriesInTwoRootsProduceIdenticalBytesIncludingFirstReportAndState()
    {
        await DocumentationRepositoryAsync();
        string secondRoot = Path.Combine(sandbox, "second-repository");
        foreach (string path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(secondRoot, Path.GetRelativePath(Root, path));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(path, destination);
        }
        // Empty Git directories are significant to the scanner, even before the first commit.
        foreach (string directory in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(secondRoot, Path.GetRelativePath(Root, directory)));
        string firstOutput = Path.Combine(sandbox, "first-site");
        string secondOutput = Path.Combine(sandbox, "second-site");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            await GenerateDocumentationAsync(Root, firstOutput);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            await GenerateDocumentationAsync(secondRoot, secondOutput);
        }
        finally { CultureInfo.CurrentCulture = previous; }
        foreach (string first in Directory.GetFiles(firstOutput, "*", SearchOption.AllDirectories))
        {
            byte[] content = await File.ReadAllBytesAsync(first, Token);
            Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(secondOutput, Path.GetRelativePath(firstOutput, first)), Token));
            Assert.DoesNotContain(sandbox, Encoding.UTF8.GetString(content), StringComparison.Ordinal);
        }
        Assert.Equal(Directory.GetFiles(firstOutput, "*", SearchOption.AllDirectories).Length,
            Directory.GetFiles(secondOutput, "*", SearchOption.AllDirectories).Length);

        // Identical source edits also produce identical regenerated sites, not just identical first builds.
        foreach (string root in new[] { Root, secondRoot })
            await File.AppendAllTextAsync(Path.Combine(root, "docs", "guide.md"), "\nSame permitted edit.\n", Token);
        await GenerateDocumentationAsync(Root, firstOutput);
        await GenerateDocumentationAsync(secondRoot, secondOutput);
        Assert.Equal(CaptureOutput(firstOutput).Select(static item => (item.Key, item.Value.Bytes)),
            CaptureOutput(secondOutput).Select(static item => (item.Key, item.Value.Bytes)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditedManagedOutputStillFailsEvenWhenTheSourceAlsoChanges(bool editSource)
    {
        await DocumentationRepositoryAsync();
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        await File.WriteAllTextAsync(Path.Combine(output, "index.html"), "User customization", Token);
        if (editSource) Put("README.md", "# Revised content");
        Dictionary<string, byte[]> before = Directory.GetFiles(output, "*", SearchOption.AllDirectories).ToDictionary(static path => path, File.ReadAllBytes);
        var inputs = await DocumentationInputsAsync(Root);
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Conflict, preparation.Status);
        Assert.Null(preparation.Executor);
        Assert.Equal("SF-GEN-FILE-004", Assert.Single(preparation.Diagnostics).Code.Value);
        foreach ((string path, byte[] bytes) in before) Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
    }

    [Theory]
    [InlineData("<script>alert('x')</script>")]
    [InlineData("<iframe src=\"https://example.invalid\"></iframe>")]
    [InlineData("<svg onload=\"alert(1)\"></svg>")]
    [InlineData("<img src=x onerror=\"alert(1)\">")]
    [InlineData("<style>body{background:url(https://example.invalid)}</style>")]
    [InlineData("[bad](javascript:alert%281%29) [bad](java&#x73;cript:alert%281%29)")]
    [InlineData("[bad](data:text/html;base64,PHNjcmlwdD4=) [bad](file:///private.txt)")]
    [InlineData("![remote](https://example.invalid/pixel) ![local](docs/guide.md)")]
    [InlineData("<https://example.invalid> <test@example.invalid> [remote](//example.invalid)")]
    [InlineData("[escape](../../outside.md) [encoded](%2e%2e/%2e%2e/outside.md)")]
    [InlineData("[reference][x]\n\n[x]: https://example.invalid")]
    [InlineData("# Title {#bad onclick=alert(1)}\n\n<!--markdig:extensions=advanced+attributes+media-->")]
    public async Task UntrustedMarkdownCannotIntroduceActiveContentOrUnapprovedLinks(string payload)
    {
        await DocumentationRepositoryAsync();
        Put("README.md", "# Home\n\n" + payload);
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        XElement html = ParseGeneratedHtml(await File.ReadAllTextAsync(Path.Combine(output, "index.html"), Token));
        Assert.DoesNotContain(html.Descendants(), static element => element.Name.LocalName is "script" or "iframe" or "svg" or "img" or "style" or "object" or "embed" or "form" or "base");
        Assert.DoesNotContain(html.DescendantsAndSelf().Attributes(), static attribute => attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.LocalName is "src" or "srcdoc" or "style");
        Assert.All(html.DescendantsAndSelf().Attributes("href"), static href => Assert.True(href.Value.StartsWith("./", StringComparison.Ordinal) || href.Value == "#content", href.Value));
        Assert.Contains(html.Descendants("meta"), static meta => (string?)meta.Attribute("http-equiv") == "Content-Security-Policy");
    }

    [Fact]
    public async Task FileDirectoryCollisionCreatedByHtmlProjectionIsRejectedBeforeOutputCreation()
    {
        await InitAsync();
        Put("README.md", "# Home");
        Put("docs/guide.md", "# Guide");
        Put("docs/guide.html/child.md", "# Child");
        DocumentationSite model = await ReadDocumentationModelAsync(Root);
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationContribution contribution = module.CreateContribution(model);
        TemplateCatalogReadResult result = await contribution.Templates.ReadAsync(Token);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Manifests);
        Assert.False(Directory.Exists(Path.Combine(sandbox, "site")));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("docs/.git/config.md")]
    [InlineData("docs\\escape.md")]
    [InlineData("/absolute.md")]
    public void MaliciousLogicalSourcesCannotBecomeGenerationContributions(string path)
    {
        DocumentationModelResult result = DocumentationModelBuilder.Build([
            new(new("README.md", "README.md", "Home", DocumentationSourceType.RootReadme), "# Home"),
            new(new(path, path, "Bad", DocumentationSourceType.DocumentationPage), "# Bad")], Token);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Site);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Fact]
    public async Task LinkedOutputIsRejectedByTheExistingPhysicalPreflight()
    {
        await DocumentationRepositoryAsync();
        string outside = Directory.CreateDirectory(Path.Combine(sandbox, "outside")).FullName;
        await File.WriteAllTextAsync(Path.Combine(outside, "sentinel.txt"), "Unchanged", Token);
        string output = Path.Combine(sandbox, "site");
        Link(output, outside);
        var inputs = await DocumentationInputsAsync(Root);
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.NotEqual(InitialGenerationPreparationStatus.Ready, preparation.Status);
        Assert.Null(preparation.Executor);
        Assert.Equal("sentinel.txt", Path.GetFileName(Assert.Single(Directory.GetFiles(outside))));
        Assert.Equal("Unchanged", await File.ReadAllTextAsync(Path.Combine(outside, "sentinel.txt"), Token));
    }

    [Theory]
    [InlineData("missing-home", "SF-DOC-SITE-001")]
    [InlineData("too-many-pages", "SF-DOC-SITE-002")]
    [InlineData("long-path", "SF-DOC-SITE-002")]
    public async Task UnsupportedSiteShapesHaveStableDiagnosticsWithoutAnyOutput(string scenario, string code)
    {
        List<DocumentationSource> sources = [];
        if (scenario != "missing-home") sources.Add(DocumentationSource("README.md"));
        if (scenario == "too-many-pages") sources.AddRange(Enumerable.Range(0, 128).Select(index => DocumentationSource($"docs/{index}.md")));
        else sources.Add(DocumentationSource("docs/" + (scenario == "long-path" ? new string('a', 195) : "guide") + ".md"));
        DocumentationModelResult model = DocumentationModelBuilder.Build(sources, Token);
        Assert.True(model.IsSuccess);
        DocumentationModule module = new(new TemplateManifestJsonReader());
        TemplateCatalogReadResult result = await module.CreateContribution(model.Site!).Templates.ReadAsync(Token);
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Diagnostic!.Code.Value);
        Assert.Empty(result.Manifests);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Fact]
    public async Task MaximumSiteStillFitsTheExistingOwnershipStateAndCanBeRegenerated()
    {
        string prefix = "docs/" + string.Join('/', Enumerable.Repeat(new string('é', 45), 4)) + "/";
        DocumentationModelResult model = DocumentationModelBuilder.Build([
            DocumentationSource("README.md"),
            .. Enumerable.Range(0, 127).Select(index => DocumentationSource(prefix + index.ToString("D4", CultureInfo.InvariantCulture) + ".md"))], Token);
        Assert.True(model.IsSuccess);
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationContribution contribution = module.CreateContribution(model.Site!);
        TemplateManifest manifest = Assert.Single((await contribution.Templates.ReadAsync(Token)).Manifests);
        ClientConfigurationV1 configuration = DocumentationConfiguration();
        GenerationPlan plan = GenerationPlanBuilder.Build(new("maximum-docs"), configuration, manifest);
        Assert.False(plan.HasErrors);
        GenerationExecutionRequest request = new(plan, configuration.Client.Slug);
        string output = Path.Combine(sandbox, "site");
        for (int iteration = 0; iteration < 2; iteration++)
        {
            InitialGenerationPreparation prepared = await InitialGenerationExecutor.PrepareAsync(request, configuration, manifest, output, contribution.Content, Token);
            Assert.Equal(InitialGenerationPreparationStatus.Ready, prepared.Status);
            Assert.Equal(GenerationExecutionStatus.Succeeded, (await prepared.Executor!.ExecuteAsync(request, Token)).Status);
            Assert.InRange(new FileInfo(Path.Combine(output, ".sitefactory", "generation-state.json")).Length, 1, 256 * 1024);
        }
    }

    [Fact]
    public async Task PhysicallyInvalidLastPageCannotLeaveEarlierDocumentationOrArtifacts()
    {
        DocumentationModelResult model = DocumentationModelBuilder.Build([
            DocumentationSource("README.md"), DocumentationSource("docs/a.md"), DocumentationSource("docs/b.md"),
            DocumentationSource("docs/zz/" + new string('é', 128) + ".md")], Token);
        Assert.True(model.IsSuccess);
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationContribution contribution = module.CreateContribution(model.Site!);
        TemplateManifest manifest = Assert.Single((await contribution.Templates.ReadAsync(Token)).Manifests);
        ClientConfigurationV1 configuration = DocumentationConfiguration();
        GenerationPlan plan = GenerationPlanBuilder.Build(new("unsafe-docs"), configuration, manifest);
        Assert.False(plan.HasErrors);
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(new(plan, configuration.Client.Slug), configuration, manifest,
            Path.Combine(sandbox, "site"), contribution.Content, Token);
        Assert.Equal(InitialGenerationPreparationStatus.InvalidRoot, preparation.Status);
        Assert.Null(preparation.Executor);
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    [Fact]
    public async Task SiteContentIsBoundToTheResolvedManifestAndPropagatesCancellation()
    {
        DocumentationSite site = DocumentationModelBuilder.Build([DocumentationSource("README.md")], Token).Site!;
        DocumentationModule module = new(new TemplateManifestJsonReader());
        GenerationContribution contribution = module.CreateContribution(site);
        TemplateManifest manifest = Assert.Single((await contribution.Templates.ReadAsync(Token)).Manifests);
        TemplateManifest foreign = new(manifest.TemplateId, new("9.0.0"), manifest.SupportedProductIds, manifest.Files);
        Assert.Null(await contribution.Content.ReadAsync(foreign, DocumentationConfiguration(), manifest.Files[0].Source, Token));
        Assert.Null(await contribution.Content.ReadAsync(manifest, DocumentationConfiguration(), "../outside", Token));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => contribution.Content.ReadAsync(manifest, DocumentationConfiguration(), manifest.Files[0].Source, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => contribution.Templates.ReadAsync(cancellation.Token));
        Assert.Empty(Directory.GetFileSystemEntries(sandbox));
    }

    private static DocumentationSource DocumentationSource(string path) => new(new(path, path, "Unused candidate",
        path == "README.md" ? DocumentationSourceType.RootReadme : DocumentationSourceType.DocumentationPage), "# Page\n");

    [Fact]
    public async Task UnicodeAndSpacesRoundTripThroughPhysicalPagesAndEncodedLinks()
    {
        await DocumentationRepositoryAsync();
        Put("docs/équipe 東京.md", "# Team\n\n[Home](../README.md#home)");
        Put("README.md", "# Home\n\n[Team](docs/%C3%A9quipe%20%E6%9D%B1%E4%BA%AC.md#team)");
        string output = Path.Combine(sandbox, "site");
        await GenerateDocumentationAsync(Root, output);
        Assert.True(File.Exists(Path.Combine(output, "pages", "équipe 東京.html")));
        string html = await File.ReadAllTextAsync(Path.Combine(output, "index.html"), Token);
        XElement document = ParseGeneratedHtml(html);
        Assert.Contains(document.Descendants("a"), static anchor => anchor.Value == "Team"
            && (string?)anchor.Attribute("href") == "./pages/%C3%A9quipe%20%E6%9D%B1%E4%BA%AC.html#team");
    }

    private async Task DocumentationRepositoryAsync()
    {
        await InitAsync();
        Put("README.md", "# Home\n\n**Useful** documentation. [Read the guide](docs/guide.md#guide)\n");
        Put("docs/guide.md", "# Guide\n\n## INTRODUCTION\n\n[Home](../README.md#home)\n");
        Put("docs/architecture/overview.md", "# Overview\n\n[Guide](../guide.md#guide)\n\n- First\n- Second\n\n```\nplain code\n```\n");
    }

    private static async Task<DocumentationSite> ReadDocumentationModelAsync(string root)
    {
        DocumentationScanResult scan = await Scanner().ScanAsync(root, Token);
        Assert.True(scan.IsSuccess, scan.Diagnostic?.Code);
        using IReadOnlyRepository repository = await new LocalGitRepositoryReader().OpenAsync(root, Token);
        DocumentationModelResult result = await DocumentationModelLoader.LoadAsync(scan, repository, Token);
        Assert.True(result.IsSuccess, result.Diagnostic?.Code);
        return result.Site!;
    }

    private static async Task<(GenerationComposition Composition, TemplateManifest Template, GenerationExecutionRequest Request)> DocumentationInputsAsync(string root)
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        ModuleCatalog catalog = new([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]);
        GenerationComposition composition = new(catalog, [module.CreateContribution(await ReadDocumentationModelAsync(root))]);
        ClientConfigurationV1 configuration = DocumentationConfiguration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateResolutionResult resolved = await new TemplateResolver(composition).ResolveAsync(configuration, Token);
        Assert.True(resolved.IsSuccess, resolved.Diagnostic?.Code.Value);
        GenerationPlan plan = GenerationPlanBuilder.Build(new("documentation-plan"), configuration, resolved.Template!);
        Assert.False(plan.HasErrors);
        return (composition, resolved.Template!, new(plan, configuration.Client.Slug));
    }

    private static async Task<GenerationExecutionResult> GenerateDocumentationAsync(string root, string output)
    {
        var inputs = await DocumentationInputsAsync(root);
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(inputs.Request, DocumentationConfiguration(), inputs.Template, output, inputs.Composition, Token);
        Assert.Equal(InitialGenerationPreparationStatus.Ready, preparation.Status);
        GenerationExecutionResult result = await preparation.Executor!.ExecuteAsync(inputs.Request, Token);
        Assert.Equal(GenerationExecutionStatus.Succeeded, result.Status);
        return result;
    }

    private static ClientConfigurationV1 DocumentationConfiguration() => new(1, new("test-docs", "Documentation fictive"),
        new("documentation", "documentation-minimal", "en-US", "https://example.test"), new([]), new("docker-compose-vps"), new(true, false));

    private static XElement ParseGeneratedHtml(string html)
    {
        // Test fixtures only: normalize the layout's two HTML void-element kinds for the BCL XML reader.
        string xml = Regex.Replace(html.Replace("<!DOCTYPE html>", "", StringComparison.Ordinal), "<(meta|link)([^>]+)>", "<$1$2 />", RegexOptions.CultureInvariant);
        return XElement.Parse(xml);
    }
}
