using System.Globalization;
using LogicGate.SiteFactory.Modules.Documentation.Model;
using LogicGate.SiteFactory.Modules.Documentation.Scanning;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Templates;

public sealed class DocumentationModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void HomeAndPagesAreBuiltWithoutRenderingOrARepository()
    {
        DocumentationSite site = Build(Source("docs/guide.md", "## Guide\n\nRaw <text>"), Source("README.md", "Preface\r\n\r\n# Home\r\n"));
        Assert.Equal("README.md", site.Home!.LogicalId);
        Assert.Equal("/", site.Home.Route);
        Assert.Equal("Home", site.Home.Title);
        Assert.Equal("Preface\n\n# Home\n", site.Home.Markdown);
        Assert.Same(site.Home, site.Pages[0]);
        DocumentationPage page = site.Pages[1];
        Assert.Equal("docs/guide.md", page.RelativePath);
        Assert.Equal("/guide", page.Route);
        Assert.Equal("Guide", page.Title);
        Assert.Equal("## Guide\n\nRaw <text>", page.Markdown);
        Assert.Equal(DocumentationSourceType.DocumentationPage, page.SourceType);
    }

    [Fact]
    public void NavigationUsesDirectoriesAndTitlesNotDiscoveryOrder()
    {
        DocumentationSite site = Build(Source("docs/z-last.md"), Source("docs/architecture/deep/contracts.md"),
            Source("docs/architecture/overview.md", "# Architecture Overview"), Source("docs/getting-started.md"), Source("README.md", "# Home"));
        Assert.Equal(["Home", "Getting Started", "Z Last"], site.Navigation.Pages.Select(static page => page.Title));
        DocumentationSection architecture = Assert.Single(site.Navigation.Sections);
        Assert.Equal("/architecture", architecture.LogicalPath);
        Assert.Equal("Architecture", architecture.Title);
        Assert.Equal("Architecture Overview", Assert.Single(architecture.Navigation.Pages).Title);
        DocumentationSection deep = Assert.Single(architecture.Navigation.Sections);
        Assert.Equal("/architecture/deep", deep.LogicalPath);
        Assert.Equal("/architecture/deep/contracts", Assert.Single(deep.Navigation.Pages).Route);
    }

    [Fact]
    public void RepeatedTitlesDoNotCollapsePagesOrChangeRoutes()
    {
        DocumentationSite site = Build(Source("docs/b.md", "# Same"), Source("docs/a.md", "# Same"));
        Assert.Equal(["/a", "/b"], site.Pages.Select(static page => page.Route));
        Assert.All(site.Pages, static page => Assert.Equal("Same", page.Title));
        Assert.Null(site.Home);
    }

    [Theory]
    [InlineData("docs/a.md", "docs/a.md")]
    [InlineData("docs/A.md", "docs/a.md")]
    [InlineData("README.fr.md", "docs/readme.fr.md")]
    public void AmbiguousRoutesFailWithoutAPartialSite(string first, string second)
    {
        DocumentationModelResult result = DocumentationModelBuilder.Build([Source(first), Source(second)], Token);
        AssertFailure(result, "SF-DOC-MODEL-002");
        Assert.Equal(result.Diagnostic, DocumentationModelBuilder.Build([Source(second), Source(first)], Token).Diagnostic);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../secret.md")]
    [InlineData("/docs/page.md")]
    [InlineData("C:\\docs\\page.md")]
    [InlineData("\\\\server\\docs\\page.md")]
    [InlineData("docs/../secret.md")]
    [InlineData("docs//page.md")]
    [InlineData("docs\\page.md")]
    [InlineData("docs/.git/secret.md")]
    [InlineData("docs/OBJ/secret.md")]
    [InlineData("docs/secrets/secret.md")]
    [InlineData("docs/NUL.md")]
    [InlineData("docs/page.md.")]
    [InlineData("docs/.md")]
    [InlineData("elsewhere/page.md")]
    [InlineData("README.backup.md")]
    [InlineData("readme.md")]
    public void InvalidSourcePathsAreNotTransformedIntoRoutes(string path) =>
        AssertFailure(DocumentationModelBuilder.Build([Source(path)], Token), "SF-DOC-MODEL-001");

    [Fact]
    public void ScannerIdentityAndSourceTypeMustAgreeWithThePath()
    {
        foreach (RepositoryDocument document in new[]
        {
            new RepositoryDocument("docs/a.md", "other", "Ignored", DocumentationSourceType.DocumentationPage),
            new RepositoryDocument("docs/a.md", "docs/a.md", "Ignored", DocumentationSourceType.RootReadme),
            new RepositoryDocument("README.md", "README.md", "Ignored", (DocumentationSourceType)99)
        })
            AssertFailure(DocumentationModelBuilder.Build([new(document, "# Text")], Token), "SF-DOC-MODEL-001");
    }

    [Theory]
    [InlineData("Introduction\n\n### Chosen ###\n# Later", "Chosen")]
    [InlineData("#Invalid\n####### Invalid\n\\# Escaped\n\n## Valid", "Valid")]
    [InlineData("```md\n# Not title\n<!--\n```\n\n# Valid", "Valid")]
    [InlineData("~~~\n# Not title\n~~~\nTitle\n=====", "Title")]
    [InlineData("    # Code\n\t# Code too\n\n# Visible", "Visible")]
    [InlineData("<!--\n# Hidden\n-->\n# Visible", "Visible")]
    [InlineData("Title\n-----\n# Later", "Title")]
    [InlineData("   ###### Last Level\n", "Last Level")]
    [InlineData("#\n\n", "Getting Started")]
    [InlineData("# ###\n\n", "Getting Started")]
    [InlineData("No heading.", "Getting Started")]
    [InlineData("", "Getting Started")]
    [InlineData("\uFEFF# Équipe 東京", "Équipe 東京")]
    public void TitlesUseFirstSupportedHeadingThenInvariantFilenameFallback(string markdown, string expected) =>
        Assert.Equal(expected, Assert.Single(Build(Source("docs/getting-started.md", markdown)).Pages).Title);

    [Fact]
    public void TitlesAreBoundedWithoutSplittingUnicodePairs()
    {
        string title = new('x', 199);
        Assert.Equal(title, Build(Source("README.md", "# " + title + "😀tail")).Home!.Title);
    }

    [Fact]
    public void ReadmeVariantsRemainDistinctAndDoNotInventAHome()
    {
        DocumentationSite site = Build(Source("README.fr-FR.md", "# Accueil"), Source("docs/index.md", "# Index"));
        Assert.Null(site.Home);
        Assert.Equal(["/readme.fr-fr", "/index"], site.Pages.Select(static page => page.Route));
        Assert.Empty(Build().Pages);
        Assert.Empty(Build().Navigation.Sections);
    }

    [Fact]
    public void InternalLinksResolveOnlyWithinTheProvidedDocuments()
    {
        DocumentationPage page = Build(Source("README.md"), Source("docs/other page.md"), Source("docs/a/page.md", """
            # Page
            [home](../../README.md#home) [other](<../other%20page.md>) [here](#section)
            [missing](../missing.md) [escape](../../../outside.md) [encoded](%2e%2e/%2e%2e/%2e%2e/outside.md)
            [external](https://example.invalid/page.md) [mail](mailto:a@example.invalid)
            [network](//example.invalid/a.md) ![image](image.md) [asset](image.png)
            `[code](../missing.md)` \[escaped](../missing.md)
            ```md
            [fenced](../missing.md)
            ```
            """)).Pages.Single(static page => page.RelativePath == "docs/a/page.md");
        Assert.Equal(6, page.InternalLinks.Count);
        Assert.Equal(new("../../README.md#home", "README.md", "/", "home"), page.InternalLinks[0]);
        Assert.Equal(new("../other%20page.md", "docs/other page.md", "/other%20page", null), page.InternalLinks[1]);
        Assert.Equal(new("#section", page.LogicalId, page.Route, "section"), page.InternalLinks[2]);
        Assert.All(page.InternalLinks.Skip(3), static link =>
        {
            Assert.False(link.IsResolved);
            Assert.Null(link.TargetRoute);
        });
    }

    [Fact]
    public void InputOrderCultureAndLineEndingsDoNotChangeTheLogicalModel()
    {
        DocumentationSource[] sources = [Source("docs/Z.md", "# Same\r\n"), Source("README.md"), Source("docs/I/intro.md", "No heading\r")];
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            DocumentationSite first = Build(sources);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            DocumentationSite second = Build(sources.Reverse().Select(static source => new DocumentationSource(source.Document, source.Markdown.ReplaceLineEndings("\n"))).ToArray());
            Assert.Equal(Describe(first), Describe(second));
            Assert.Equal("I", Assert.Single(first.Navigation.Sections).Title);
            Assert.Equal(first.Navigation.Sections.Select(static section => section.LogicalPath), second.Navigation.Sections.Select(static section => section.LogicalPath));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void CollectionsAreSnapshotsAndReadOnly()
    {
        List<DocumentationSource> sources = [Source("README.md", "[self](#self)")];
        DocumentationSite site = DocumentationModelBuilder.Build(sources, Token).Site!;
        sources.Clear();
        Assert.Single(site.Pages);
        Assert.Throws<NotSupportedException>(() => ((IList<DocumentationPage>)site.Pages).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DocumentationPage>)site.Navigation.Pages).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DocumentationSection>)site.Navigation.Sections).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DocumentationLink>)site.Home!.InternalLinks).Clear());
    }

    [Fact]
    public void InvalidInputsAlsoProduceAnOrderIndependentDiagnostic()
    {
        DocumentationSource[] sources = [Source("docs/z.md", "\uD800"), Source("../invalid.md")];
        DocumentationModelResult first = DocumentationModelBuilder.Build(sources, Token);
        DocumentationModelResult second = DocumentationModelBuilder.Build(sources.Reverse(), Token);
        AssertFailure(first, "SF-DOC-MODEL-001");
        Assert.Equal(first.Diagnostic, second.Diagnostic);
    }

    [Fact]
    public void ModelContractsExposeOnlyLogicalDataNotRenderingOrFilesystemServices()
    {
        Type[] contracts = [typeof(DocumentationSite), typeof(DocumentationPage), typeof(DocumentationNavigation),
            typeof(DocumentationSection), typeof(DocumentationLink), typeof(DocumentationSource), typeof(DocumentationModelResult)];
        foreach (Type contract in contracts)
        {
            Assert.All(contract.GetProperties(), property => AssertDataType(property.PropertyType));
            Assert.All(contract.GetConstructors().SelectMany(static constructor => constructor.GetParameters()),
                parameter => AssertDataType(parameter.ParameterType));
        }
        Assert.Equal([typeof(IEnumerable<DocumentationSource>), typeof(CancellationToken)],
            typeof(DocumentationModelBuilder).GetMethod("Build")!.GetParameters().Select(static parameter => parameter.ParameterType));
    }

    private static void AssertDataType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            AssertDataType(type.GetGenericArguments()[0]);
            return;
        }
        Assert.True(type == typeof(string) || type == typeof(bool)
            || type == typeof(RepositoryDocument) || type == typeof(DocumentationSourceType)
            || type.Namespace == typeof(DocumentationSite).Namespace, $"Unexpected model dependency: {type.FullName}");
    }

    [Fact]
    public void CancellationAndResourceLimitsAreExplicit()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => DocumentationModelBuilder.Build([], cancelled.Token));
        AssertFailure(DocumentationModelBuilder.Build([Source("README.md", new string('x', RepositoryDocumentationScanner.MaximumDocumentBytes + 1))], Token), "SF-DOC-MODEL-004");
        AssertFailure(DocumentationModelBuilder.Build(Enumerable.Range(0, RepositoryDocumentationScanner.MaximumDocuments + 1).Select(index => Source($"docs/{index}.md")), Token), "SF-DOC-MODEL-004");
        AssertFailure(DocumentationModelBuilder.Build([Source("README.md", string.Concat(Enumerable.Repeat("[x](#x)\n", DocumentationModelBuilder.MaximumLinksPerPage + 1)))], Token), "SF-DOC-MODEL-004");
        AssertFailure(DocumentationModelBuilder.Build([Source("README.md", "\uD800")], Token), "SF-DOC-MODEL-003");
        string links = string.Concat(Enumerable.Repeat("[x](#x)\n", DocumentationModelBuilder.MaximumLinksPerPage));
        AssertFailure(DocumentationModelBuilder.Build(Enumerable.Range(0, DocumentationModelBuilder.MaximumTotalLinks / DocumentationModelBuilder.MaximumLinksPerPage + 1)
            .Select(index => Source($"docs/{index}.md", links)), Token), "SF-DOC-MODEL-004");
    }

    private static DocumentationSource Source(string path, string markdown = "") =>
        new(new(path, path, "Untrusted scanner candidate", path.StartsWith("README", StringComparison.Ordinal)
            ? DocumentationSourceType.RootReadme : DocumentationSourceType.DocumentationPage), markdown);

    private static DocumentationSite Build(params DocumentationSource[] sources)
    {
        DocumentationModelResult result = DocumentationModelBuilder.Build(sources, Token);
        Assert.True(result.IsSuccess, result.Diagnostic?.Code);
        Assert.Null(result.Diagnostic);
        return Assert.IsType<DocumentationSite>(result.Site);
    }

    private static IEnumerable<string> Describe(DocumentationSite site) => site.Pages.Select(static page =>
        $"{page.LogicalId}|{page.Route}|{page.Title}|{page.Markdown}");

    private static void AssertFailure(DocumentationModelResult result, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Site);
        Assert.Equal(code, result.Diagnostic!.Code);
    }
}
