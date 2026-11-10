using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Hosting.Tests;

public sealed class DependencyBoundaryTests
{
    private const string ContractsReference = "../LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj";
    private const string RuntimeReference = "../LogicGate.SiteFactory.Runtime/LogicGate.SiteFactory.Runtime.csproj";
    private static readonly string[] ApprovedPackages = ["Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Hosting.Abstractions", "Microsoft.Extensions.Logging.Abstractions"];
    private static readonly string[] ForbiddenNamespaces = ["System.IO", "System.Net", "Npgsql", "Client",
        "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore",
        "LogicGate.SiteFactory.Runtime.Database", "LogicGate.SiteFactory.Infrastructure", "LogicGate.SiteFactory.Modules",
        "LogicGate.SiteFactory.ModuleSdk", "LogicGate.SiteFactory.Application", "LogicGate.SiteFactory.Cli"];

    [Fact]
    public void ProductionProjectRestoreAndAssemblyUseOnlyTheApprovedDependencies()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Runtime.Hosting");
        AssertHostingProject(ReadProject(Path.Combine(directory, "LogicGate.SiteFactory.Runtime.Hosting.csproj")));
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json")));
        JsonElement libraries = assets.RootElement.GetProperty("libraries");
        Assert.Equal(["LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts", "Microsoft.Extensions.Configuration.Abstractions",
                "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Diagnostics.Abstractions",
                "Microsoft.Extensions.FileProviders.Abstractions", "Microsoft.Extensions.Hosting.Abstractions",
                "Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.Options", "Microsoft.Extensions.Primitives"],
            libraries.EnumerateObject().Select(static library => library.Name.Split('/')[0]).OrderBy(static name => name, StringComparer.Ordinal));
        foreach (string package in libraries.EnumerateObject().Select(static library => library.Name.Split('/')[0])
            .Where(static name => name.StartsWith("Microsoft.", StringComparison.Ordinal)))
        {
            Assert.Equal("package", libraries.GetProperty(package + "/10.0.12").GetProperty("type").GetString());
        }
        foreach (string project in new[] { "LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts" })
            Assert.Equal("project", Assert.Single(libraries.EnumerateObject(),
                library => library.Name.StartsWith(project + "/", StringComparison.Ordinal)).Value.GetProperty("type").GetString());
        foreach (JsonProperty framework in assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            Assert.Equal(["Microsoft.NETCore.App"], framework.Value.GetProperty("frameworkReferences")
                .EnumerateObject().Select(static reference => reference.Name));
        }

        Assembly assembly = typeof(RuntimeConfigurationReader).Assembly;
        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            Assert.True(reference.Name is "LogicGate.SiteFactory.Runtime" or "LogicGate.SiteFactory.Runtime.Contracts"
                or "Microsoft.Extensions.Primitives" || ApprovedPackages.Contains(reference.Name, StringComparer.Ordinal)
                || reference.Name!.StartsWith("System.", StringComparison.Ordinal), reference.Name);
        }
        Assert.Empty(assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false));
        using FileStream stream = File.OpenRead(assembly.Location);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference reference = metadata.GetTypeReference(handle);
            string typeNamespace = metadata.GetString(reference.Namespace);
            Assert.DoesNotContain(ForbiddenNamespaces,
                prefix => typeNamespace == prefix || typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal));
            if (typeNamespace.StartsWith("Microsoft.", StringComparison.Ordinal))
            {
                Assert.True(typeNamespace is "Microsoft.Extensions.Configuration" or "Microsoft.Extensions.Primitives"
                    or "Microsoft.Extensions.DependencyInjection" or "Microsoft.Extensions.Hosting" or "Microsoft.Extensions.Logging"
                    or "Microsoft.Extensions.Logging.Abstractions", typeNamespace);
            }
            if (typeNamespace.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal))
            {
                Assert.True(typeNamespace is "LogicGate.SiteFactory.Runtime.Contracts" or "LogicGate.SiteFactory.Runtime" or "LogicGate.SiteFactory.Runtime.Hosting", typeNamespace);
            }
        }
    }

    [Theory]
    [InlineData("ProjectReference")]
    [InlineData("PackageReference")]
    [InlineData("FrameworkReference")]
    [InlineData("Reference")]
    [InlineData("InternalsVisibleTo")]
    [InlineData("Compile")]
    [InlineData("Import")]
    public void GuardRejectsAdditionalDependenciesAndLinkedSourcesInMemory(string element)
    {
        XDocument project = new(new XElement("Project", new XElement("ItemGroup",
            new[] { ContractsReference, RuntimeReference }.Select(static reference => new XElement("ProjectReference", new XAttribute("Include", reference))),
            ApprovedPackages.Select(static package => new XElement("PackageReference", new XAttribute("Include", package))))));
        AssertHostingProject(project);
        project.Root!.Add(new XElement("ItemGroup", new XElement(element, new XAttribute("Include", "Forbidden.Dependency"))));
        Assert.ThrowsAny<Exception>(() => AssertHostingProject(project));
    }

    [Fact]
    public void PublicSurfaceContainsExactlyTheEightApprovedTypesAndSignatures()
    {
        Type[] expected = [typeof(RuntimeConfigurationReader), typeof(RuntimeConfigurationException), typeof(RuntimeConfigurationStartupValidation),
            typeof(RuntimeStartupService), typeof(RuntimeStartupException), typeof(RuntimeStartupRegistration), typeof(IRuntimeStartupReadiness), typeof(IRuntimeReadinessRefresh)];
        Assert.Equal(expected.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(RuntimeConfigurationReader).Assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal));
        Assert.All(expected, static type => Assert.Equal("LogicGate.SiteFactory.Runtime.Hosting", type.Namespace));

        Assert.True(typeof(RuntimeConfigurationReader).IsAbstract && typeof(RuntimeConfigurationReader).IsSealed);
        Assert.Empty(typeof(RuntimeConfigurationReader).GetConstructors());
        MethodInfo read = Assert.Single(typeof(RuntimeConfigurationReader).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Read", read.Name);
        Assert.Equal(typeof(RuntimeConfiguration), read.ReturnType);
        Assert.Equal([typeof(IConfiguration), typeof(string), typeof(RuntimeComponent)],
            read.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Empty(typeof(RuntimeConfigurationReader).GetProperties());
        Assert.Empty(typeof(RuntimeConfigurationReader).GetFields());

        Assert.True(typeof(RuntimeConfigurationException).IsSealed);
        Assert.Equal(typeof(Exception), typeof(RuntimeConfigurationException).BaseType);
        ConstructorInfo exceptionConstructor = Assert.Single(typeof(RuntimeConfigurationException).GetConstructors());
        Assert.Equal(typeof(IReadOnlyList<StartupValidationResult>), Assert.Single(exceptionConstructor.GetParameters()).ParameterType);
        PropertyInfo diagnostics = Assert.Single(typeof(RuntimeConfigurationException).GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("Diagnostics", diagnostics.Name);
        Assert.Equal(typeof(IReadOnlyList<StartupValidationResult>), diagnostics.PropertyType);
        Assert.Null(diagnostics.SetMethod);
        Assert.Equal(["get_Diagnostics"], typeof(RuntimeConfigurationException).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(static method => method.Name));
        Assert.Empty(typeof(RuntimeConfigurationException).GetFields());

        Assert.True(typeof(RuntimeConfigurationStartupValidation).IsSealed);
        Assert.Equal([typeof(IStartupValidation)], typeof(RuntimeConfigurationStartupValidation).GetInterfaces());
        ConstructorInfo validationConstructor = Assert.Single(typeof(RuntimeConfigurationStartupValidation).GetConstructors());
        Assert.Equal([typeof(IConfiguration), typeof(string), typeof(RuntimeComponent)],
            validationConstructor.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Equal(["Configuration", "Id"], typeof(RuntimeConfigurationStartupValidation).GetProperties()
            .Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal(typeof(StartupValidationId), typeof(RuntimeConfigurationStartupValidation).GetProperty("Id")!.PropertyType);
        Assert.Null(typeof(RuntimeConfigurationStartupValidation).GetProperty("Id")!.SetMethod);
        PropertyInfo configuration = typeof(RuntimeConfigurationStartupValidation).GetProperty("Configuration")!;
        Assert.Equal(typeof(RuntimeConfiguration), configuration.PropertyType);
        Assert.True(configuration.GetMethod!.IsPublic);
        Assert.True(configuration.GetSetMethod(nonPublic: true)!.IsPrivate);
        MethodInfo validate = Assert.Single(typeof(RuntimeConfigurationStartupValidation).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), static method => !method.IsSpecialName);
        Assert.Equal("ValidateAsync", validate.Name);
        Assert.Equal(typeof(Task<IReadOnlyList<StartupValidationResult>>), validate.ReturnType);
        Assert.Equal(typeof(CancellationToken), Assert.Single(validate.GetParameters()).ParameterType);
        Assert.Equal(["ValidateAsync", "get_Configuration", "get_Id"], typeof(RuntimeConfigurationStartupValidation).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Empty(typeof(RuntimeConfigurationStartupValidation).GetFields());

        Type readiness = typeof(IRuntimeStartupReadiness);
        Assert.True(readiness.IsInterface);
        Assert.Empty(readiness.GetInterfaces());
        Assert.Equal(["ConfigurationValidated", "IsReady"], readiness.GetProperties().Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.All(readiness.GetProperties(), static property => { Assert.Equal(typeof(bool), property.PropertyType); Assert.Null(property.SetMethod); });
        Assert.Equal(["get_ConfigurationValidated", "get_IsReady"], readiness.GetMethods().Select(static method => method.Name).OrderBy(static name => name, StringComparer.Ordinal));

        Type refresh = typeof(IRuntimeReadinessRefresh);
        Assert.True(refresh.IsInterface);
        Assert.Empty(refresh.GetInterfaces());
        Assert.Empty(refresh.GetProperties());
        Assert.Empty(refresh.GetFields());
        MethodInfo refreshMethod = Assert.Single(refresh.GetMethods());
        Assert.Equal("RefreshAsync", refreshMethod.Name);
        Assert.Equal(typeof(Task<bool>), refreshMethod.ReturnType);
        ParameterInfo refreshToken = Assert.Single(refreshMethod.GetParameters());
        Assert.Equal(typeof(CancellationToken), refreshToken.ParameterType);
        Assert.False(refreshToken.HasDefaultValue);

        Type service = typeof(RuntimeStartupService);
        Assert.True(service.IsSealed);
        Assert.Equal(new[] { typeof(IHostedLifecycleService), typeof(IHostedService) }.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            service.GetInterfaces().OrderBy(static type => type.FullName, StringComparer.Ordinal));
        ParameterInfo[] constructor = Assert.Single(service.GetConstructors()).GetParameters();
        Assert.Equal([typeof(IRuntimeStartupValidator), typeof(ILogger<RuntimeStartupService>), typeof(IHostApplicationLifetime), typeof(IRuntimeStartupReadiness)],
            constructor.Select(static parameter => parameter.ParameterType));
        Assert.True(constructor[^1].HasDefaultValue);
        Assert.Null(constructor[^1].DefaultValue);
        Assert.Equal(["ConfigurationIsValidated", "IsReady", "Report"], service.GetProperties().Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal(typeof(RuntimeStartupReport), service.GetProperty("Report")!.PropertyType);
        Assert.True(service.GetProperty("Report")!.GetSetMethod(nonPublic: true)!.IsPrivate);
        foreach (string property in new[] { "ConfigurationIsValidated", "IsReady" })
        {
            Assert.Equal(typeof(bool), service.GetProperty(property)!.PropertyType);
            Assert.Null(service.GetProperty(property)!.SetMethod);
        }
        Assert.Equal(["StartAsync", "StartedAsync", "StartingAsync", "StopAsync", "StoppedAsync", "StoppingAsync",
                "get_ConfigurationIsValidated", "get_IsReady", "get_Report"],
            service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(static method => method.Name).OrderBy(static name => name, StringComparer.Ordinal));
        foreach (MethodInfo method in service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(static method => !method.IsSpecialName))
        {
            Assert.Equal(typeof(Task), method.ReturnType);
            Assert.Equal(typeof(CancellationToken), Assert.Single(method.GetParameters()).ParameterType);
        }
        Assert.Empty(service.GetFields());

        Type startupException = typeof(RuntimeStartupException);
        Assert.True(startupException.IsSealed);
        Assert.Equal(typeof(Exception), startupException.BaseType);
        Assert.Equal(typeof(RuntimeStartupReport), Assert.Single(Assert.Single(startupException.GetConstructors()).GetParameters()).ParameterType);
        PropertyInfo report = Assert.Single(startupException.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("Report", report.Name);
        Assert.Equal(typeof(RuntimeStartupReport), report.PropertyType);
        Assert.Null(report.SetMethod);
        Assert.Equal(["ToString", "get_Report"], startupException.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal(typeof(string), startupException.GetMethod("ToString")!.ReturnType);
        Assert.Empty(startupException.GetMethod("ToString")!.GetParameters());
        Assert.Empty(startupException.GetFields());

        Type registration = typeof(RuntimeStartupRegistration);
        Assert.True(registration.IsAbstract && registration.IsSealed);
        Assert.Empty(registration.GetConstructors());
        MethodInfo register = Assert.Single(registration.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("AddRuntimeStartupValidation", register.Name);
        Assert.Equal(typeof(IServiceCollection), register.ReturnType);
        Assert.Equal([typeof(IServiceCollection), typeof(IConfiguration), typeof(string), typeof(RuntimeComponent)], register.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.True(register.IsDefined(typeof(ExtensionAttribute), inherit: false));
        Assert.Empty(registration.GetFields());
        Assert.Empty(registration.GetProperties());
    }

    [Fact]
    public void BindingTestsUseOnlyTheApprovedReferencesWithoutLinkedSources()
    {
        XDocument project = ReadProject(Path.Combine(FindRepositoryRoot(), "tests", "LogicGate.SiteFactory.Runtime.Hosting.Tests",
            "LogicGate.SiteFactory.Runtime.Hosting.Tests.csproj"));
        Assert.Equal(["../../src/LogicGate.SiteFactory.Runtime.Hosting/LogicGate.SiteFactory.Runtime.Hosting.csproj",
                "../../src/LogicGate.SiteFactory.Runtime/LogicGate.SiteFactory.Runtime.csproj"],
            project.Descendants().Where(static element => element.Name.LocalName == "ProjectReference")
                .Select(static element => element.Attribute("Include")?.Value).OrderBy(static path => path, StringComparer.Ordinal));
        Assert.Equal(["Microsoft.Extensions.Configuration", "Microsoft.Extensions.Hosting", "Microsoft.NET.Test.Sdk", "xunit.runner.visualstudio", "xunit.v3"],
            project.Descendants().Where(static element => element.Name.LocalName == "PackageReference")
                .Select(static element => element.Attribute("Include")?.Value).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(project.Descendants(), static element =>
            element.Name.LocalName is "Compile" or "FrameworkReference" or "Reference" or "InternalsVisibleTo" or "Import");
        Assert.Equal(["LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.Contracts", "LogicGate.SiteFactory.Runtime.Hosting"],
            typeof(DependencyBoundaryTests).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name)
                .Where(static name => name!.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal))
                .OrderBy(static name => name, StringComparer.Ordinal));
    }

    private static void AssertHostingProject(XDocument project)
    {
        XElement[] references = project.Descendants().Where(static element => element.Name.LocalName == "ProjectReference").ToArray();
        Assert.Equal([ContractsReference, RuntimeReference], references.Select(static element => element.Attribute("Include")?.Value).OrderBy(static path => path, StringComparer.Ordinal));
        Assert.All(references, static reference => Assert.Single(reference.Attributes()));
        XElement[] packages = project.Descendants().Where(static element => element.Name.LocalName == "PackageReference").ToArray();
        Assert.Equal(ApprovedPackages, packages.Select(static element => element.Attribute("Include")?.Value).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.All(packages, static package => Assert.Single(package.Attributes()));
        Assert.DoesNotContain(project.Descendants(), static element =>
            element.Name.LocalName is "FrameworkReference" or "Reference" or "InternalsVisibleTo" or "Compile" or "Import");
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
            if (File.Exists(Path.Combine(directory.FullName, "SiteFactory.slnx"))) return directory.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
