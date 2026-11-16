using System.Reflection;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace LogicGate.SiteFactory.ModuleSdk.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void SdkHasNoProjectPackageOrAdditionalFrameworkDependency()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.ModuleSdk");
        XDocument project = ReadProject(Path.Combine(directory, "LogicGate.SiteFactory.ModuleSdk.csproj"));
        Assert.DoesNotContain(project.Descendants(), static element =>
            element.Name.LocalName is "ProjectReference" or "PackageReference" or "FrameworkReference" or "Reference" or "InternalsVisibleTo");

        // Also inspect effective restore output, including centrally inherited references.
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json")));
        Assert.Empty(assets.RootElement.GetProperty("libraries").EnumerateObject());
        foreach (JsonProperty framework in assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            Assert.Equal(["Microsoft.NETCore.App"], framework.Value.GetProperty("frameworkReferences")
                .EnumerateObject().Select(static reference => reference.Name));
        }

        Assert.All(typeof(ISiteFactoryModule).Assembly.GetReferencedAssemblies(), static reference =>
            Assert.StartsWith("System.", reference.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void ConsumerReferencesOnlyTheSdkFromSiteFactory()
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "LogicGate.SiteFactory.ModuleSdk.Tests", "LogicGate.SiteFactory.ModuleSdk.Tests.csproj");
        XDocument project = ReadProject(path);
        XElement reference = Assert.Single(project.Descendants(), static element => element.Name.LocalName == "ProjectReference");
        Assert.Equal("../../src/LogicGate.SiteFactory.ModuleSdk/LogicGate.SiteFactory.ModuleSdk.csproj", reference.Attribute("Include")?.Value);
        Assert.Equal(["LogicGate.SiteFactory.ModuleSdk"], typeof(DependencyBoundaryTests).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name).Where(static name => name!.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal)));
    }

    [Fact]
    public void PublicSurfaceIsLimitedToApprovedContractsAndOneDescriptorProperty()
    {
        Type[] expected = [typeof(ISiteFactoryModule), typeof(ModuleDescriptor), typeof(ModuleId), typeof(ModuleVersion), typeof(ModuleCapability),
            typeof(ISiteFactoryTemplateProvider), typeof(TemplateIdentity), typeof(TemplateDefinition), typeof(TemplateFileDefinition),
            typeof(TemplateFilePolicy), typeof(TemplateRenderContext), typeof(TemplateCatalogResult), typeof(TemplateContentResult), typeof(TemplateReadFailure)];
        Assert.Equal(expected.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(ISiteFactoryModule).Assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal));

        PropertyInfo descriptor = Assert.Single(typeof(ISiteFactoryModule).GetProperties());
        Assert.Equal(typeof(ModuleDescriptor), descriptor.PropertyType);
        Assert.Null(descriptor.SetMethod);
        Assert.Equal(descriptor.GetMethod, Assert.Single(typeof(ISiteFactoryModule).GetMethods()));
        foreach (Type type in expected.Where(static type => type != typeof(ISiteFactoryModule)))
        {
            Assert.All(type.GetProperties(), static property => Assert.Null(property.SetMethod));
        }
    }

    [Fact]
    public void TemplateProviderIsIndependentAndExposesOnlyTheTwoApprovedOperations()
    {
        Type provider = typeof(ISiteFactoryTemplateProvider);
        Assert.Empty(provider.GetInterfaces());
        Assert.Empty(provider.GetProperties());
        MethodInfo[] methods = provider.GetMethods().OrderBy(static method => method.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(["ReadCatalogAsync", "ReadContentAsync"], methods.Select(static method => method.Name));
        Assert.Equal(typeof(Task<TemplateCatalogResult>), methods[0].ReturnType);
        Assert.Equal(typeof(Task<TemplateContentResult>), methods[1].ReturnType);
        Assert.Equal([typeof(CancellationToken)], methods[0].GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Equal([typeof(TemplateIdentity), typeof(string), typeof(TemplateRenderContext), typeof(CancellationToken)],
            methods[1].GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.All(methods, static method => Assert.True(method.GetParameters()[^1].HasDefaultValue));
        Assert.Equal(["Files", "Identity", "SupportedProductIds"], typeof(TemplateDefinition).GetProperties().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["DefaultLocale", "DisplayName"], typeof(TemplateRenderContext).GetProperties().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.NotNull(typeof(TemplateRenderContext).GetConstructor([typeof(string), typeof(string)]));
        Assert.Equal([TemplateFilePolicy.Generated, TemplateFilePolicy.CreateOnce], Enum.GetValues<TemplateFilePolicy>());
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
