using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests;

/// <summary>Concrete knowledge is confined to the built-in adapter, its packaging and explicit composition root.</summary>
public sealed partial class CoreModuleBoundaryTests(PublicRepositorySnapshot repository) : IClassFixture<PublicRepositorySnapshot>
{
    private static readonly string[] ProgramReferences =
    [
        "LogicGate.SiteFactory.Application.Generation", "LogicGate.SiteFactory.Application.Modules",
        "LogicGate.SiteFactory.Cli", "LogicGate.SiteFactory.Infrastructure.Templates", "LogicGate.SiteFactory.Modules.Documentation",
    ];
    private static readonly string[] DispatcherReferences =
    [
        "LogicGate.SiteFactory.Application.Generation", "LogicGate.SiteFactory.Cli",
        "LogicGate.SiteFactory.Cli.Commands.Generate", "LogicGate.SiteFactory.Cli.Commands.Plan", "LogicGate.SiteFactory.Cli.Commands.Validate",
    ];

    [Fact]
    public void ProductionCouplingsRemainLimitedToTheDocumentedBaseline()
    {
        string root = FindRepositoryRoot();
        string[] paths = Directory.EnumerateFiles(Path.Combine(root, "src"), "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets")
            .Concat(Directory.EnumerateFiles(root, "Directory.*.props"))
            .Concat(Directory.EnumerateFiles(root, "Directory.*.targets"))
            .ToArray();
        Assert.NotEmpty(paths);

        string[] actual = paths.SelectMany(path => ReadCouplings(
                Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
            .Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(DocumentedBaseline().Order(StringComparer.Ordinal).ToArray(), actual);
    }

    [Fact]
    public void PublicEntrypointUsesExplicitPublicComposition()
    {
        string root = FindRepositoryRoot();
        string publicProgram = File.ReadAllText(Path.Combine(root, "src", "LogicGate.SiteFactory.Cli", "Program.cs"));

        Assert.Contains("DocumentationModule", publicProgram, StringComparison.Ordinal);
        Assert.Contains("module.CreateContribution()", publicProgram, StringComparison.Ordinal);
        Assert.Contains("RunConsoleAsync(args, composition)", publicProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInComposition", publicProgram, StringComparison.Ordinal);
        AssertPublicReferences(publicProgram, ProgramReferences);

        string dispatcher = File.ReadAllText(Path.Combine(root, "src", "LogicGate.SiteFactory.Cli", "SiteFactoryCli.cs"));
        Assert.DoesNotContain("BuiltInComposition", dispatcher, StringComparison.Ordinal);
        AssertPublicReferences(dispatcher, DispatcherReferences);
        XDocument publicProject = XDocument.Load(Path.Combine(root, "src", "LogicGate.SiteFactory.Cli", "LogicGate.SiteFactory.Cli.csproj"));
        Assert.Equal("BuiltInComposition.cs", Assert.Single(publicProject.Descendants("Compile")).Attribute("Remove")?.Value);
        AssertReferenceCounterproofs();
    }

    [Theory]
    [InlineData("new TemplateId(\"fixture-sector-template\")")]
    [InlineData("new TemplateId(\"FixtureSectorTemplate\")")]
    [InlineData("new TemplateId(\"FIXTURE SECTOR TEMPLATE\")")]
    [InlineData("new TemplateId(\"FIXTURE_SECTOR_TEMPLATE\")")]
    [InlineData("MinimalTemplateContent.ReadAsync")]
    [InlineData("new EmbeddedTemplateCatalog()")]
    [InlineData("new FixtureTemplateContent()")]
    [InlineData("new UnapprovedModule()")]
    [InlineData("product == \"fixture-product\"")]
    [InlineData("ContactRequest")]
    [InlineData("DocumentationModule")]
    [InlineData("LogicGate.SiteFactory.Modules.Content")]
    [InlineData("public sealed class Lead")]
    [InlineData("public sealed record Content")]
    public void GuardDetectsNewConcreteDependencies(string source)
    {
        // Pure mutation probe: no repository source is edited to test the guard.
        string coupling = Assert.Single(ReadCouplings("LogicGate.SiteFactory.Domain/NewFeature.cs", source));
        Assert.DoesNotContain(coupling, DocumentedBaseline());
    }

    [Theory]
    [InlineData("TemplateId templateId; ITemplateCatalog catalog; TemplateManifest manifest;")]
    [InlineData("ModuleId module; Capability capability; GenerationContribution contribution;")]
    [InlineData("IReadOnlyList<(PlannedFile File, string? Content)> files;")]
    public void GuardAllowsGenericConcepts(string source)
    {
        Assert.Empty(ReadCouplings("LogicGate.SiteFactory.Application/Generic.cs", source));
    }

    [Fact]
    public void GuardRejectsReintroducingConcreteKnowledgeIntoTheExtractedValidator()
    {
        const string path = "src/LogicGate.SiteFactory.Application/Configuration/ClientConfigurationV1Validator.cs";
        string current = File.ReadAllText(Path.Combine(FindRepositoryRoot(), path.Replace('/', Path.DirectorySeparatorChar)));
        string[] original = ReadCouplings(path, current).ToArray();
        string[] changed = ReadCouplings(path, current + "\n// LogicGate.SiteFactory.Modules.Unapproved\n").ToArray();

        Assert.Empty(original);
        Assert.Single(changed);
        Assert.False(original.SequenceEqual(changed, StringComparer.Ordinal));
        const string approvedPath = "src/LogicGate.SiteFactory.Cli/Program.cs";
        string approved = Assert.Single(ReadCouplings(approvedPath, "DocumentationModule"));
        Assert.Contains(approved, DocumentedBaseline());
        foreach (string source in new[] { "new UnapprovedModule()", "LogicGate.SiteFactory.Modules.Unapproved" })
        {
            string replacement = Assert.Single(ReadCouplings(approvedPath, source));
            Assert.NotEqual(approved, replacement);
            Assert.DoesNotContain(replacement, DocumentedBaseline());
        }
    }

    private void AssertReferenceCounterproofs()
    {
        string root = FindRepositoryRoot();
        foreach ((string file, string[] expected) in new[] { ("Program.cs", ProgramReferences), ("SiteFactoryCli.cs", DispatcherReferences) })
        {
            string source = File.ReadAllText(Path.Combine(root, "src", "LogicGate.SiteFactory.Cli", file));
            AssertPublicReferences(source, expected);
            foreach (string dependency in new[] { "LogicGate.SiteFactory.Modules.Unapproved",
                         "LogicGate.SiteFactory.Application.Unapproved", "LogicGate.SiteFactory.Unapproved.Composition" })
                Assert.ThrowsAny<Exception>(() => AssertPublicReferences(source + "\nusing " + dependency + ";\n", expected));
        }
    }

    private static void AssertPublicReferences(string source, string[] expected) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal),
            QualifiedReference().Matches(source).Select(static match => match.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static IEnumerable<string> ReadCouplings(string path, string source) =>
        ConcreteDependency().Matches(source).Select(match => $"{path}|{Category(match)}");

    private static string Category(Match match) => match.Groups["template"].Success ? "template"
        : match.Groups["contentAdapter"].Success ? "content-adapter"
        : match.Groups["catalogAdapter"].Success ? "catalog-adapter"
        : match.Groups["offer"].Success ? "offer"
        : match.Groups["moduleName"].Success && match.Groups["moduleName"].Value != "Documentation"
            || match.Groups["moduleType"].Success && match.Groups["moduleType"].Value != "DocumentationModule"
            ? "unapproved-module" : "business-module";

    private static IEnumerable<string> DocumentedBaseline()
    {
        // SF-TASK-055 moves adapter resources out of Infrastructure as well. These exact files own the adapter/composition,
        // not an allowance for generic code or a whole Infrastructure directory.
        (string Path, string Category, int Count)[] entries =
        [
            ("src/LogicGate.SiteFactory.Cli/Program.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/DocumentationModule.cs", "business-module", 6),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Rendering/DocumentationSiteTemplate.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Rendering/DocumentationHtmlRenderer.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Model/DocumentationSite.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Model/DocumentationSourcePath.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Model/MarkdownOutline.cs", "business-module", 1),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Model/DocumentationModelBuilder.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Model/DocumentationModelLoader.cs", "business-module", 2),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Scanning/DocumentationScanResult.cs", "business-module", 1),
            ("src/LogicGate.SiteFactory.Modules.Documentation/Scanning/RepositoryDocumentationScanner.cs", "business-module", 1),
        ];
        return entries.SelectMany(entry => Enumerable.Repeat($"{entry.Path}|{entry.Category}", entry.Count));
    }

    [GeneratedRegex("""(?<template>\b(?:TemplateId|ModuleId)\s*\(\s*"[^"]+")|(?<contentAdapter>\b(?:MinimalTemplateContent|FixtureTemplateContent)\b)|(?<catalogAdapter>\bEmbeddedTemplateCatalog\b)|(?<offer>\bproduct\s*(?:==|!=)\s*"[^"]+")|\bContactRequest\w*\b|(?<moduleType>(?-i:\b(?!ISiteFactoryModule\b)[A-Z]\w*Module\b))|(?<moduleNamespace>\bLogicGate\.SiteFactory\.Modules\.(?<moduleName>\w+)\b)|\b(?:class|record|struct|interface|enum)\s+(?:Documentation|Lead|Content)\b""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConcreteDependency();

    [GeneratedRegex(@"\bLogicGate\.SiteFactory\.[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*", RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedReference();

    private string FindRepositoryRoot() => repository.Root;
}
