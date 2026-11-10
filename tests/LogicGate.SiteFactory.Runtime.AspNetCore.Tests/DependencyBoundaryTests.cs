using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.AspNetCore.Tests;

public sealed class DependencyBoundaryTests
{
    private const string HostingReference = "../LogicGate.SiteFactory.Runtime.Hosting/LogicGate.SiteFactory.Runtime.Hosting.csproj";
    private static readonly string[] ApprovedNamespaces = ["Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Diagnostics.HealthChecks",
        "Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Routing", "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Diagnostics.HealthChecks", "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Primitives", "LogicGate.SiteFactory.Runtime.Hosting"];

    [Fact]
    public void ProductionProjectAssetsAndAssemblyUseOnlyApprovedHttpAndHostingDependencies()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Runtime.AspNetCore");
        AssertProductionProject(ReadProject(Path.Combine(directory, "LogicGate.SiteFactory.Runtime.AspNetCore.csproj")));
        string source = File.ReadAllText(Path.Combine(directory, "RuntimeHealthEndpoints.cs"));
        AssertSourceBoundary(source);
        foreach (string forbidden in new[] { "using LogicGate.SiteFactory.Modules.Unapproved;", "new DatabaseCompatibilityProbe()", "CurrentReport" })
            Assert.ThrowsAny<Exception>(() => AssertSourceBoundary(forbidden));
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json")));
        JsonElement libraries = assets.RootElement.GetProperty("libraries");
        // The shared framework supplies Extensions; restore prunes the packages from this HTTP graph.
        Assert.Equal(["LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts", "LogicGate.SiteFactory.Runtime.Hosting"],
            libraries.EnumerateObject().Select(static library => library.Name.Split('/')[0]).Order(StringComparer.Ordinal));
        foreach (JsonProperty framework in assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
            Assert.Equal(["Microsoft.AspNetCore.App", "Microsoft.NETCore.App"], framework.Value.GetProperty("frameworkReferences")
                .EnumerateObject().Select(static reference => reference.Name).Order(StringComparer.Ordinal));

        Assembly assembly = typeof(RuntimeHealthEndpoints).Assembly;
        Assert.Equal(["LogicGate.SiteFactory.Runtime.Hosting"], assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name).Where(static name => name!.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal)));
        Assert.Empty(assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false));
        using FileStream stream = File.OpenRead(assembly.Location);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference reference = metadata.GetTypeReference(handle);
            string typeNamespace = ResolveNamespace(metadata, reference);
            AssertNamespace(typeNamespace);
            if (typeNamespace == "LogicGate.SiteFactory.Runtime.Hosting")
                Assert.Contains(metadata.GetString(reference.Name), new[] { nameof(RuntimeStartupService), nameof(IRuntimeReadinessRefresh) });
        }
    }

    [Fact]
    public void PublicSurfaceIsExactlyTheThreeApprovedExtensionMethods()
    {
        Type type = typeof(RuntimeHealthEndpoints);
        Assert.Equal([type], type.Assembly.GetExportedTypes());
        Assert.Equal("LogicGate.SiteFactory.Runtime.AspNetCore", type.Namespace);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetConstructors());
        Assert.Empty(type.GetFields());
        Assert.Empty(type.GetProperties());
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(["AddRuntimeHealthChecks", "MapRuntimeHealthEndpoints", "UseRuntimeReadinessGate"],
            methods.Select(static method => method.Name).Order(StringComparer.Ordinal));
        foreach (MethodInfo method in methods)
        {
            Assert.True(method.IsDefined(typeof(ExtensionAttribute), inherit: false));
            ParameterInfo parameter = Assert.Single(method.GetParameters());
            Assert.False(parameter.HasDefaultValue);
            (Type input, Type output) = method.Name switch
            {
                "AddRuntimeHealthChecks" => (typeof(IServiceCollection), typeof(IHealthChecksBuilder)),
                "MapRuntimeHealthEndpoints" => (typeof(IEndpointRouteBuilder), typeof(void)),
                _ => (typeof(WebApplication), typeof(void)),
            };
            Assert.Equal(input, parameter.ParameterType);
            Assert.Equal(output, method.ReturnType);
        }
    }

    [Theory]
    [InlineData("ProjectReference")]
    [InlineData("PackageReference")]
    [InlineData("FrameworkReference")]
    [InlineData("Reference")]
    [InlineData("Compile")]
    [InlineData("Import")]
    [InlineData("InternalsVisibleTo")]
    public void ProjectGuardRejectsExtraDependenciesOrLinkedSourcesInMemory(string element)
    {
        XDocument project = new(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", HostingReference)),
                new XElement("FrameworkReference", new XAttribute("Include", "Microsoft.AspNetCore.App")))));
        AssertProductionProject(project);
        project.Root!.Add(new XElement("ItemGroup", new XElement(element, new XAttribute("Include", "Forbidden.Dependency"))));
        Assert.ThrowsAny<Exception>(() => AssertProductionProject(project));
    }

    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("LogicGate.SiteFactory.Infrastructure.PostgreSql")]
    [InlineData("LogicGate.SiteFactory.Runtime.Database")]
    [InlineData("LogicGate.SiteFactory.Runtime.Contracts")]
    [InlineData("LogicGate.SiteFactory.ModuleSdk")]
    [InlineData("LogicGate.SiteFactory.Modules.Unapproved")]
    [InlineData("Client.Web")]
    public void MetadataGuardRejectsProvidersPrivateCompositionAndCopiedPolicies(string typeNamespace) =>
        Assert.ThrowsAny<Exception>(() => AssertNamespace(typeNamespace));

    private static void AssertSourceBoundary(string source)
    {
        foreach (string forbidden in new[] { "DatabaseCompatibilityProbe", "RuntimeDatabaseObservation", "sitefactory_runtime",
            "SF-DB-STARTUP", "Npgsql", "EntityFrameworkCore", "WebDatabaseReadiness", "LogicGate.SiteFactory.Modules.", "CurrentReport" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    [Fact]
    public void TestsUseOnlyTheirApprovedPublicProjectsFrameworkAndExistingTestPackages()
    {
        XDocument project = ReadProject(Path.Combine(FindRepositoryRoot(), "tests", "LogicGate.SiteFactory.Runtime.AspNetCore.Tests",
            "LogicGate.SiteFactory.Runtime.AspNetCore.Tests.csproj"));
        Assert.Equal(["../../src/LogicGate.SiteFactory.Runtime.AspNetCore/LogicGate.SiteFactory.Runtime.AspNetCore.csproj",
                "../../src/LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj",
                "../../src/LogicGate.SiteFactory.Runtime.Hosting/LogicGate.SiteFactory.Runtime.Hosting.csproj"], Values(project, "ProjectReference"));
        Assert.Equal(["Microsoft.AspNetCore.App"], Values(project, "FrameworkReference"));
        Assert.Equal(["Microsoft.NET.Test.Sdk", "xunit.runner.visualstudio", "xunit.v3"], Values(project, "PackageReference"));
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is "Reference" or "Compile" or "Import" or "InternalsVisibleTo");
        Assert.Equal(["LogicGate.SiteFactory.Runtime.AspNetCore", "LogicGate.SiteFactory.Runtime.Contracts", "LogicGate.SiteFactory.Runtime.Hosting"],
            typeof(DependencyBoundaryTests).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name)
                .Where(static name => name!.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    private static string ResolveNamespace(MetadataReader metadata, TypeReference reference)
    {
        string typeNamespace = metadata.GetString(reference.Namespace);
        return typeNamespace.Length == 0 && reference.ResolutionScope.Kind == HandleKind.TypeReference
            ? ResolveNamespace(metadata, metadata.GetTypeReference((TypeReferenceHandle)reference.ResolutionScope))
            : typeNamespace;
    }

    private static void AssertNamespace(string typeNamespace) => Assert.True(typeNamespace == "System"
        || typeNamespace.StartsWith("System.", StringComparison.Ordinal)
        || ApprovedNamespaces.Any(prefix => typeNamespace == prefix || typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal)),
        "Unapproved HTTP dependency: " + typeNamespace);

    private static void AssertProductionProject(XDocument project)
    {
        Assert.Equal("Microsoft.NET.Sdk", project.Root!.Attribute("Sdk")?.Value);
        Assert.Equal([HostingReference], Values(project, "ProjectReference"));
        Assert.Equal(["Microsoft.AspNetCore.App"], Values(project, "FrameworkReference"));
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is "PackageReference" or "Reference"
            or "Compile" or "Import" or "InternalsVisibleTo");
        Assert.All(project.Descendants().Where(static element => element.Name.LocalName is "ProjectReference" or "FrameworkReference"),
            static element => Assert.Single(element.Attributes()));
    }

    private static IEnumerable<string?> Values(XDocument project, string element) => project.Descendants()
        .Where(item => item.Name.LocalName == element).Select(static item => item.Attribute("Include")?.Value).Order(StringComparer.Ordinal);

    private static XDocument ReadProject(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SiteFactory.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
