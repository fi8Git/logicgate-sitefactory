using System.Reflection;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

public sealed class PostgreSqlTestGateTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", "", false)]
    [InlineData(null, "1", true)]
    [InlineData(null, "false", true)]
    [InlineData(" ", null, true)]
    [InlineData(null, " ", true)]
    [InlineData("missing-file", null, true)]
    public void OnlyAbsentOptionalConfigurationCanSkip(string? file, string? required, bool expected) =>
        Assert.Equal(expected, PostgreSqlTestGate.ShouldRun(file, required));

    [Theory]
    [InlineData(null, "1")]
    [InlineData(" ", "1")]
    [InlineData("missing-file", "1")]
    [InlineData(null, "false")]
    [InlineData(null, " ")]
    public async Task RequiredConfigurationErrorsFailInsteadOfPassingOrSkipping(string? file, string required)
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PostgreSqlTestGate.ReadAsync(file, required, TestContext.Current.CancellationToken));
        Assert.StartsWith("SF-TEST-PG-", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task CancellationPrecedesConfigurationIo()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PostgreSqlTestGate.ReadAsync("missing-file", "1", cancellation.Token));
    }

    [Theory]
    [MemberData(nameof(IncompleteOrUnsafeConnections))]
    public void IncompleteOrUnsafeConfigurationIsRejectedWithoutProviderDetails(string content)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => PostgreSqlTestConfiguration.Parse(content));
        Assert.StartsWith("SF-TEST-PG-", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DO_NOT_DISCLOSE", error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        if (content.StartsWith("Host=localhost;", StringComparison.Ordinal))
            Assert.StartsWith("SF-TEST-PG-003", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidConfigurationIsExplicitRedactedAndNonPooledWithoutOpeningAConnection()
    {
        PostgreSqlTestConfiguration configuration = PostgreSqlTestConfiguration.Parse(ConnectionFixture("127.0.0.1").ConnectionString);
        Assert.Equal("PostgreSQL integration test configuration (redacted)", configuration.ToString());
        Assert.Equal("{}", System.Text.Json.JsonSerializer.Serialize(configuration));
        using NpgsqlConnection connection = configuration.Connection("sf_test_00000000000000000000000000000000");
        NpgsqlConnectionStringBuilder settings = new(connection.ConnectionString);
        Assert.False(settings.Pooling);
        Assert.False(settings.IncludeErrorDetail);
        Assert.False(settings.LogParameters);
        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
        Assert.Equal(5, settings.Timeout);
        Assert.Equal(10, settings.CommandTimeout);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    public static TheoryData<string> IncompleteOrUnsafeConnections => new()
    {
        "not-a-connection-string",
        new NpgsqlConnectionStringBuilder { Host = "127.0.0.1", ["Password"] = "DO_NOT_DISCLOSE" }.ConnectionString,
        ConnectionFixture("localhost", 5432).ConnectionString,
    };

    private static NpgsqlConnectionStringBuilder ConnectionFixture(string host, int port = 54321) => new()
    {
        Host = host,
        Port = port,
        Database = "sitefactory_test_admin",
        Username = "sitefactory_test_admin",
        ["Password"] = "DO_NOT_DISCLOSE",
        SslMode = SslMode.VerifyFull,
        RootCertificate = "unused.crt",
    };

    [Fact]
    public void ExpectedInventoryExactlyMatchesEveryRealPostgreSqlFact()
    {
        string[] actual = typeof(PostgreSqlTestGate).Assembly.GetTypes()
            .Where(static type => type.GetCustomAttributes<TraitAttribute>().Any(static trait =>
                trait.Name == "Category" && trait.Value == "PostgreSql"))
            .SelectMany(static type => type.GetMethods().Where(static method => method.GetCustomAttribute<FactAttribute>() is not null)
                .Select(method => type.FullName + "." + method.Name))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(5, PostgreSqlTestGate.ExpectedTests.Count);
        Assert.Equal(3, PostgreSqlTestGate.ExpectedG05Tests.Count);
        Assert.Equal(8, PostgreSqlTestGate.ExpectedP01Tests.Count);
        Assert.Equal(4, PostgreSqlTestGate.ExpectedP02Tests.Count);
        Assert.Empty(PostgreSqlTestGate.ExpectedTests.Intersect(PostgreSqlTestGate.ExpectedG05Tests, StringComparer.Ordinal));
        Assert.Empty(PostgreSqlTestGate.ExpectedTests.Intersect(PostgreSqlTestGate.ExpectedP01Tests, StringComparer.Ordinal));
        Assert.Empty(PostgreSqlTestGate.ExpectedG05Tests.Intersect(PostgreSqlTestGate.ExpectedP01Tests, StringComparer.Ordinal));
        Assert.Empty(PostgreSqlTestGate.ExpectedTests.Intersect(PostgreSqlTestGate.ExpectedP02Tests, StringComparer.Ordinal));
        Assert.Empty(PostgreSqlTestGate.ExpectedG05Tests.Intersect(PostgreSqlTestGate.ExpectedP02Tests, StringComparer.Ordinal));
        Assert.Empty(PostgreSqlTestGate.ExpectedP01Tests.Intersect(PostgreSqlTestGate.ExpectedP02Tests, StringComparer.Ordinal));
        Assert.Equal(20, actual.Length);
        Assert.Equal(PostgreSqlTestGate.ExpectedTests.Concat(PostgreSqlTestGate.ExpectedG05Tests)
            .Concat(PostgreSqlTestGate.ExpectedP01Tests).Concat(PostgreSqlTestGate.ExpectedP02Tests).Order(StringComparer.Ordinal), actual);
        string[] g05 = typeof(PostgreSqlTestGate).Assembly.GetTypes()
            .Where(static type => type.GetCustomAttributes<TraitAttribute>().Any(static trait => trait.Name == "ExtractionLot" && trait.Value == "G05"))
            .SelectMany(static type => type.GetMethods().Where(static method => method.GetCustomAttribute<FactAttribute>() is not null)
                .Select(method => type.FullName + "." + method.Name)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(PostgreSqlTestGate.ExpectedG05Tests.Order(StringComparer.Ordinal), g05);
        string[] p01 = typeof(PostgreSqlTestGate).Assembly.GetTypes()
            .Where(static type => type.GetCustomAttributes<TraitAttribute>().Any(static trait => trait.Name == "ExtractionLot" && trait.Value == "P01"))
            .SelectMany(static type => type.GetMethods().Where(static method => method.GetCustomAttribute<FactAttribute>() is not null)
                .Select(method => type.FullName + "." + method.Name)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(PostgreSqlTestGate.ExpectedP01Tests.Order(StringComparer.Ordinal), p01);
        string[] p02 = typeof(PostgreSqlTestGate).Assembly.GetTypes()
            .Where(static type => type.GetCustomAttributes<TraitAttribute>().Any(static trait => trait.Name == "ExtractionLot" && trait.Value == "P02"))
            .SelectMany(static type => type.GetMethods().Where(static method => method.GetCustomAttribute<FactAttribute>() is not null)
                .Select(method => type.FullName + "." + method.Name)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(PostgreSqlTestGate.ExpectedP02Tests.Order(StringComparer.Ordinal), p02);
        PostgreSqlTestGate.ValidateInventories(actual.Except(g05).Except(p01).Except(p02), g05, p01, p02, actual);
        Assert.All(actual, name =>
        {
            int separator = name.LastIndexOf('.');
            MethodInfo method = typeof(PostgreSqlTestGate).Assembly.GetType(name[..separator])!.GetMethod(name[(separator + 1)..])!;
            FactAttribute fact = method.GetCustomAttribute<FactAttribute>()!;
            Assert.Equal("IsConfiguredOrRequired", fact.SkipUnless);
            Assert.NotNull(fact.Skip);
        });
    }

    [Theory]
    [InlineData("missing-p01")]
    [InlineData("duplicate-p01")]
    [InlineData("unexpected-p01")]
    [InlineData("missing-p02")]
    [InlineData("duplicate-p02")]
    [InlineData("unexpected-p02")]
    [InlineData("cross-p02-g04")]
    [InlineData("cross-p02-g05")]
    [InlineData("cross-p02-p01")]
    [InlineData("wrong-p02-partition")]
    [InlineData("missing-p02-discovery")]
    [InlineData("duplicate-p02-discovery")]
    [InlineData("unexpected-p02-discovery")]
    [InlineData("wrong-partition")]
    [InlineData("cross-lot")]
    [InlineData("missing-global")]
    [InlineData("duplicate-global")]
    [InlineData("unexpected-global")]
    public void InvalidInventoriesAreRejected(string scenario)
    {
        List<string> g04 = PostgreSqlTestGate.ExpectedTests.ToList();
        List<string> g05 = PostgreSqlTestGate.ExpectedG05Tests.ToList();
        List<string> p01 = PostgreSqlTestGate.ExpectedP01Tests.ToList();
        List<string> p02 = PostgreSqlTestGate.ExpectedP02Tests.ToList();
        List<string> all = g04.Concat(g05).Concat(p01).Concat(p02).ToList();
        switch (scenario)
        {
            case "missing-p01": p01.RemoveAt(0); break;
            case "duplicate-p01": p01[1] = p01[0]; break;
            case "unexpected-p01": p01[0] = "unexpected"; break;
            case "missing-p02": p02.RemoveAt(0); break;
            case "duplicate-p02": p02[1] = p02[0]; break;
            case "unexpected-p02": p02[0] = "unexpected"; break;
            case "cross-p02-g04": p02[0] = g04[0]; break;
            case "cross-p02-g05": p02[0] = g05[0]; break;
            case "cross-p02-p01": p02[0] = p01[0]; break;
            case "wrong-p02-partition": (p02[0], g05[0]) = (g05[0], p02[0]); break;
            case "missing-p02-discovery": all.Remove(p02[0]); break;
            case "duplicate-p02-discovery": all.Add(p02[0]); break;
            case "unexpected-p02-discovery": all[all.IndexOf(p02[0])] = "unexpected"; break;
            case "wrong-partition": (g05[0], p01[0]) = (p01[0], g05[0]); break;
            case "cross-lot": p01[0] = g04[0]; break;
            case "missing-global": all.RemoveAt(0); break;
            case "duplicate-global": all[1] = all[0]; break;
            case "unexpected-global": all[0] = "unexpected"; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlTestGate.ValidateInventories(g04, g05, p01, p02, all));
        Assert.StartsWith("SF-TEST-PG-INVENTORY", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MetricsAllowOnlyG04P01AndP02WithoutInstrumentingG05OrP03()
    {
        Assert.All(PostgreSqlTestGate.ExpectedTests.Concat(PostgreSqlTestGate.ExpectedP01Tests).Concat(PostgreSqlTestGate.ExpectedP02Tests), test =>
            Assert.True(PostgreSqlTestMetrics.IsAllowedTestName(test[(test.LastIndexOf('.') + 1)..])));
        Assert.All(PostgreSqlTestGate.ExpectedG05Tests, test =>
            Assert.False(PostgreSqlTestMetrics.IsAllowedTestName(test[(test.LastIndexOf('.') + 1)..])));
        Assert.False(PostgreSqlTestMetrics.IsAllowedTestName("unregistered"));
        Assert.False(PostgreSqlTestMetrics.IsAllowedTestName("RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent"));
        Assert.False(PostgreSqlTestMetrics.IsAllowedTestName("RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites"));
        Assert.False(PostgreSqlTestMetrics.IsAllowedTestName("RealWebReadinessHasNoFalseFailuresUnderConcurrentTraffic"));
    }

    [Fact]
    public void MetricsRejectUnregisteredNamesAndUnknownPhases()
    {
        Assert.Throws<ArgumentException>(() => PostgreSqlTestMetrics.Test("unregistered"));
        Assert.Throws<ArgumentException>(() => PostgreSqlTestMetrics.Measure((PostgreSqlTestMetrics.Phase)42));
    }
}
