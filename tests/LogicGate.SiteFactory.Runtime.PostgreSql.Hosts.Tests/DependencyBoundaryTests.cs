using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

public sealed class DependencyBoundaryTests
{
    private const string ProjectName = "LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests";
    private static readonly string[] Projects = ["LogicGate.SiteFactory.Infrastructure.PostgreSql", "LogicGate.SiteFactory.Migrator",
        "LogicGate.SiteFactory.Runtime", "LogicGate.SiteFactory.Runtime.AspNetCore", "LogicGate.SiteFactory.Runtime.Contracts", "LogicGate.SiteFactory.Runtime.Hosting"];
    private static readonly string[] Packages = ["Microsoft.NET.Test.Sdk", "Npgsql", "xunit.runner.visualstudio", "xunit.v3"];
    private static readonly string[] SharedSources = ["PostgreSqlRoleBaseline.cs", "PostgreSqlRoleFixture.cs", "PostgreSqlTestConfiguration.cs",
        "PostgreSqlTestDatabase.cs", "PostgreSqlTestGate.cs"];
    private static readonly Dictionary<short, OpCode> Instructions = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!).ToDictionary(static code => code.Value);

    [Fact]
    public void TestProjectUsesExactlyTheApprovedDirectDependenciesAndFramework()
    {
        AssertProject(ReadProject(Path.Combine(TestDirectory(), ProjectName + ".csproj")));
        Assembly assembly = typeof(DependencyBoundaryTests).Assembly;
        Assert.Empty(assembly.GetCustomAttributes<InternalsVisibleToAttribute>());
        Assert.Equal(Projects, assembly.GetReferencedAssemblies().Select(static name => name.Name!)
            .Where(static name => name.StartsWith("LogicGate.SiteFactory.", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.False(typeof(TestHosts).IsVisible);
        Assert.False(typeof(OfflineDatabaseFixture).IsVisible);
        foreach (string name in SharedSources)
            Assert.False(assembly.GetType("LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql." + Path.GetFileNameWithoutExtension(name), throwOnError: true)!.IsVisible);
    }

    [Fact]
    public void SourcesAndAssemblyNeverLoadPrivateCompositionOrOtherTestAssemblies()
    {
        foreach (string file in Directory.EnumerateFiles(TestDirectory(), "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(file) == nameof(DependencyBoundaryTests) + ".cs") continue;
            string source = File.ReadAllText(file);
            AssertSourceBoundary(source, realProof: false);
        }
        Assert.DoesNotContain(typeof(DependencyBoundaryTests).Assembly.GetReferencedAssemblies(), static name =>
            name.Name!.StartsWith("Client.", StringComparison.Ordinal)
            || name.Name.StartsWith("LogicGate.SiteFactory.Modules.", StringComparison.Ordinal)
            || name.Name.StartsWith("LogicGate.SiteFactory.Cli", StringComparison.Ordinal)
            || name.Name == "LogicGate.SiteFactory.ModuleSdk"
            || name.Name.EndsWith(".Tests", StringComparison.Ordinal)
            || name.Name == "Microsoft.AspNetCore.TestHost");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(TestDirectory(), "PostgreSql"), "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            AssertSourceBoundary(source, realProof: true);
        }
        foreach (bool realProof in new[] { false, true })
            Assert.ThrowsAny<Exception>(() => AssertSourceBoundary("using LogicGate.SiteFactory.Modules.Unapproved;", realProof));
    }

    private static void AssertSourceBoundary(string source, bool realProof)
    {
        string[] forbidden = realProof
            ? ["Client.", "LogicGate.SiteFactory.Modules.", "templates/", "AssemblyLoadContext", "Assembly.Load",
                "Process.Start", "InternalsVisibleTo", ".Evaluate(", "new DatabaseCompatibilityProbe", "CreateDbContext"]
            : ["Client.", "LogicGate.SiteFactory.Modules.", "MinimalApplication", "MinimalWorker", "templates/",
                "AssemblyLoadContext", "Assembly.Load", "Process.Start", "PostgreSqlRoleFixture", "PostgreSqlTestGate"];
        foreach (string dependency in forbidden)
            Assert.DoesNotContain(dependency, source, StringComparison.Ordinal);
    }

    [Fact]
    public void OfflineFixtureContainsOnlyObservationsAndNoProviderIoSqlOrCopiedPolicy()
    {
        string source = File.ReadAllText(Path.Combine(TestDirectory(), nameof(OfflineDatabaseFixture) + ".cs"));
        foreach (string forbidden in new[] { "Npgsql", "EntityFrameworkCore", ".Evaluate(", ".ValidateAsync(", "CreateDbContext", "DbConnection(" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        Assert.DoesNotMatch("\\b(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|GRANT|REVOKE)\\b", source);
        foreach (Type type in typeof(OfflineDatabaseFixture).Assembly.GetTypes().Where(static type =>
                     type == typeof(OfflineDatabaseFixture) || type.DeclaringType == typeof(OfflineDatabaseFixture)))
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
                foreach (MemberInfo called in ReferencedMembers(method))
                {
                    string scope = called.DeclaringType?.Namespace ?? string.Empty;
                    Assert.False(scope.StartsWith("Npgsql", StringComparison.Ordinal) || scope.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
                    Assert.False(called.DeclaringType == typeof(LogicGate.SiteFactory.Runtime.Database.DatabaseCompatibilityProbe));
                    Assert.False(called.DeclaringType is { } owner && typeof(System.Data.Common.DbConnection).IsAssignableFrom(owner));
                }
        }
    }

    [Fact]
    public void NoProductionProjectReferencesThisTestProject()
    {
        string root = FindRepositoryRoot();
        string testProject = Path.GetFullPath(Path.Combine(TestDirectory(), ProjectName + ".csproj"));
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
            foreach (string? reference in Values(ReadProject(file), "ProjectReference"))
                Assert.NotEqual(testProject, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!,
                    reference!.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void RealBridgeIsPublicCompilableAndNotAnOfflineXunitTest()
    {
        MethodInfo bridge = typeof(WebDatabaseReadinessTests).GetMethod(nameof(WebDatabaseReadinessTests.ExerciseRealAsync))!;
        Assert.True(bridge.IsPublic && bridge.IsStatic);
        Assert.Equal(typeof(Task<string[]>), bridge.ReturnType);
        Assert.Equal([typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(Func<Task>), typeof(CancellationToken)],
            bridge.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Null(bridge.GetCustomAttribute<FactAttribute>());
        Assert.Null(bridge.GetCustomAttribute<TheoryAttribute>());
        string source = File.ReadAllText(Path.Combine(TestDirectory(), nameof(WebDatabaseReadinessTests) + ".cs"));
        string body = source[source.IndexOf("public static async Task<string[]> ExerciseRealAsync", StringComparison.Ordinal)..];
        Assert.DoesNotContain("OfflineDatabaseFixture", body, StringComparison.Ordinal);
        Assert.Contains("Assert.IsType<PostgreSqlDatabaseReader>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ControlledReader", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureDatabaseServices", body, StringComparison.Ordinal);
        // Shape only here; the three P03 Facts execute real PostgreSQL separately.
    }

    [Fact]
    public void ExactlyThreeRealFactsRetainTheirOwnClosedInventoryAndConfigurationGate()
    {
        string[] expected = ["PostgreSqlWebReadinessTests.RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent",
            "PostgreSqlWorkerReadinessTests.RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites",
            "PostgreSqlWebConcurrencyTests.RealWebReadinessHasNoFalseFailuresUnderConcurrentTraffic"];
        Type[] types = typeof(DependencyBoundaryTests).Assembly.GetTypes().Where(static type =>
            type.Namespace == "LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql" && type.IsPublic).ToArray();
        MethodInfo[] facts = types.SelectMany(static type => type.GetMethods())
            .Where(static method => method.GetCustomAttribute<FactAttribute>() is not null).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), facts.Select(static method => method.DeclaringType!.Name + "." + method.Name).Order(StringComparer.Ordinal));
        foreach (MethodInfo method in facts)
        {
            FactAttribute fact = method.GetCustomAttribute<FactAttribute>()!;
            Assert.Equal("IsConfiguredOrRequired", fact.SkipUnless);
            Assert.Contains(method.DeclaringType!.GetCustomAttributes<TraitAttribute>(), static trait => trait.Name == "Category" && trait.Value == "PostgreSql");
            Assert.Contains(method.DeclaringType!.GetCustomAttributes<TraitAttribute>(), static trait => trait.Name == "ExtractionLot" && trait.Value == "P03");
            string source = File.ReadAllText(Path.Combine(TestDirectory(), "PostgreSql", method.DeclaringType!.Name + ".cs"));
            Assert.Contains("PostgreSqlTestGate.IsConfiguredOrRequired", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AddSingleton", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ValidateInventories", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ExpectedP01Tests", source, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Include")]
    [InlineData("Link")]
    public void SourceLinkGuardRejectsAnyChangeToAnApprovedLink(string attribute)
    {
        XDocument project = ReadProject(Path.Combine(TestDirectory(), ProjectName + ".csproj"));
        AssertProject(project);
        project.Descendants("Compile").First().SetAttributeValue(attribute, "Forbidden.cs");
        Assert.ThrowsAny<Exception>(() => AssertProject(project));
    }

    [Theory]
    [InlineData("ProjectReference")]
    [InlineData("PackageReference")]
    [InlineData("FrameworkReference")]
    [InlineData("Compile")]
    [InlineData("Import")]
    [InlineData("Reference")]
    [InlineData("InternalsVisibleTo")]
    public void ProjectGuardRejectsAdditionalDependenciesAndLinkedSourcesInMemory(string element)
    {
        XDocument project = ReadProject(Path.Combine(TestDirectory(), ProjectName + ".csproj"));
        AssertProject(project);
        project.Root!.Add(new XElement("ItemGroup", new XElement(element, new XAttribute("Include", "Forbidden.Dependency"))));
        Assert.ThrowsAny<Exception>(() => AssertProject(project));
    }

    private static void AssertProject(XDocument project)
    {
        Assert.Equal("Microsoft.NET.Sdk", project.Root!.Attribute("Sdk")?.Value);
        Assert.Equal("true", Assert.Single(project.Descendants("IsTestProject")).Value);
        Assert.Equal("false", Assert.Single(project.Descendants("IsPackable")).Value);
        Assert.Equal(["Microsoft.AspNetCore.App"], Values(project, "FrameworkReference"));
        Assert.Equal(Packages, Values(project, "PackageReference"));
        Assert.Equal(Projects.Select(static name => "../../src/" + name + "/" + name + ".csproj").Order(StringComparer.Ordinal),
            Values(project, "ProjectReference"));
        Assert.DoesNotContain(project.Descendants(), static element => element.Name.LocalName is "Import" or "Reference" or "InternalsVisibleTo");
        Assert.Equal(SharedSources.Select(static name => "../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/" + name), Values(project, "Compile"));
        Assert.Equal(SharedSources.Select(static name => "PostgreSql/SharedG04/" + name), project.Descendants("Compile")
            .OrderBy(static element => element.Attribute("Include")!.Value, StringComparer.Ordinal).Select(static element => element.Attribute("Link")?.Value));
        Assert.All(project.Descendants("Compile"), static element => Assert.Equal(["Include", "Link"],
            element.Attributes().Select(static attribute => attribute.Name.LocalName).Order(StringComparer.Ordinal)));
        Assert.All(project.Descendants("PackageReference"), static element => Assert.Null(element.Attribute("Version")));
    }

    private static IEnumerable<MemberInfo> ReferencedMembers(MethodBase method)
    {
        byte[]? body = method.GetMethodBody()?.GetILAsByteArray();
        if (body is null) yield break;
        for (int offset = 0; offset < body.Length;)
        {
            short value = body[offset++];
            if (value == 0xfe) value = unchecked((short)(0xfe00 | body[offset++]));
            OperandType operand = Instructions[value].OperandType;
            if (operand is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
                yield return method.Module.ResolveMember(BitConverter.ToInt32(body, offset), method.DeclaringType!.GetGenericArguments(),
                    method.IsGenericMethod ? method.GetGenericArguments() : null)!;
            offset += operand switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(body, offset),
                _ => 4,
            };
        }
    }

    private static IEnumerable<string?> Values(XDocument document, string element) => document.Descendants()
        .Where(item => item.Name.LocalName == element).Select(static item => item.Attribute("Include")?.Value).Order(StringComparer.Ordinal);

    private static XDocument ReadProject(string file)
    {
        using XmlReader reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static string TestDirectory() => Path.Combine(FindRepositoryRoot(), "tests", ProjectName);

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SiteFactory.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
