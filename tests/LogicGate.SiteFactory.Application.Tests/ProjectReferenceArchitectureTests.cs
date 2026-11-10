using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests;

public sealed class ProjectReferenceArchitectureTests(PublicRepositorySnapshot repository) : IClassFixture<PublicRepositorySnapshot>
{
    [Fact]
    public void ProductionProjectsUseTheExactAllowedReferenceMatrix()
    {
        AssertExactGraph(ReadProductionGraph());
    }

    private static void AssertExactGraph(Dictionary<string, string[]> actual)
    {
        Dictionary<string, string[]> expected = CreateExpectedGraph();

        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(
            expected.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
            actual.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray());

        foreach (string projectName in expected.Keys.OrderBy(
                     static name => name,
                     StringComparer.Ordinal))
        {
            Assert.Equal(expected[projectName], actual[projectName]);
        }
    }

    [Fact]
    public void ProductionProjectReferenceGraphIsAcyclic()
    {
        Dictionary<string, string[]> graph = ReadProductionGraph();
        HashSet<string> visiting = new(StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal);

        foreach (string projectName in graph.Keys.OrderBy(
                     static name => name,
                     StringComparer.Ordinal))
        {
            Visit(projectName, graph, visiting, visited);
        }
    }

    [Fact]
    public void ExactMatrixRejectsAnAdditionalForbiddenReference()
    {
        Dictionary<string, string[]> graph = CreateExpectedGraph();
        graph["LogicGate.SiteFactory.Domain"] = ["LogicGate.SiteFactory.Infrastructure"];
        Assert.NotNull(Record.Exception(() => AssertExactGraph(graph)));
    }

    [Fact]
    public void CycleGuardRejectsAnIntroducedCycle()
    {
        Dictionary<string, string[]> graph = CreateExpectedGraph();
        graph["LogicGate.SiteFactory.Domain"] = ["LogicGate.SiteFactory.Application"];
        Assert.Throws<InvalidOperationException>(() =>
            Visit("LogicGate.SiteFactory.Application", graph, new(StringComparer.Ordinal), new(StringComparer.Ordinal)));
    }

    private static Dictionary<string, string[]> CreateExpectedGraph()
    {
        return new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["LogicGate.SiteFactory.Domain"] = [],
            ["LogicGate.SiteFactory.ModuleSdk"] = [],
            ["LogicGate.SiteFactory.Runtime.Contracts"] = [],
            ["LogicGate.SiteFactory.Runtime"] = ["LogicGate.SiteFactory.Runtime.Contracts"],
            ["LogicGate.SiteFactory.Runtime.Hosting"] = ["LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts"],
            ["LogicGate.SiteFactory.Runtime.AspNetCore"] = ["LogicGate.SiteFactory.Runtime.Hosting"],
            ["LogicGate.SiteFactory.Infrastructure.PostgreSql"] = ["LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts", "LogicGate.SiteFactory.Runtime.Hosting"],
            ["LogicGate.SiteFactory.Migrator"] = ["LogicGate.SiteFactory.Infrastructure.PostgreSql"],
            ["LogicGate.SiteFactory.Application"] =
                ["LogicGate.SiteFactory.Domain", "LogicGate.SiteFactory.ModuleSdk"],
            ["LogicGate.SiteFactory.Infrastructure"] =
            [
                "LogicGate.SiteFactory.Application",
                "LogicGate.SiteFactory.Domain",
            ],
            ["LogicGate.SiteFactory.Cli"] =
            [
                "LogicGate.SiteFactory.Application",
                "LogicGate.SiteFactory.Infrastructure",
                "LogicGate.SiteFactory.Modules.Documentation",
            ],
            ["LogicGate.SiteFactory.Modules.Documentation"] =
            [
                "LogicGate.SiteFactory.Application",
                "LogicGate.SiteFactory.Domain",
                "LogicGate.SiteFactory.ModuleSdk",
            ],
        };
    }

    private Dictionary<string, string[]> ReadProductionGraph()
    {
        string sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        string[] projectPaths = Directory
            .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(static path => Path.GetFullPath(path))
            .OrderBy(static path => path, pathComparer)
            .ToArray();

        Dictionary<string, string> namesByPath = new(pathComparer);
        Dictionary<string, string> pathsByName = new(StringComparer.Ordinal);

        foreach (string projectPath in projectPaths)
        {
            string projectName = Path.GetFileNameWithoutExtension(projectPath);

            if (!pathsByName.TryAdd(projectName, projectPath))
            {
                throw new InvalidOperationException(
                    $"Duplicate production project name '{projectName}'.");
            }

            namesByPath.Add(projectPath, projectName);
        }

        Dictionary<string, string[]> graph = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> project in pathsByName)
        {
            XDocument document = LoadProject(project.Value);
            string projectDirectory = Path.GetDirectoryName(project.Value)
                ?? throw new InvalidOperationException(
                    $"Cannot resolve directory for project '{project.Key}'.");

            List<string> references = [];

            foreach (XElement element in document
                         .Descendants()
                         .Where(static element =>
                             element.Name.LocalName == "ProjectReference"))
            {
                string include = element.Attribute("Include")?.Value
                    ?? throw new InvalidOperationException(
                        $"Project '{project.Key}' contains a ProjectReference without Include.");

                string portableInclude = include
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);

                string targetPath = Path.GetFullPath(
                    Path.Combine(projectDirectory, portableInclude));

                if (!namesByPath.TryGetValue(targetPath, out string? targetName))
                {
                    throw new InvalidOperationException(
                        $"Project '{project.Key}' references unknown production project " +
                        $"'{Path.GetFileNameWithoutExtension(include)}'.");
                }

                references.Add(targetName);
            }

            graph.Add(
                project.Key,
                references
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray());
        }

        return graph;
    }

    private static XDocument LoadProject(string path)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using XmlReader reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private string FindRepositoryRoot() => repository.Root;

    private static void Visit(
        string projectName,
        Dictionary<string, string[]> graph,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(projectName))
        {
            return;
        }

        if (!visiting.Add(projectName))
        {
            throw new InvalidOperationException(
                $"Project reference cycle detected through '{projectName}'.");
        }

        foreach (string reference in graph[projectName])
        {
            Visit(reference, graph, visiting, visited);
        }

        visiting.Remove(projectName);
        visited.Add(projectName);
    }
}
