using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using LogicGate.SiteFactory.Runtime.Contracts;
using Xunit;

// Shares the source-linked supervisor's namespace, not a reference to the CLI test assembly.
namespace LogicGate.SiteFactory.Cli.Tests;

public sealed class DependencyBoundaryTests
{
    private static readonly string[] ForbiddenNamespaces = ["System.IO", "System.Net", "Microsoft", "Npgsql", "Client", "LogicGate.SiteFactory.Infrastructure"];

    [Fact]
    public void ContractsHaveOnlyBclDependenciesIncludingEffectiveRestoreAndMetadata()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Runtime.Contracts");
        AssertPureProject(ReadProject(Path.Combine(directory, "LogicGate.SiteFactory.Runtime.Contracts.csproj")));
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json")));
        Assert.Empty(assets.RootElement.GetProperty("libraries").EnumerateObject());
        foreach (JsonProperty framework in assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            Assert.Equal(["Microsoft.NETCore.App"], framework.Value.GetProperty("frameworkReferences")
                .EnumerateObject().Select(static reference => reference.Name));
        }

        Assert.All(typeof(RuntimeIdentity).Assembly.GetReferencedAssemblies(), static reference =>
            Assert.StartsWith("System.", reference.Name, StringComparison.Ordinal));
        Assert.Empty(typeof(RuntimeIdentity).Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false));
        using FileStream stream = File.OpenRead(typeof(RuntimeIdentity).Assembly.Location);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            string name = metadata.GetString(metadata.GetTypeReference(handle).Namespace);
            Assert.DoesNotContain(ForbiddenNamespaces,
                prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("ProjectReference")]
    [InlineData("PackageReference")]
    [InlineData("FrameworkReference")]
    [InlineData("Reference")]
    [InlineData("InternalsVisibleTo")]
    public void BoundaryGuardRejectsForbiddenReferencesInMemory(string element)
    {
        XDocument project = new(new XElement("Project", new XElement("ItemGroup",
            new XElement(element, new XAttribute("Include", "Forbidden.Dependency")))));
        Assert.ThrowsAny<Exception>(() => AssertPureProject(project));
    }

    [Fact]
    public void PublicSurfaceIsExactlyThePureContractsAndTestsReferenceOnlyThatAssembly()
    {
        Type[] expected = [typeof(RuntimeEnvironment), typeof(RuntimeComponent), typeof(RuntimeIdentity),
            typeof(RuntimeConfiguration), typeof(StartupValidationId), typeof(StartupValidationSeverity),
            typeof(StartupValidationResult), typeof(RuntimeStartupReport), typeof(IStartupValidation), typeof(IRuntimeStartupValidator)];
        Assert.Equal(expected.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(RuntimeIdentity).Assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal));
        Assert.All(expected, static type => Assert.Equal("LogicGate.SiteFactory.Runtime.Contracts", type.Namespace));
        Assert.Equal(["LogicGate.SiteFactory.Runtime.Contracts"], typeof(DependencyBoundaryTests).Assembly.GetReferencedAssemblies()
            .Select(static reference => reference.Name).Where(static name => name!.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal)));
        XDocument project = ReadProject(Path.Combine(FindRepositoryRoot(), "tests", "LogicGate.SiteFactory.Runtime.Contracts.Tests",
            "LogicGate.SiteFactory.Runtime.Contracts.Tests.csproj"));
        XElement reference = Assert.Single(project.Descendants(), static element => element.Name.LocalName == "ProjectReference");
        Assert.Equal("../../src/LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj", reference.Attribute("Include")?.Value);
    }

    [Fact]
    public async Task ExternalProjectBuildsAndRunsWithOnlyThePublicContractsDll()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string repository = FindRepositoryRoot();
        string directory = Directory.CreateTempSubdirectory("sitefactory-runtime-consumer-").FullName;
        try
        {
            Assert.False(directory.StartsWith(repository + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            File.Copy(typeof(RuntimeIdentity).Assembly.Location, Path.Combine(directory, "LogicGate.SiteFactory.Runtime.Contracts.dll"));
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), await File.ReadAllTextAsync(Path.Combine(repository, "global.json"), token), token);
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"),
                "<configuration><packageSources><clear /></packageSources></configuration>", token);
            const string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <UseSharedCompilation>false</UseSharedCompilation>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include="LogicGate.SiteFactory.Runtime.Contracts">
                      <HintPath>LogicGate.SiteFactory.Runtime.Contracts.dll</HintPath>
                    </Reference>
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(Path.Combine(directory, "Consumer.csproj"), project, token);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), ConsumerSource, token);
            CliBytesResult build = await RunDotnetAsync(directory,
                ["build", "Consumer.csproj", "--configuration", "Release", "--disable-build-servers", "--nologo"], TimeSpan.FromSeconds(90), token);
            Assert.True(build.ExitCode == 0, Encoding.UTF8.GetString(build.StandardOutput) + Encoding.UTF8.GetString(build.StandardError));
            using JsonDocument assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "obj", "project.assets.json"), token));
            Assert.Empty(assets.RootElement.GetProperty("libraries").EnumerateObject());
            XDocument consumer = ReadProject(Path.Combine(directory, "Consumer.csproj"));
            Assert.DoesNotContain(consumer.Descendants(), static element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "InternalsVisibleTo");
            Assert.Single(consumer.Descendants(), static element => element.Name.LocalName == "Reference");
            CliBytesResult execution = await RunDotnetAsync(directory,
                [Path.Combine("bin", "Release", "net10.0", "Consumer.dll")], TimeSpan.FromSeconds(20), token);
            Assert.True(execution.ExitCode == 0, Encoding.UTF8.GetString(execution.StandardOutput) + Encoding.UTF8.GetString(execution.StandardError));
            Assert.Empty(execution.StandardError);
            Assert.Equal("contracts-consumer:PASS", Encoding.UTF8.GetString(execution.StandardOutput).Trim());
        }
        finally
        {
            // Only this invocation's randomly allocated directory, after the owned process scope is cleaned.
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, directory, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-runtime-consumer-", Path.GetFileName(directory), StringComparison.Ordinal);
            Assert.False((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(directory, recursive: true);
            Assert.False(Directory.Exists(directory));
        }
    }

    private static Task<CliBytesResult> RunDotnetAsync(string directory, string[] arguments, TimeSpan timeout, CancellationToken token)
    {
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";
        start.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        return OwnedProcessRunner.RunAsync(start, timeout, token);
    }

    private static void AssertPureProject(XDocument project) => Assert.DoesNotContain(project.Descendants(), static element =>
        element.Name.LocalName is "ProjectReference" or "PackageReference" or "FrameworkReference" or "Reference" or "InternalsVisibleTo");

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

    private const string ConsumerSource = """
        using LogicGate.SiteFactory.Runtime.Contracts;

        if (!RuntimeIdentity.TryCreate("external-client", "Production", RuntimeComponent.Worker, out RuntimeIdentity? identity, out var errors) || errors.Count != 0)
            return 1;
        if (!RuntimeConfiguration.TryCreate(identity, "https://example.test", out RuntimeConfiguration? configuration, out _) || !ReferenceEquals(identity, configuration.Identity))
            return 2;
        using CancellationTokenSource caller = new();
        IStartupValidation check = new Check();
        var results = await check.ValidateAsync(caller.Token);
        RuntimeStartupReport report = new(results);
        IRuntimeStartupValidator validator = new Validator(report);
        if (!(await validator.ValidateAsync(caller.Token)).IsReady || report.SchemaVersion != 1 || identity.Environment != RuntimeEnvironment.Production)
            return 3;
        await caller.CancelAsync();
        try { await check.ValidateAsync(caller.Token); return 4; }
        catch (OperationCanceledException error) when (error.CancellationToken == caller.Token) { }
        Console.WriteLine("contracts-consumer:PASS");
        return 0;

        sealed class Check : IStartupValidation
        {
            public StartupValidationId Id { get; } = new("startup.external");
            public Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<StartupValidationResult>>([new(Id, StartupValidationSeverity.Success, null, "Valid.")]);
            }
        }

        sealed class Validator(RuntimeStartupReport report) : IRuntimeStartupValidator
        {
            public Task<RuntimeStartupReport> ValidateAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(report);
            }
        }
        """;
}

// The existing supervisor's result contract; no CLI runner, production type or PostgreSQL metrics imported.
internal sealed record CliBytesResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);

internal static class RuntimeContractsTestEntryPoint
{
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == OwnedProcessRunner.SupervisorMode)
        {
            return OwnedProcessRunner.SuperviseAsync(args[1]).GetAwaiter().GetResult();
        }

        return global::LogicGate.SiteFactory.Runtime.Contracts.Tests.AutoGenerated.XunitAutoGeneratedEntryPoint.Main(args);
    }
}
