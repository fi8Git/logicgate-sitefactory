using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Tests;

public sealed class DependencyBoundaryTests
{
    private const string ContractsReference = "../LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj";
    private static readonly string[] ForbiddenNamespaces = ["System.IO", "System.Net", "Microsoft", "Npgsql", "Client",
        "LogicGate.SiteFactory.Infrastructure", "LogicGate.SiteFactory.Modules", "LogicGate.SiteFactory.ModuleSdk",
        "LogicGate.SiteFactory.Application", "LogicGate.SiteFactory.Cli"];

    [Fact]
    public void RuntimeDependsOnlyOnContractsAndBclInTheProjectRestoreAndAssembly()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Runtime");
        AssertRuntimeProject(ReadProject(Path.Combine(directory, "LogicGate.SiteFactory.Runtime.csproj")));
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json")));
        JsonProperty library = Assert.Single(assets.RootElement.GetProperty("libraries").EnumerateObject());
        Assert.StartsWith("LogicGate.SiteFactory.Runtime.Contracts/", library.Name, StringComparison.Ordinal);
        Assert.Equal("project", library.Value.GetProperty("type").GetString());
        foreach (JsonProperty framework in assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            Assert.Equal(["Microsoft.NETCore.App"], framework.Value.GetProperty("frameworkReferences")
                .EnumerateObject().Select(static reference => reference.Name));
        }

        foreach (System.Reflection.AssemblyName reference in typeof(DatabaseCompatibilityProbe).Assembly.GetReferencedAssemblies())
        {
            Assert.True(reference.Name == "LogicGate.SiteFactory.Runtime.Contracts"
                || reference.Name!.StartsWith("System.", StringComparison.Ordinal), reference.Name);
        }

        Assert.Empty(typeof(DatabaseCompatibilityProbe).Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false));
        using FileStream stream = File.OpenRead(typeof(DatabaseCompatibilityProbe).Assembly.Location);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference reference = metadata.GetTypeReference(handle);
            string typeNamespace = metadata.GetString(reference.Namespace);
            Assert.DoesNotContain(ForbiddenNamespaces,
                prefix => typeNamespace == prefix || typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal));
            Assert.False(typeNamespace == "System.Diagnostics" && metadata.GetString(reference.Name) == "Process");
        }
    }

    [Theory]
    [InlineData("ProjectReference")]
    [InlineData("PackageReference")]
    [InlineData("FrameworkReference")]
    [InlineData("Reference")]
    [InlineData("InternalsVisibleTo")]
    [InlineData("Compile")]
    public void GuardRejectsAdditionalDependenciesAndLinkedSourcesInMemory(string element)
    {
        XDocument project = new(new XElement("Project", new XElement("ItemGroup",
            new XElement("ProjectReference", new XAttribute("Include", ContractsReference)),
            new XElement(element, new XAttribute("Include", "Forbidden.Dependency")))));
        Assert.ThrowsAny<Exception>(() => AssertRuntimeProject(project));
    }

    [Fact]
    public void PublicSurfaceContainsExactlyTheNineCompatibilityTypesAndTheStartupPipelineUsingExistingContracts()
    {
        Type[] expected = [typeof(DatabaseReadOutcome), typeof(DatabaseRolePrivileges), typeof(DatabaseRoleObservation),
            typeof(DatabaseIdentityObservation), typeof(DatabaseSchemaObservation), typeof(RuntimeDatabaseObservation),
            typeof(DatabaseCompatibilityExpectation), typeof(IRuntimeDatabaseReader), typeof(DatabaseCompatibilityProbe)];
        Assert.Equal(expected.Append(typeof(RuntimeStartupValidator)).OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(DatabaseCompatibilityProbe).Assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal));
        Assert.All(expected, static type => Assert.Equal("LogicGate.SiteFactory.Runtime.Database", type.Namespace));
        Assert.Equal("LogicGate.SiteFactory.Runtime", typeof(RuntimeStartupValidator).Namespace);
        Assert.True(typeof(RuntimeStartupValidator).IsSealed);
        Assert.Equal([typeof(IRuntimeStartupValidator)], typeof(RuntimeStartupValidator).GetInterfaces());
        System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(RuntimeStartupValidator).GetConstructors());
        Assert.Equal(typeof(IEnumerable<IStartupValidation>), Assert.Single(constructor.GetParameters()).ParameterType);
        System.Reflection.MethodInfo method = Assert.Single(typeof(RuntimeStartupValidator).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly));
        Assert.Equal("ValidateAsync", method.Name);
        Assert.Equal(typeof(Task<RuntimeStartupReport>), method.ReturnType);
        Assert.Equal(typeof(CancellationToken), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Empty(typeof(RuntimeStartupValidator).GetProperties());
        Assert.Empty(typeof(RuntimeStartupValidator).GetFields());
        Assert.Equal(typeof(RuntimeIdentity), typeof(DatabaseCompatibilityExpectation).GetProperty("Identity")!.PropertyType);
        Assert.Equal(typeof(Task<IReadOnlyList<StartupValidationResult>>),
            typeof(DatabaseCompatibilityProbe).GetMethod("ValidateAsync")!.ReturnType);
        Assert.DoesNotContain(typeof(DatabaseCompatibilityProbe).Assembly.GetExportedTypes(),
            static type => type.Namespace == "LogicGate.SiteFactory.Runtime.Contracts");
    }

    [Fact]
    public void AutonomousTestsReferenceOnlyRuntimeAndExistingCentralizedTestPackages()
    {
        XDocument project = ReadProject(Path.Combine(FindRepositoryRoot(), "tests", "LogicGate.SiteFactory.Runtime.Tests",
            "LogicGate.SiteFactory.Runtime.Tests.csproj"));
        XElement reference = Assert.Single(project.Descendants(), static element => element.Name.LocalName == "ProjectReference");
        Assert.Equal("../../src/LogicGate.SiteFactory.Runtime/LogicGate.SiteFactory.Runtime.csproj", reference.Attribute("Include")?.Value);
        Assert.Equal(["Microsoft.NET.Test.Sdk", "xunit.runner.visualstudio", "xunit.v3"],
            project.Descendants().Where(static element => element.Name.LocalName == "PackageReference")
                .Select(static element => element.Attribute("Include")?.Value).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is "Compile" or "FrameworkReference" or "Reference" or "InternalsVisibleTo" or "Import");
        Assert.Equal(["LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts"],
            typeof(DependencyBoundaryTests).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name)
                .Where(static name => name!.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal))
                .OrderBy(static name => name, StringComparer.Ordinal));
    }

    private static void AssertRuntimeProject(XDocument project)
    {
        XElement reference = Assert.Single(project.Descendants(), static element => element.Name.LocalName == "ProjectReference");
        Assert.Equal(ContractsReference, reference.Attribute("Include")?.Value);
        Assert.Single(reference.Attributes());
        Assert.DoesNotContain(project.Descendants(), static element =>
            element.Name.LocalName is "PackageReference" or "FrameworkReference" or "Reference" or "InternalsVisibleTo" or "Compile" or "Import");
    }

    private static XDocument ReadProject(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SiteFactory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
