using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

/// <summary>Builds unchanged production code with a fixture replacing only the embedded manifest.</summary>
internal sealed class PublicPlanCliBuild : IDisposable
{
    private static readonly string[] RootFiles = ["global.json", "Directory.Build.props", "Directory.Packages.props", ".editorconfig"];
    private static readonly string[] ProjectNames = ["Domain", "ModuleSdk", "Application", "Infrastructure", "Modules.Documentation", "Cli"];
    private readonly string directory = Directory.CreateTempSubdirectory("sitefactory-plan-build-").FullName;

    public string CliAssemblyPath => Path.Combine(directory, "src", "LogicGate.SiteFactory.Cli", "bin", "Release", "net10.0", "LogicGate.SiteFactory.Cli.dll");

    public async Task BuildAsync(string manifestFixturePath, CancellationToken cancellationToken)
    {
        string repository = FindRepositoryRoot();
        foreach (string file in RootFiles)
            File.Copy(Path.Combine(repository, file), Path.Combine(directory, file));

        foreach (string name in ProjectNames)
        {
            string relativePath = Path.Combine("src", "LogicGate.SiteFactory." + name);
            CopySourceTree(Path.Combine(repository, relativePath), Path.Combine(directory, relativePath));
        }

        string manifestDirectory = Path.Combine(directory, "templates", "documentation-minimal");
        CopySourceTree(Path.Combine(repository, "templates", "documentation-minimal"), manifestDirectory);
        File.Copy(manifestFixturePath, Path.Combine(manifestDirectory, "template.json"), overwrite: true);

        // Reuse exactly the already-restored public packages; no network feed or production mapping change.
        string feed = Directory.CreateDirectory(Path.Combine(directory, "offline-packages")).FullName;
        CopyRestoredPackages(Path.Combine(repository, "src", "LogicGate.SiteFactory.Cli", "obj", "project.assets.json"), feed);
        XDocument nugetConfig = XDocument.Load(Path.Combine(repository, "NuGet.config"));
        XElement sources = Assert.Single(nugetConfig.Descendants("packageSources"));
        XElement publicSource = Assert.Single(sources.Elements("add"));
        Assert.Equal("nuget.org", publicSource.Attribute("key")?.Value);
        publicSource.SetAttributeValue("value", feed);
        publicSource.Attribute("protocolVersion")?.Remove(); // The isolated source is a folder, not a V3 HTTP endpoint.
        string configPath = Path.Combine(directory, "NuGet.config");
        nugetConfig.Save(configPath);

        string project = Path.Combine("src", "LogicGate.SiteFactory.Cli", "LogicGate.SiteFactory.Cli.csproj");
        await RunSetupAsync(["restore", project, "--locked-mode", "--configfile", configPath,
            "--packages", Path.Combine(directory, "packages"), "--disable-parallel", "-nr:false"], cancellationToken);
        await RunSetupAsync(["build", project, "--configuration", "Release", "--no-restore", "--disable-build-servers"], cancellationToken);
        Assert.True(File.Exists(CliAssemblyPath));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(CliAssemblyPath)!, "LogicGate.SiteFactory.Modules.Documentation.dll")));
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task RunSetupAsync(string[] arguments, CancellationToken cancellationToken)
    {
        CliBytesResult result = await CliProcessRunner.RunDotnetAsync(directory, arguments, TimeSpan.FromMinutes(3), cancellationToken);
        Assert.True(result.ExitCode == 0,
            $"Isolated test setup failed ({arguments[0]}):\n{Encoding.UTF8.GetString(result.StandardOutput)}\n{Encoding.UTF8.GetString(result.StandardError)}");
    }

    private static void CopySourceTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        string[] excluded = Directory.EnumerateFiles(source, "*.csproj")
            .SelectMany(path => XDocument.Load(path).Descendants("Compile").Attributes("Remove"))
            .Select(static attribute => attribute.Value).ToArray();
        foreach (string file in Directory.EnumerateFiles(source).Order(StringComparer.Ordinal))
        {
            if (!excluded.Any(pattern => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, Path.GetFileName(file))))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string child in Directory.EnumerateDirectories(source).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(child);
            if (name is not ("bin" or "obj"))
                CopySourceTree(child, Path.Combine(destination, name));
        }
    }

    internal static void CopyRestoredPackages(string assetsPath, string feed)
    {
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        string[] packageRoots = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(static property => property.Name).ToArray();

        foreach (JsonProperty library in assets.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() != "package")
                continue;

            string packagePath = library.Value.GetProperty("path").GetString()!;
            string packageDirectory = packageRoots.Select(root => Path.Combine(root, packagePath))
                .First(Directory.Exists);
            string archive = Assert.Single(Directory.EnumerateFiles(packageDirectory, "*.nupkg"));
            File.Copy(archive, Path.Combine(feed, Path.GetFileName(archive)));
        }
    }

    internal static string FindRepositoryRoot()
    {
        for (DirectoryInfo? candidate = new(AppContext.BaseDirectory); candidate is not null; candidate = candidate.Parent)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "SiteFactory.slnx")))
                return candidate.FullName;
        }

        throw new InvalidOperationException("The end-to-end build tests must run from a restored repository checkout.");
    }
}
