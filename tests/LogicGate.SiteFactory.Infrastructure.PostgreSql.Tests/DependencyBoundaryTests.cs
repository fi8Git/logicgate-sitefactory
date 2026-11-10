using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence.Migrations;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class DependencyBoundaryTests
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!).ToDictionary(static code => code.Value);
    private static readonly string[] RuntimeProjectLibraries = ["LogicGate.SiteFactory.Runtime.Contracts/0.1.0-dev", "LogicGate.SiteFactory.Runtime/0.1.0-dev", "LogicGate.SiteFactory.Runtime.Hosting/0.1.0-dev"];

    [Fact]
    public void ConfigurationTypesAndTheirMethodBodiesRemainIndependentOfPersistenceAndHosts()
    {
        // G03 shares this assembly, not the G02 types. Inspect signatures AND IL, including async state machines.
        Type[] configuration = typeof(PostgreSqlRuntimeConfiguration).Assembly.GetTypes()
            .Where(static type => type.Namespace == "LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration" || IsCompilerData(type)).ToArray();
        Assert.NotEmpty(configuration);
        Assert.All(configuration, AssertConfigurationType);
    }

    [Theory]
    [InlineData(typeof(ForbiddenConfigurationSignature))]
    [InlineData(typeof(ForbiddenConfigurationBody))]
    public void ConfigurationGuardRejectsPersistenceInSignaturesAndMethodBodies(Type type)
    {
        Assert.ThrowsAny<Exception>(() => AssertConfigurationType(type));
    }

    [Fact]
    public void PostgreSqlInfrastructureReferencesOnlyItsApprovedDependencies()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Infrastructure.PostgreSql");
        XDocument project = ReadProject(Path.Combine(directory, "LogicGate.SiteFactory.Infrastructure.PostgreSql.csproj"));
        AssertApprovedProject(project);
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "obj", "project.assets.json")));
        string[] packages = ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Abstractions",
            "Microsoft.EntityFrameworkCore.Analyzers", "Microsoft.EntityFrameworkCore.Relational",
            "Microsoft.Extensions.Caching.Abstractions", "Microsoft.Extensions.Caching.Memory",
            "Microsoft.Extensions.Configuration.Abstractions", "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Diagnostics.Abstractions", "Microsoft.Extensions.FileProviders.Abstractions", "Microsoft.Extensions.Hosting.Abstractions",
            "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.Options", "Microsoft.Extensions.Primitives",
            "Npgsql", "Npgsql.EntityFrameworkCore.PostgreSQL"];
        string[] closure = packages.Select(static name => name + (name.StartsWith("Npgsql", StringComparison.Ordinal) ? "/10.0.3" : "/10.0.12"))
            .Concat(RuntimeProjectLibraries)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(closure, assets.RootElement.GetProperty("libraries").EnumerateObject().Select(static library => library.Name).Order(StringComparer.Ordinal));
        Assert.All(typeof(PostgreSqlRuntimeConfiguration).Assembly.GetReferencedAssemblies(), static assembly =>
            Assert.True(assembly.Name!.StartsWith("System.", StringComparison.Ordinal)
                || assembly.Name is "Npgsql" or "LogicGate.SiteFactory.Runtime.Contracts" or "LogicGate.SiteFactory.Runtime" or "LogicGate.SiteFactory.Runtime.Hosting"
                    or "Microsoft.Extensions.Configuration.Abstractions" or "Microsoft.Extensions.DependencyInjection.Abstractions"
                    or "Microsoft.EntityFrameworkCore" or "Microsoft.EntityFrameworkCore.Relational"
                    or "Microsoft.EntityFrameworkCore.Abstractions" or "Npgsql.EntityFrameworkCore.PostgreSQL"));
        Assert.Empty(typeof(PostgreSqlRuntimeConfiguration).Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false));
        using FileStream stream = File.OpenRead(typeof(PostgreSqlRuntimeConfiguration).Assembly.Location);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            string name = metadata.GetString(metadata.GetTypeReference(handle).Namespace);
            Assert.False(name.StartsWith("Client", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || (name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
                    && !name.StartsWith("Microsoft.Extensions.Configuration", StringComparison.Ordinal)
                    && !name.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
                || name.StartsWith("LogicGate.SiteFactory.ModuleSdk", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("PackageReference", "Microsoft.EntityFrameworkCore")]
    [InlineData("ProjectReference", "../Unapproved.Provider/Unapproved.Provider.csproj")]
    [InlineData("FrameworkReference", "Microsoft.AspNetCore.App")]
    [InlineData("Reference", "Hidden.Provider")]
    public void GuardRejectsAdditionalDependenciesInMemory(string element, string value)
    {
        XDocument project = new(new XElement("Project", new XElement("ItemGroup",
            new XElement("PackageReference", new XAttribute("Include", "Microsoft.Extensions.Configuration.Abstractions")),
            new XElement("PackageReference", new XAttribute("Include", "Microsoft.Extensions.DependencyInjection.Abstractions")),
            new XElement("PackageReference", new XAttribute("Include", "Npgsql")),
            new XElement("PackageReference", new XAttribute("Include", "Microsoft.EntityFrameworkCore.Relational")),
            new XElement("PackageReference", new XAttribute("Include", "Npgsql.EntityFrameworkCore.PostgreSQL")),
            new XElement("ProjectReference", new XAttribute("Include", "../LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj")),
            new XElement("ProjectReference", new XAttribute("Include", "../LogicGate.SiteFactory.Runtime/LogicGate.SiteFactory.Runtime.csproj")),
            new XElement("ProjectReference", new XAttribute("Include", "../LogicGate.SiteFactory.Runtime.Hosting/LogicGate.SiteFactory.Runtime.Hosting.csproj")),
            new XElement(element, new XAttribute("Include", value)))));
        XDocument valid = new(project);
        valid.Descendants().Last().Remove();
        AssertApprovedProject(valid);
        Assert.ThrowsAny<Exception>(() => AssertApprovedProject(project));
    }

    [Fact]
    public void PublicFactoryIsConfinedToPersistenceAndConfigurationHasNoSecretAccessor()
    {
        Type[] expected = [typeof(PostgreSqlComponent), typeof(PostgreSqlConfigurationReader),
            typeof(PostgreSqlRuntimeConfiguration), typeof(PostgreSqlConfigurationException)];
        Assert.Equal(expected.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(PostgreSqlRuntimeConfiguration).Assembly.GetExportedTypes()
                .Where(static type => type.Namespace == "LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration")
                .OrderBy(static type => type.FullName, StringComparer.Ordinal));
        Type[] persistence = [typeof(RuntimeDbContext), typeof(RuntimeDbContextFactory), typeof(DatabaseIdentity), typeof(SchemaState), typeof(CreateRuntimeMetadata)];
        Type[] g05 = [typeof(PostgreSqlDatabaseReader), typeof(PostgreSqlStartupValidation), typeof(PostgreSqlStartupRegistration)];
        Type[] g06 = [typeof(WebDatabaseReadiness), typeof(WebDatabaseStartupRegistration),
            typeof(WorkerDatabaseStartupValidator), typeof(WorkerDatabaseStartupRegistration)];
        Assert.Equal(expected.Concat(persistence).Concat(g05).Concat(g06).OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(RuntimeDbContext).Assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal));
        Assert.All(expected, static type => Assert.Equal("LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration", type.Namespace));
        Assert.Equal(["Web", "Worker", "Migrator"], Enum.GetNames<PostgreSqlComponent>());
        Assert.Equal(["Web", "Worker"], Enum.GetNames<RuntimeComponent>());
        Assert.Equal(["Component"], typeof(PostgreSqlRuntimeConfiguration).GetProperties().Select(static property => property.Name));
        Assert.Null(typeof(PostgreSqlRuntimeConfiguration).GetMethod("CreateDbContext"));
        Assert.Null(typeof(PostgreSqlRuntimeConfiguration).GetMethod("CreateProbeConnection"));
        Assert.Null(typeof(PostgreSqlRuntimeConfiguration).GetMethod("CreateConnection"));
        Assert.Empty(typeof(PostgreSqlRuntimeConfiguration).GetConstructors());
        Assert.Equal("LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence", typeof(RuntimeDbContextFactory).Namespace);
        MethodInfo create = Assert.Single(typeof(RuntimeDbContextFactory).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", create.Name);
        Assert.Equal(typeof(RuntimeDbContext), create.ReturnType);
        Assert.Equal(typeof(PostgreSqlRuntimeConfiguration), Assert.Single(create.GetParameters()).ParameterType);
        Assert.Empty(typeof(RuntimeDbContextFactory).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Fact]
    public void TestsAreAutonomousAndContractsStillHaveAnEmptyEffectiveRestoreClosure()
    {
        string root = FindRepositoryRoot();
        XDocument project = ReadProject(Path.Combine(root, "tests", "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests",
            "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.csproj"));
        Assert.Equal(["../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/LogicGate.SiteFactory.Infrastructure.PostgreSql.csproj",
            "../../src/LogicGate.SiteFactory.Migrator/LogicGate.SiteFactory.Migrator.csproj"],
            project.Descendants("ProjectReference").Select(static element => element.Attribute("Include")?.Value));
        Assert.Equal(["../LogicGate.SiteFactory.Infrastructure.Tests/Generation/DirectoryLinkFixture.cs",
            "../LogicGate.SiteFactory.Cli.Tests/OwnedProcessRunner.cs", "../LogicGate.SiteFactory.Cli.Tests/OwnedProcessScope.cs"],
            project.Descendants("Compile").Select(static element => element.Attribute("Include")?.Value));
        Assert.Equal("LogicGate.SiteFactory.Cli.Tests.G04TestEntryPoint", Assert.Single(project.Descendants("StartupObject")).Value);
        Assert.Equal(["Microsoft.Extensions.Configuration", "Microsoft.NET.Test.Sdk", "xunit.runner.visualstudio", "xunit.v3"],
            project.Descendants("PackageReference").Select(static element => element.Attribute("Include")?.Value));
        AssertDiagnosticsScriptCopy(project);
        foreach (string path in new[] { "PostgreSql/*.ps1", "../LogicGate.SiteFactory.Cli.Tests/PostgreSql/Private.ps1" })
        {
            XDocument invalid = new(project);
            Assert.Single(invalid.Descendants("None"), static rule => rule.Attribute("Update") is not null).SetAttributeValue("Update", path);
            Assert.ThrowsAny<Exception>(() => AssertDiagnosticsScriptCopy(invalid));
        }
        XDocument extra = new(project);
        extra.Root!.Add(new XElement("ItemGroup", new XElement("None", new XAttribute("Include", "Other.ps1"),
            new XAttribute("CopyToOutputDirectory", "PreserveNewest"))));
        Assert.ThrowsAny<Exception>(() => AssertDiagnosticsScriptCopy(extra));
        string script = Path.Combine(AppContext.BaseDirectory, "PostgreSql", "PostgreSqlHarnessDiagnostics.ps1");
        string publicDirectory = Path.Combine(AppContext.BaseDirectory, "PostgreSql", "Public");
        Assert.Equal(new[] { script, Path.Combine(publicDirectory, "Invoke-PostgreSqlTests.ps1"), Path.Combine(publicDirectory, "PostgreSqlProof.ps1") }
            .Order(StringComparer.Ordinal), Directory.GetFiles(AppContext.BaseDirectory, "*.ps1", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(["Invoke-PostgreSqlTests.ps1", "PostgreSqlProof.ps1", "shards.json"],
            Directory.GetFiles(publicDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (string file in new[] { "Invoke-PostgreSqlTests.ps1", "PostgreSqlProof.ps1", "shards.json" })
            Assert.Equal(File.ReadAllText(Path.Combine(root, "tests", "PostgreSql", "Public", file)), File.ReadAllText(Path.Combine(publicDirectory, file)));
        Assert.Equal(File.ReadAllText(Path.Combine(root, "tests", "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests",
            "PostgreSql", "PostgreSqlHarnessDiagnostics.ps1")), File.ReadAllText(script));
        AssertTypeAndNested(typeof(PostgreSqlHarnessDiagnosticsTests), AssertP01DiagnosticsDependency);
        Assert.Empty(typeof(PostgreSqlHarnessDiagnosticsTests).GetCustomAttributes<TraitAttribute>());
        MethodInfo[] diagnostics = typeof(PostgreSqlHarnessDiagnosticsTests).GetMethods()
            .Where(static method => method.GetCustomAttribute<FactAttribute>() is not null).ToArray();
        Assert.Equal(5, diagnostics.Length);
        Assert.All(diagnostics, static method =>
        {
            FactAttribute fact = method.GetCustomAttribute<FactAttribute>()!;
            Assert.Empty(method.GetCustomAttributes<TraitAttribute>());
            Assert.Null(fact.Skip);
            Assert.Null(fact.SkipUnless);
            Assert.Null(fact.SkipWhen);
        });
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "LogicGate.SiteFactory.Runtime.Contracts", "obj", "project.assets.json")));
        Assert.Empty(assets.RootElement.GetProperty("libraries").EnumerateObject());
    }

    private static void AssertDiagnosticsScriptCopy(XDocument project)
    {
        Assert.Equal(4, project.Descendants("None").Count());
        XElement rule = Assert.Single(project.Descendants("None"), static element => element.Attribute("Update") is not null);
        Assert.Equal(["CopyToOutputDirectory=PreserveNewest", "Update=PostgreSql/PostgreSqlHarnessDiagnostics.ps1"],
            rule.Attributes().Select(static attribute => attribute.Name.LocalName + "=" + attribute.Value).Order(StringComparer.Ordinal));
        Assert.Empty(rule.Elements());
        Assert.Null(rule.Parent!.Attribute("Condition"));
        XElement[] resources = project.Descendants("None").Where(static element => element.Attribute("Include") is not null).ToArray();
        Assert.Equal(["../PostgreSql/Public/Invoke-PostgreSqlTests.ps1", "../PostgreSql/Public/PostgreSqlProof.ps1", "../PostgreSql/Public/shards.json"],
            resources.Select(static element => element.Attribute("Include")!.Value).Order(StringComparer.Ordinal));
        Assert.All(resources, static resource =>
        {
            string file = Path.GetFileName(resource.Attribute("Include")!.Value);
            Assert.Equal(new[] { "CopyToOutputDirectory=PreserveNewest", "Include=../PostgreSql/Public/" + file, "Link=PostgreSql/Public/" + file },
                resource.Attributes().Select(static attribute => attribute.Name.LocalName + "=" + attribute.Value).Order(StringComparer.Ordinal));
            Assert.Empty(resource.Elements());
            Assert.Null(resource.Parent!.Attribute("Condition"));
        });
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is "Content" or "EmbeddedResource"
            || (element.Attribute("CopyToOutputDirectory") is not null && element.Name.LocalName != "None"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wildcard")]
    [InlineData("private")]
    [InlineData("conditional")]
    [InlineData("destination")]
    public void P04ResourceGuardRejectsEveryUnapprovedCopyRule(string scenario)
    {
        XDocument project = ReadProject(Path.Combine(FindRepositoryRoot(), "tests", "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests",
            "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.csproj"));
        XElement resource = project.Descendants("None").First(static rule => rule.Attribute("Include") is not null);
        switch (scenario)
        {
            case "missing": resource.Remove(); break;
            case "wildcard": resource.SetAttributeValue("Include", "../PostgreSql/Public/*.ps1"); break;
            case "private": resource.SetAttributeValue("Include", "../PostgreSql/Invoke-PostgreSqlTests.ps1"); break;
            case "conditional": resource.Parent!.SetAttributeValue("Condition", "'$(OS)' == 'Windows_NT'"); break;
            case "destination": resource.SetAttributeValue("Link", "Private.ps1"); break;
        }
        Assert.ThrowsAny<Exception>(() => AssertDiagnosticsScriptCopy(project));
    }

    [Fact]
    public void P04OfflineProofTestsDependOnlyOnTheirSameAssemblyHelpersAndBcl()
    {
        AssertTypeAndNested(typeof(PostgreSqlProofTests), AssertP04ProofDependency);
        Assert.ThrowsAny<Exception>(() => AssertP04ProofDependency(typeof(PostgreSqlRoleFixture)));
        Assert.ThrowsAny<Exception>(() => AssertP04ProofDependency(typeof(RuntimeStartupService)));
        Assert.Empty(typeof(PostgreSqlProofTests).GetCustomAttributes<TraitAttribute>());
        foreach (MethodInfo method in typeof(PostgreSqlProofTests).GetMethods().Where(static method => method.GetCustomAttribute<FactAttribute>() is not null))
        {
            FactAttribute fact = method.GetCustomAttribute<FactAttribute>()!;
            Assert.Empty(method.GetCustomAttributes<TraitAttribute>());
            Assert.Null(fact.Skip);
            Assert.Null(fact.SkipUnless);
            Assert.Null(fact.SkipWhen);
        }
    }

    private static void AssertP04ProofDependency(Type type)
    {
        if (type.HasElementType) { AssertP04ProofDependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), AssertP04ProofDependency);
        Type owner = type;
        while (owner.DeclaringType is not null) owner = owner.DeclaringType;
        string name = type.Namespace ?? string.Empty;
        Assert.True(name == "System" || (name.StartsWith("System.", StringComparison.Ordinal) && !name.StartsWith("System.Net", StringComparison.Ordinal))
            || name == "Xunit" || name.StartsWith("Xunit.", StringComparison.Ordinal)
            || owner == typeof(PostgreSqlProofTests) || type == typeof(PostgreSqlTestGate)
            || type == typeof(MigratorProcessRunner) || type == typeof(LogicGate.SiteFactory.Cli.Tests.CliBytesResult)
            || (type.Assembly == typeof(DependencyBoundaryTests).Assembly && owner.FullName == "<PrivateImplementationDetails>"
                && owner.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)),
            "Unapproved P04-A proof dependency: " + type.FullName);
    }

    [Fact]
    public void P04ExecutionBridgeAndTestsHaveClosedDependenciesWithoutPrivateHosts()
    {
        Assert.False(typeof(PostgreSqlHarnessProcess).IsPublic);
        AssertTypeAndNested(typeof(PostgreSqlHarnessProcess), static type => AssertP04ExecutionDependency(type, bridge: true));
        AssertTypeAndNested(typeof(PostgreSqlHarnessExecutionTests), static type => AssertP04ExecutionDependency(type, bridge: false));
        foreach (Type forbidden in new[] { typeof(RuntimeStartupService), typeof(PostgreSqlRoleFixture),
            typeof(RuntimeDbContext), typeof(LogicGate.SiteFactory.Migrator.MigrationCommand) })
        {
            Assert.ThrowsAny<Exception>(() => AssertP04ExecutionDependency(forbidden, bridge: true));
            Assert.ThrowsAny<Exception>(() => AssertP04ExecutionDependency(forbidden, bridge: false));
        }
        foreach (MethodInfo method in typeof(PostgreSqlHarnessExecutionTests).GetMethods()
            .Where(static method => method.GetCustomAttribute<FactAttribute>() is not null))
        {
            FactAttribute fact = method.GetCustomAttribute<FactAttribute>()!;
            Assert.Empty(method.GetCustomAttributes<TraitAttribute>());
            Assert.Null(fact.Skip); Assert.Null(fact.SkipUnless); Assert.Null(fact.SkipWhen);
        }
    }

    private static void AssertP04ExecutionDependency(Type type, bool bridge)
    {
        if (type.HasElementType) { AssertP04ExecutionDependency(type.GetElementType()!, bridge); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), argument => AssertP04ExecutionDependency(argument, bridge));
        Type owner = type;
        while (owner.DeclaringType is not null) owner = owner.DeclaringType;
        string name = type.Namespace ?? string.Empty;
        bool approved = name == "System" || (name.StartsWith("System.", StringComparison.Ordinal) && !name.StartsWith("System.Net", StringComparison.Ordinal))
            || owner == typeof(PostgreSqlHarnessProcess) || type == typeof(MigratorProcessRunner)
            || type == typeof(LogicGate.SiteFactory.Cli.Tests.CliBytesResult)
            || (type.Assembly == typeof(DependencyBoundaryTests).Assembly && owner.FullName == "<PrivateImplementationDetails>"
                && owner.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));
        approved |= bridge
            ? type == typeof(LogicGate.SiteFactory.Cli.Tests.OwnedProcessRunner) || type == typeof(PostgreSqlTestGate)
                || type == typeof(PostgreSqlTestConfiguration) || type == typeof(Npgsql.NpgsqlConnection) || type == typeof(Npgsql.NpgsqlCommand)
            : owner == typeof(PostgreSqlHarnessExecutionTests) || owner == typeof(PostgreSqlProofTests)
                || type == typeof(LogicGate.SiteFactory.Cli.Tests.G04TestEntryPoint) || name == "Xunit" || name.StartsWith("Xunit.", StringComparison.Ordinal);
        Assert.True(approved, "Unapproved P04-C dependency: " + type.FullName);
    }

    private static void AssertP01DiagnosticsDependency(Type type)
    {
        if (type.HasElementType) { AssertP01DiagnosticsDependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), AssertP01DiagnosticsDependency);
        Type owner = type;
        while (owner.DeclaringType is not null) owner = owner.DeclaringType;
        string name = type.Namespace ?? string.Empty;
        Assert.True(name == "System" || (name.StartsWith("System.", StringComparison.Ordinal) && !name.StartsWith("System.Net", StringComparison.Ordinal))
            || name == "Xunit" || name.StartsWith("Xunit.", StringComparison.Ordinal)
            || owner == typeof(PostgreSqlHarnessDiagnosticsTests) || type == typeof(PostgreSqlTestMetrics)
            || type == typeof(PostgreSqlTestMetrics.Phase) || type == typeof(PostgreSqlTestGate)
            || type == typeof(MigratorProcessRunner) || type == typeof(LogicGate.SiteFactory.Cli.Tests.CliBytesResult)
            || type == typeof(LogicGate.SiteFactory.Cli.Tests.G04TestEntryPoint)
            || (type.Assembly == typeof(DependencyBoundaryTests).Assembly && owner.FullName == "<PrivateImplementationDetails>"
                && owner.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)),
            "Unapproved P01-C diagnostics dependency: " + type.FullName);
    }

    [Fact]
    public void P01IncrementATestsUseOnlyTheirExistingOfflineHelpers()
    {
        Type[] tests = [typeof(PostgreSqlTestConfigurationTests), typeof(PostgreSqlRoleBaselineTests)];
        Type[] helpers = [typeof(PostgreSqlTestConfiguration), typeof(PostgreSqlTestDatabase), typeof(PostgreSqlRoleBaseline)];
        Assert.All(helpers, static type =>
        {
            Assert.False(type.IsPublic);
            Assert.Equal(typeof(DependencyBoundaryTests).Assembly, type.Assembly);
        });
        foreach (Type type in tests)
        {
            AssertTypeAndNested(type, AssertP01IncrementADependency);
            Assert.Empty(type.GetCustomAttributes<TraitAttribute>());
            foreach (MethodInfo method in type.GetMethods().Where(static method => method.GetCustomAttribute<FactAttribute>() is not null))
            {
                Assert.Empty(method.GetCustomAttributes<TraitAttribute>());
                FactAttribute fact = method.GetCustomAttribute<FactAttribute>()!;
                Assert.Null(fact.Skip);
                Assert.Null(fact.SkipUnless);
                Assert.Null(fact.SkipWhen);
            }
        }
    }

    [Fact]
    public void P01IncrementAGuardRejectsConnectionAndHostDependencies()
    {
        Assert.ThrowsAny<Exception>(() => AssertTypeDependencies(typeof(ForbiddenStartupConnectivity), AssertP01IncrementADependency));
        Assert.ThrowsAny<Exception>(() => AssertP01IncrementADependency(typeof(RuntimeStartupService)));
        Assert.ThrowsAny<Exception>(() => AssertP01IncrementADependency(typeof(PostgreSqlRoleFixture)));
    }

    private static void AssertP01IncrementADependency(Type type)
    {
        if (type.HasElementType) { AssertP01IncrementADependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), AssertP01IncrementADependency);
        Type owner = type;
        while (owner.DeclaringType is not null) owner = owner.DeclaringType;
        string name = type.Namespace ?? string.Empty;
        bool bcl = (name == "System" || name.StartsWith("System.", StringComparison.Ordinal))
            && !name.StartsWith("System.Net", StringComparison.Ordinal) && type != typeof(System.Diagnostics.Process);
        Assert.True(bcl || name == "Xunit" || name.StartsWith("Xunit.", StringComparison.Ordinal)
            || type == typeof(Npgsql.NpgsqlConnectionStringBuilder) || type == typeof(Npgsql.SslMode)
            || type == typeof(PostgreSqlTestConfiguration) || type == typeof(PostgreSqlTestDatabase) || type == typeof(PostgreSqlRoleBaseline)
            || owner == typeof(PostgreSqlTestConfigurationTests) || owner == typeof(PostgreSqlRoleBaselineTests)
            || (type.Assembly == typeof(DependencyBoundaryTests).Assembly && owner.FullName == "<PrivateImplementationDetails>"
                && owner.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)),
            "Unapproved P01-A dependency: " + type.FullName);
    }

    [Fact]
    public void MigratorIsAnExplicitExecutableWithOnlyTheApprovedProjectReferenceAndPublicCommand()
    {
        string path = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Migrator", "LogicGate.SiteFactory.Migrator.csproj");
        AssertApprovedMigratorProject(ReadProject(path));
        Assembly assembly = typeof(LogicGate.SiteFactory.Migrator.MigrationCommand).Assembly;
        Assert.Equal([typeof(LogicGate.SiteFactory.Migrator.MigrationCommand)], assembly.GetExportedTypes());
        Assert.NotNull(assembly.EntryPoint);
        Assert.Empty(assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false));
        Assert.All(assembly.GetReferencedAssemblies(), static reference => Assert.True(
            reference.Name!.StartsWith("System.", StringComparison.Ordinal)
            || reference.Name is "LogicGate.SiteFactory.Infrastructure.PostgreSql"
                or "Microsoft.EntityFrameworkCore" or "Microsoft.EntityFrameworkCore.Relational"
                or "Microsoft.EntityFrameworkCore.Abstractions"));
        MethodInfo run = Assert.Single(typeof(LogicGate.SiteFactory.Migrator.MigrationCommand)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("RunAsync", run.Name);
        Assert.Equal(typeof(Task<int>), run.ReturnType);
        Assert.Equal([typeof(string[]), typeof(string), typeof(TextWriter), typeof(TextWriter), typeof(CancellationToken)],
            run.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Equal("20260923093309_CreateRuntimeMetadata", LogicGate.SiteFactory.Migrator.MigrationCommand.InitialMigration);
    }

    [Theory]
    [InlineData("PackageReference", "Npgsql")]
    [InlineData("ProjectReference", "../LogicGate.SiteFactory.ModuleSdk/LogicGate.SiteFactory.ModuleSdk.csproj")]
    [InlineData("FrameworkReference", "Microsoft.AspNetCore.App")]
    [InlineData("Reference", "Hidden.Provider")]
    public void MigratorGuardRejectsAdditionalDependenciesInMemory(string element, string value)
    {
        XDocument project = new(new XElement("Project",
            new XElement("PropertyGroup", new XElement("OutputType", "Exe"), new XElement("IsPackable", "false")),
            new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include",
                "../LogicGate.SiteFactory.Infrastructure.PostgreSql/LogicGate.SiteFactory.Infrastructure.PostgreSql.csproj")),
                new XElement(element, new XAttribute("Include", value)))));
        Assert.ThrowsAny<Exception>(() => AssertApprovedMigratorProject(project));
    }

    private static void AssertApprovedMigratorProject(XDocument project)
    {
        Assert.Equal("Exe", Assert.Single(project.Descendants("OutputType")).Value);
        Assert.Equal("false", Assert.Single(project.Descendants("IsPackable")).Value);
        Assert.Equal(["../LogicGate.SiteFactory.Infrastructure.PostgreSql/LogicGate.SiteFactory.Infrastructure.PostgreSql.csproj"],
            project.Descendants("ProjectReference").Select(static element => element.Attribute("Include")?.Value));
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is
            "PackageReference" or "FrameworkReference" or "Reference" or "InternalsVisibleTo" or "Compile");
    }

    private static void AssertApprovedProject(XDocument project)
    {
        Assert.Equal(["Microsoft.EntityFrameworkCore.Relational", "Microsoft.Extensions.Configuration.Abstractions", "Microsoft.Extensions.DependencyInjection.Abstractions", "Npgsql", "Npgsql.EntityFrameworkCore.PostgreSQL"],
            project.Descendants("PackageReference").Select(static element => element.Attribute("Include")?.Value).Order(StringComparer.Ordinal));
        Assert.Equal(["../LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj",
            "../LogicGate.SiteFactory.Runtime/LogicGate.SiteFactory.Runtime.csproj",
            "../LogicGate.SiteFactory.Runtime.Hosting/LogicGate.SiteFactory.Runtime.Hosting.csproj"],
            project.Descendants("ProjectReference").Select(static element => element.Attribute("Include")?.Value));
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is "FrameworkReference" or "Reference" or "InternalsVisibleTo");
    }

    private static XDocument ReadProject(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static void AssertConfigurationType(Type type) => AssertTypeDependencies(type, AssertConfigurationDependency);

    private static void AssertTypeDependencies(Type type, Action<Type> check, Action<MemberInfo>? checkMember = null)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.All(type.GetCustomAttributesData(), attribute => check(attribute.AttributeType));
        if (type.BaseType is not null) check(type.BaseType);
        Assert.All(type.GetInterfaces(), check);
        Assert.All(type.GetFields(flags), field => check(field.FieldType));
        foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
        {
            Assert.All(method.GetParameters(), parameter => check(parameter.ParameterType));
            if (method is MethodInfo info) check(info.ReturnType);
            MethodBody? body = method.GetMethodBody();
            if (body is not null)
            {
                Assert.All(body.LocalVariables, local => check(local.LocalType));
                Assert.All(body.ExceptionHandlingClauses.Where(static clause => clause.Flags == ExceptionHandlingClauseOptions.Clause),
                    clause => check(clause.CatchType!));
            }
            byte[]? il = body?.GetILAsByteArray();
            if (il is null) continue;
            for (int offset = 0; offset < il.Length;)
            {
                short value = il[offset++];
                if (value == 0xfe) value = unchecked((short)(0xfe00 | il[offset++]));
                OperandType operand = OpCodesByValue[value].OperandType;
                Assert.NotEqual(OperandType.InlineSig, operand);
                if (operand is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
                {
                    MemberInfo member = method.Module.ResolveMember(BitConverter.ToInt32(il, offset), type.GetGenericArguments(),
                        method.IsGenericMethod ? method.GetGenericArguments() : null)!;
                    checkMember?.Invoke(member);
                    if (member is Type dependency) check(dependency);
                    else if (member.DeclaringType is not null) check(member.DeclaringType);
                    if (member is MethodInfo called && called.IsGenericMethod)
                        Assert.All(called.GetGenericArguments(), check);
                }
                offset += operand switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                    _ => 4,
                };
            }
        }
    }

    private static void AssertConfigurationDependency(Type type)
    {
        if (type.HasElementType) { AssertConfigurationDependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), AssertConfigurationDependency);
        string name = type.Namespace ?? string.Empty;
        Assert.True(name == "System" || name.StartsWith("System.", StringComparison.Ordinal)
            || name == "Npgsql" || name.StartsWith("Npgsql.", StringComparison.Ordinal)
            || name == "LogicGate.SiteFactory.Runtime.Contracts"
            || name == "LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration"
            || IsCompilerData(type), $"Unapproved configuration dependency: {type.FullName}");
    }

    [Fact]
    public void ReaderCollectsWithoutEfHostOrCompatibilityDecisionsAndStartupRemainsLocal()
    {
        AssertTypeAndNested(typeof(PostgreSqlDatabaseReader), AssertReaderDependency);
        AssertTypeAndNested(typeof(PostgreSqlStartupValidation), AssertStartupDependency);
        AssertTypeAndNested(typeof(PostgreSqlStartupRegistration), AssertStartupDependency);
        Assert.Equal("LogicGate.SiteFactory.Runtime", typeof(DatabaseCompatibilityProbe).Assembly.GetName().Name);
        Assert.Equal("LogicGate.SiteFactory.Runtime.Database", typeof(DatabaseCompatibilityProbe).Namespace);
        Assert.DoesNotContain(typeof(PostgreSqlStartupValidation).GetConstructors().SelectMany(static constructor => constructor.GetParameters()),
            static parameter => parameter.ParameterType == typeof(IRuntimeDatabaseReader));
        Assert.Equal(["Configuration", "Id"], typeof(PostgreSqlStartupValidation).GetProperties().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.False(typeof(PostgreSqlStartupValidation).GetProperty("Configuration")!.SetMethod!.IsPublic);
        Assert.Equal([typeof(IStartupValidation)], typeof(PostgreSqlStartupValidation).GetInterfaces());
        Assert.Equal(["AddPostgreSqlStartupValidation"], typeof(PostgreSqlStartupRegistration)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(static method => method.Name));
    }

    [Theory]
    [InlineData(typeof(ForbiddenReaderPersistence))]
    [InlineData(typeof(ForbiddenReaderDecision))]
    public void ReaderBoundaryRejectsPersistenceAndPureProbeDecisions(Type type) =>
        Assert.ThrowsAny<Exception>(() => AssertTypeDependencies(type, AssertReaderDependency));

    [Fact]
    public void StartupBoundaryRejectsConnectivityOrProbeCalls() =>
        Assert.ThrowsAny<Exception>(() => AssertTypeDependencies(typeof(ForbiddenStartupConnectivity), AssertStartupDependency));

    [Fact]
    public void G06AdaptersHaveOnlyTheirApprovedApisAndOrchestrateWithoutProvidersOrHostResponsibilities()
    {
        Type[] adapters = [typeof(WebDatabaseReadiness), typeof(WebDatabaseStartupRegistration),
            typeof(WorkerDatabaseStartupValidator), typeof(WorkerDatabaseStartupRegistration)];
        foreach (Type type in adapters)
        {
            Assert.Equal("LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime", type.Namespace);
            AssertTypeAndNested(type, AssertAdapterDependency);
            Assert.False(typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(type));
        }
        Assert.Equal(new[] { typeof(IDisposable), typeof(IRuntimeStartupValidator), typeof(IRuntimeStartupReadiness), typeof(IRuntimeReadinessRefresh) }.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            typeof(WebDatabaseReadiness).GetInterfaces().OrderBy(static type => type.FullName, StringComparer.Ordinal));
        Assert.Equal([typeof(IRuntimeStartupValidator)], typeof(WorkerDatabaseStartupValidator).GetInterfaces());
        Assert.Equal(["ConfigurationValidated", "CurrentReport", "IsReady"],
            typeof(WebDatabaseReadiness).GetProperties().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.All(typeof(WebDatabaseReadiness).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.Equal(["Dispose", "RefreshAsync", "ValidateAsync"], DeclaredOperations(typeof(WebDatabaseReadiness)));
        Assert.Equal(["ValidateAsync"], DeclaredOperations(typeof(WorkerDatabaseStartupValidator)));
        Assert.Equal(["AddWebDatabaseReadiness"], DeclaredOperations(typeof(WebDatabaseStartupRegistration)));
        Assert.Equal(["AddWorkerDatabaseStartupValidation"], DeclaredOperations(typeof(WorkerDatabaseStartupRegistration)));
        foreach (Type type in new[] { typeof(WebDatabaseReadiness), typeof(WorkerDatabaseStartupValidator) })
        {
            Assert.Equal([typeof(IEnumerable<IStartupValidation>), typeof(RuntimeConfigurationStartupValidation),
                typeof(Microsoft.Extensions.Configuration.IConfiguration), typeof(Func<IRuntimeDatabaseReader>)],
                Assert.Single(type.GetConstructors()).GetParameters().Select(static parameter => parameter.ParameterType));
            Assert.Equal(typeof(Task<RuntimeStartupReport>), type.GetMethod("ValidateAsync")!.ReturnType);
            Assert.Equal([typeof(CancellationToken)], type.GetMethod("ValidateAsync")!.GetParameters().Select(static parameter => parameter.ParameterType));
        }
        Assert.Equal(typeof(Task<bool>), typeof(WebDatabaseReadiness).GetMethod("RefreshAsync")!.ReturnType);
        Assert.Equal([typeof(CancellationToken)], typeof(WebDatabaseReadiness).GetMethod("RefreshAsync")!.GetParameters().Select(static parameter => parameter.ParameterType));
        string directory = Path.Combine(FindRepositoryRoot(), "src", "LogicGate.SiteFactory.Infrastructure.PostgreSql", "Runtime");
        foreach (string file in new[] { "WebDatabaseReadiness.cs", "WorkerDatabaseStartupValidator.cs" })
        {
            string source = File.ReadAllText(Path.Combine(directory, file));
            Assert.DoesNotContain("sitefactory_runtime", source, StringComparison.Ordinal);
            Assert.DoesNotContain("20260923093309", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DatabaseCompatibilityProbe.Evaluate", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DatabaseCompatibilityProbe.Schema", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DatabaseCompatibilityProbe.InitialMigration", source, StringComparison.Ordinal);
            Assert.DoesNotMatch("\\b(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|GRANT|REVOKE)\\b", source);
        }
    }

    [Theory]
    [InlineData(typeof(ForbiddenReaderPersistence))]
    [InlineData(typeof(ForbiddenStartupConnectivity))]
    public void G06AdapterGuardRejectsPersistenceAndProviderMethodBodies(Type type) =>
        Assert.ThrowsAny<Exception>(() => AssertTypeDependencies(type, AssertAdapterDependency));

    private static IEnumerable<string> DeclaredOperations(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(static method => !method.IsSpecialName).Select(static method => method.Name).Order(StringComparer.Ordinal);

    private static void AssertAdapterDependency(Type type)
    {
        if (type.HasElementType) { AssertAdapterDependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) foreach (Type argument in type.GetGenericArguments()) AssertAdapterDependency(argument);
        string name = type.Namespace ?? string.Empty;
        Type[] adapters = [typeof(WebDatabaseReadiness), typeof(WebDatabaseStartupRegistration),
            typeof(WorkerDatabaseStartupValidator), typeof(WorkerDatabaseStartupRegistration)];
        Assert.True(name == "System" || name.StartsWith("System.", StringComparison.Ordinal) || IsCompilerData(type)
            || (type.Assembly == typeof(WebDatabaseReadiness).Assembly
                && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                && type.IsGenericType && type.GetGenericTypeDefinition().FullName == "<>z__ReadOnlyArray`1")
            || name == "LogicGate.SiteFactory.Runtime.Contracts" || name == "LogicGate.SiteFactory.Runtime.Database"
            || type == typeof(LogicGate.SiteFactory.Runtime.RuntimeStartupValidator)
            || type == typeof(RuntimeConfigurationStartupValidation) || type == typeof(IRuntimeStartupReadiness) || type == typeof(IRuntimeReadinessRefresh)
            || type == typeof(PostgreSqlDatabaseReader) || type == typeof(PostgreSqlStartupValidation)
            || adapters.Any(adapter => type == adapter || type.DeclaringType == adapter)
            || name.StartsWith("Microsoft.Extensions.Configuration", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal),
            "Unapproved G06 adapter dependency: " + type.FullName);
    }

    private static void AssertTypeAndNested(Type type, Action<Type> check, Action<MemberInfo>? checkMember = null)
    {
        AssertTypeDependencies(type, check, checkMember);
        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) AssertTypeAndNested(nested, check, checkMember);
    }

    [Fact]
    public void P02ProofsUseOnlyExistingPublicHelpersAndNeverInvokeAnotherFact()
    {
        foreach (Type type in P02ProofTypes)
            AssertTypeAndNested(type, AssertP02Dependency, AssertNoFactCall);
    }

    [Theory]
    [InlineData(typeof(RuntimeDbContext))]
    [InlineData(typeof(WebDatabaseReadiness))]
    [InlineData(typeof(RuntimeStartupService))]
    public void P02GuardRejectsPersistenceAndRuntimeHostComposition(Type type) =>
        Assert.ThrowsAny<Exception>(() => AssertP02Dependency(type));

    [Fact]
    public void P02GuardRejectsFactCallsButAllowsApplyAndBootstrapHelpers()
    {
        AssertNoFactCall(typeof(PostgreSqlMigrationTests).GetMethod("ApplyAsync", BindingFlags.Static | BindingFlags.NonPublic)!);
        AssertNoFactCall(typeof(PostgreSqlBootstrapTests).GetMethod("BootstrapAsync", BindingFlags.Static | BindingFlags.NonPublic)!);
        Assert.ThrowsAny<Exception>(() => AssertNoFactCall(typeof(PostgreSqlMigrationTests)
            .GetMethod(nameof(PostgreSqlMigrationTests.ExtractedMigratorAppliesOnceWithItsOwnLoginAndRejectsRuntimeCredentials))!));
        Assert.ThrowsAny<Exception>(() => AssertNoFactCall(typeof(PostgreSqlBootstrapTests)
            .GetMethod(nameof(PostgreSqlBootstrapTests.ExtractedBootstrapIsTransactionalIdempotentAndRefusesMismatchesAndRuntimeWrites))!));
    }

    [Fact]
    public void SharedPostgreSqlGateUsesLiteralInventoriesWithoutReferencingTheirProofTypes() =>
        AssertTypeAndNested(typeof(PostgreSqlTestGate), AssertSharedGateDependency);

    private static readonly Type[] P02ProofTypes = [typeof(PostgreSqlCatalogSnapshotTests),
        typeof(PostgreSqlIntegrationTests), typeof(PostgreSqlPermissionMatrixTests)];

    private static void AssertP02Dependency(Type type)
    {
        if (type.HasElementType) { AssertP02Dependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), AssertP02Dependency);
        Type owner = type;
        while (owner.DeclaringType is not null) owner = owner.DeclaringType;
        string name = type.Namespace ?? string.Empty;
        Type[] helpers = [typeof(PostgreSqlTestConfiguration), typeof(PostgreSqlTestDatabase), typeof(PostgreSqlRoleFixture),
            typeof(PostgreSqlRoleBaseline), typeof(PostgreSqlTestLogin), typeof(PostgreSqlCatalogSnapshot),
            typeof(PostgreSqlSchemaPrivilegeCases), typeof(PostgreSqlTestGate), typeof(PostgreSqlTestMetrics),
            typeof(PostgreSqlMigrationTests), typeof(PostgreSqlBootstrapTests), typeof(MigratorProcessResult)];
        bool bcl = (name == "System" || name.StartsWith("System.", StringComparison.Ordinal))
            && !name.StartsWith("System.Net", StringComparison.Ordinal) && type != typeof(System.Diagnostics.Process);
        Assert.True(bcl || name == "Xunit" || name.StartsWith("Xunit.", StringComparison.Ordinal)
            || name == "Npgsql" || name.StartsWith("Npgsql.", StringComparison.Ordinal)
            || P02ProofTypes.Contains(owner) || helpers.Contains(owner)
            || (owner.Assembly == typeof(DependencyBoundaryTests).Assembly && owner.Name == "<PrivateImplementationDetails>"
                && owner.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)),
            "Unapproved P02 proof dependency: " + type.FullName);
    }

    private static void AssertNoFactCall(MemberInfo member)
    {
        if (member is MethodInfo method)
            Assert.Null(method.GetCustomAttribute<FactAttribute>());
    }

    private static void AssertSharedGateDependency(Type type)
    {
        if (type.HasElementType) { AssertSharedGateDependency(type.GetElementType()!); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) Assert.All(type.GetGenericArguments(), AssertSharedGateDependency);
        Type owner = type;
        while (owner.DeclaringType is not null) owner = owner.DeclaringType;
        string name = type.Namespace ?? string.Empty;
        Assert.True(name == "System" || name.StartsWith("System.", StringComparison.Ordinal)
            || owner == typeof(PostgreSqlTestGate) || owner == typeof(PostgreSqlTestConfiguration)
            || (owner.Assembly == typeof(DependencyBoundaryTests).Assembly && owner.Name == "<PrivateImplementationDetails>"
                && owner.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)),
            "Shared configuration gate depends on a proof type: " + type.FullName);
    }

    private static void AssertReaderDependency(Type type) => AssertScopedDependency(type, startup: false);
    private static void AssertStartupDependency(Type type) => AssertScopedDependency(type, startup: true);

    private static void AssertScopedDependency(Type type, bool startup)
    {
        if (type.HasElementType) { AssertScopedDependency(type.GetElementType()!, startup); return; }
        if (type.IsGenericParameter) return;
        if (type.IsGenericType) foreach (Type argument in type.GetGenericArguments()) AssertScopedDependency(argument, startup);
        string name = type.Namespace ?? string.Empty;
        Assert.True(name == "System" || name.StartsWith("System.", StringComparison.Ordinal)
            || IsCompilerData(type)
            || (startup && type.Assembly == typeof(PostgreSqlStartupValidation).Assembly
                && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                && type.IsGenericType && type.GetGenericTypeDefinition().FullName == "<>z__ReadOnlySingleElementList`1")
            || name == "LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration"
            || (startup ? name == "LogicGate.SiteFactory.Runtime.Contracts"
                || type == typeof(LogicGate.SiteFactory.Runtime.Hosting.RuntimeStartupService)
                || type == typeof(PostgreSqlStartupValidation) || type.DeclaringType == typeof(PostgreSqlStartupValidation)
                || type == typeof(PostgreSqlStartupRegistration) || type.DeclaringType == typeof(PostgreSqlStartupRegistration)
                || name.StartsWith("Microsoft.Extensions.Configuration", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal)
                : name == "Npgsql" || name.StartsWith("Npgsql.", StringComparison.Ordinal)
                || (name == "LogicGate.SiteFactory.Runtime.Database" && type != typeof(DatabaseCompatibilityProbe))
                || type == typeof(PostgreSqlDatabaseReader) || type.DeclaringType == typeof(PostgreSqlDatabaseReader)),
            "Unapproved G05 responsibility dependency: " + type.FullName);
    }

    private static class ForbiddenReaderPersistence
    {
        public static void Build() => GC.KeepAlive(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder());
    }

    private static class ForbiddenReaderDecision
    {
        public static void Decide() => GC.KeepAlive(DatabaseCompatibilityProbe.Evaluate(null!, null!));
    }

    private static class ForbiddenStartupConnectivity
    {
        public static void Connect() => GC.KeepAlive(new Npgsql.NpgsqlConnection());
    }

    private static bool IsCompilerData(Type type)
    {
        while (type.DeclaringType is not null) type = type.DeclaringType;
        return type.Assembly == typeof(PostgreSqlRuntimeConfiguration).Assembly
            && type.FullName == "<PrivateImplementationDetails>"
            && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }

    private sealed class ForbiddenConfigurationSignature
    {
        public RuntimeDbContext? Context { get; init; }
    }

    private static class ForbiddenConfigurationBody
    {
        public static void Build() => GC.KeepAlive(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder());
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SiteFactory.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
