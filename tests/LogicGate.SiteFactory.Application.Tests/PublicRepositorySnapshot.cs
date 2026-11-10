using System.Xml.Linq;

namespace LogicGate.SiteFactory.Application.Tests;

/// <summary>Actual public sources in an owned snapshot, or the explicitly supplied isolated public checkout.</summary>
public sealed class PublicRepositorySnapshot : IDisposable
{
    private readonly string? ownedDirectory;
    private static readonly string[] Projects =
    [
        "Application", "Cli", "Domain", "Infrastructure", "Infrastructure.PostgreSql",
        "Migrator", "ModuleSdk", "Modules.Documentation", "Runtime", "Runtime.AspNetCore",
        "Runtime.Contracts", "Runtime.Hosting",
    ];

    public PublicRepositorySnapshot()
    {
        string? explicitRoot = Environment.GetEnvironmentVariable("SITEFACTORY_TEST_PUBLIC_REPOSITORY");
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            Root = Path.GetFullPath(explicitRoot);
            if (!File.Exists(Path.Combine(Root, "SiteFactory.slnx")))
                throw new InvalidOperationException("The explicit public test checkout is missing.");
            return;
        }

        string source = FindCheckout();
        ownedDirectory = Directory.CreateTempSubdirectory("sitefactory-public-guard-").FullName;
        Root = ownedDirectory;
        try
        {
            foreach (string name in new[] { "Directory.Build.props", "Directory.Packages.props" })
                File.Copy(Path.Combine(source, name), Path.Combine(Root, name));
            foreach (string name in Projects)
            {
                string relative = Path.Combine("src", "LogicGate.SiteFactory." + name);
                string original = Path.Combine(source, relative);
                XDocument project = XDocument.Load(Path.Combine(original, "LogicGate.SiteFactory." + name + ".csproj"));
                string[] excluded = project.Descendants("Compile").Attributes("Remove").Select(static item => item.Value).ToArray();
                foreach (string file in Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories))
                {
                    string local = Path.GetRelativePath(original, file);
                    if (local.Split(Path.DirectorySeparatorChar).Any(static segment => segment is "bin" or "obj")
                        || excluded.Any(pattern => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, local)))
                        continue;
                    string target = Path.Combine(Root, relative, local);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
                }
            }
            new XDocument(new XElement("Solution", Projects.Select(name =>
                new XElement("Project", new XAttribute("Path",
                    $"src/LogicGate.SiteFactory.{name}/LogicGate.SiteFactory.{name}.csproj")))))
                .Save(Path.Combine(Root, "SiteFactory.slnx"));
        }
        catch
        {
            Directory.Delete(ownedDirectory, recursive: true);
            throw;
        }
    }

    public string Root { get; }

    public void Dispose()
    {
        if (ownedDirectory is not null)
            Directory.Delete(ownedDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static string FindCheckout()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "SiteFactory.slnx")))
                return current.FullName;
        throw new InvalidOperationException("Public source checkout not found.");
    }
}
