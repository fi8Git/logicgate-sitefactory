using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests;

public sealed class NuGetPackageSourceMappingTests
{
    private const string InternalProbePackageId = "LogicGate.SiteFactory.MappingProbe";

    [Fact]
    public void NuGetOrgMappingAllowsOnlyCurrentPublicPackageFamilies()
    {
        string configurationPath = Path.Combine(FindRepositoryRoot(), "NuGet.config");
        XDocument document = LoadConfiguration(configurationPath);
        XElement packageSources = Assert.Single(
            document.Descendants(),
            static element => element.Name.LocalName == "packageSources");
        XElement configuredSource = Assert.Single(
            packageSources.Elements(),
            static element => element.Name.LocalName == "add");
        XElement packageSourceMapping = Assert.Single(
            document.Descendants(),
            static element => element.Name.LocalName == "packageSourceMapping");
        XElement nugetOrgSource = Assert.Single(
            packageSourceMapping.Elements(),
            static element => element.Name.LocalName == "packageSource");

        Assert.Equal("nuget.org", configuredSource.Attribute("key")?.Value);
        Assert.Equal("nuget.org", nugetOrgSource.Attribute("key")?.Value);

        string[] patterns = nugetOrgSource
            .Elements()
            .Where(static element => element.Name.LocalName == "package")
            .Select(static element => element.Attribute("pattern")?.Value
                ?? throw new InvalidOperationException(
                    "A package source mapping entry has no pattern."))
            .OrderBy(static pattern => pattern, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Markdig", "Microsoft.*", "Microsoft.Extensions.Hosting", "Npgsql", "Npgsql.EntityFrameworkCore.PostgreSQL", "System.Diagnostics.EventLog", "YamlDotNet", "xunit*"], patterns);

        foreach (string pattern in patterns)
        {
            Assert.False(
                MatchesPackageId(pattern, InternalProbePackageId),
                $"nuget.org pattern '{pattern}' maps the internal package namespace.");
        }
    }

    [Fact]
    public void MarkdownPackageIsReferencedOnlyByTheDocumentationModule()
    {
        string[] consumers = Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => LoadConfiguration(path).Descendants().Any(static element => element.Name.LocalName == "PackageReference"
                && (string?)element.Attribute("Include") == "Markdig"))
            .Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).ToArray()!;
        Assert.Equal(["LogicGate.SiteFactory.Modules.Documentation"], consumers);
    }

    private static bool MatchesPackageId(string pattern, string packageId)
    {
        return pattern.EndsWith('*')
            ? packageId.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, packageId, StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadConfiguration(string path)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using XmlReader reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SiteFactory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
