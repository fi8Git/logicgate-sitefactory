using System.Globalization;
using System.Reflection;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

public sealed class SiteFactoryCliTests
{
    [Fact]
    public async Task NoArgumentsWritesHelpAndReturnsSuccess()
    {
        using StringWriter standardOutput = CreateWriter();
        using StringWriter standardError = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(
            [],
            CreatePublicComposition(),
            standardOutput,
            standardError,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: sitefactory", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("--help", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("--version", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Fact]
    public async Task HelpOptionWritesHelpAndReturnsSuccess()
    {
        using StringWriter standardOutput = CreateWriter();
        using StringWriter standardError = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(
            ["--help"],
            CreatePublicComposition(),
            standardOutput,
            standardError,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: sitefactory", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Fact]
    public async Task VersionOptionWritesAssemblyVersionAndReturnsSuccess()
    {
        using StringWriter standardOutput = CreateWriter();
        using StringWriter standardError = CreateWriter();
        Assembly cliAssembly = typeof(SiteFactoryCli).Assembly;
        AssemblyInformationalVersionAttribute versionAttribute =
            Assert.IsType<AssemblyInformationalVersionAttribute>(
                cliAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>());

        int exitCode = await SiteFactoryCli.RunAsync(
            ["--version"],
            CreatePublicComposition(),
            standardOutput,
            standardError,
            TestContext.Current.CancellationToken);

        string actualVersion = standardOutput.ToString().Trim();
        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(actualVersion));
        Assert.Equal(versionAttribute.InformationalVersion, actualVersion);
        Assert.Equal(string.Empty, standardError.ToString());
    }

    [Fact]
    public async Task UnknownArgumentWritesErrorAndReturnsUsageError()
    {
        using StringWriter standardOutput = CreateWriter();
        using StringWriter standardError = CreateWriter();

        int exitCode = await SiteFactoryCli.RunAsync(
            ["--unknown"],
            CreatePublicComposition(),
            standardOutput,
            standardError,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Contains("argument inconnu", standardError.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CliAssemblyLoadsAndContainsExpectedMarker()
    {
        Type markerType = typeof(Cli.AssemblyMarker);

        Assert.Equal("LogicGate.SiteFactory.Cli", markerType.Assembly.GetName().Name);
        Assert.Equal(
            markerType,
            markerType.Assembly.GetType("LogicGate.SiteFactory.Cli.AssemblyMarker"));
    }

    private static GenerationComposition CreatePublicComposition()
    {
        DocumentationModule module = new(new TemplateManifestJsonReader());
        return new(new ModuleCatalog([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]),
            [module.CreateContribution()]);
    }

    private static StringWriter CreateWriter()
    {
        return new StringWriter(CultureInfo.InvariantCulture);
    }
}
