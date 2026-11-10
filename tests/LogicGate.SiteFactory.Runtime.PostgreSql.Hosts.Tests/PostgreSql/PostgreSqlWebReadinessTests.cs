using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P03")]
public sealed class PostgreSqlWebReadinessTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires explicitly configured dedicated PostgreSQL 18 with verified TLS.")]
    public async Task RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent()
    {
        await using RealPostgreSqlHostFixture fixture = await RealPostgreSqlHostFixture.CreateAsync(Token);
        string web = fixture.FileFor(PostgreSqlTestLogin.Web);
        int scenarios = 0;
        await CheckAsync("baseline", web, ["PASS"]);
        await CheckAsync("wrong-client", web, ["SF-DB-STARTUP-004"], client: "wrong-client");
        await CheckAsync("wrong-environment", web, ["SF-DB-STARTUP-005"], environment: "Development");
        await CheckAsync("worker-credential", fixture.FileFor(PostgreSqlTestLogin.Worker), ["SF-DB-STARTUP-007"]);
        await CheckAsync("migrator-credential", fixture.FileFor(PostgreSqlTestLogin.Migrator), ["SF-DB-STARTUP-007", "SF-DB-STARTUP-008"]);
        await fixture.MissingMetadataAsync(code => CheckAsync("missing-" + code, web, [code]), Token);
        string invalid = await fixture.ChangedConnectionAsync(PostgreSqlTestLogin.Web, "invalid-password",
            settings => settings.Password = RealPostgreSqlHostFixture.InvalidPassword(), Token);
        await CheckAsync("invalid-password", invalid, ["SF-DB-STARTUP-001"]);
        await fixture.MutationAsync("UPDATE sitefactory_runtime.schema_state SET current_version = '2'",
            "UPDATE sitefactory_runtime.schema_state SET current_version = '1'",
            () => CheckAsync("unsupported-version", web, ["SF-SCHEMA-STARTUP-009"]), Token);
        await fixture.MutationAsync($"ALTER ROLE \"{fixture.Roles.Baseline.Web}\" CREATEDB",
            $"ALTER ROLE \"{fixture.Roles.Baseline.Web}\" NOCREATEDB",
            () => CheckAsync("createdb", web, ["SF-DB-STARTUP-008"]), Token);

        // The mutation happens after compatible startup, not before. Snapshot after setup mutation
        // is compared to the same setup state after the real refresh/gate, before restoring the role.
        string[]? driftState = null;
        try
        {
            Assert.Equal(["SF-DB-STARTUP-008"], await WebDatabaseReadinessTests.ExerciseRealAsync(web,
                fixture.Roles.Baseline.Web, "example-client", "Test", false, async () =>
                {
                    await fixture.AdminAsync($"ALTER ROLE \"{fixture.Roles.Baseline.Web}\" CREATEROLE", Token);
                    driftState = await fixture.SnapshotAsync(Token);
                }, Token));
            PostgreSqlHostSnapshot.AssertUnchanged(driftState!, await fixture.SnapshotAsync(Token));
            Complete("createrole-after-startup");
        }
        finally
        {
            using CancellationTokenSource restore = new(TimeSpan.FromSeconds(15));
            await fixture.AdminAsync($"ALTER ROLE \"{fixture.Roles.Baseline.Web}\" NOCREATEROLE", restore.Token);
        }

        using TcpListener silent = new(IPAddress.Loopback, 0);
        silent.Start();
        string down = await fixture.ChangedConnectionAsync(PostgreSqlTestLogin.Web, "silent-port", settings =>
        {
            settings.Port = ((IPEndPoint)silent.LocalEndpoint).Port;
            settings.Timeout = 999;
            settings.CommandTimeout = 0;
        }, Token);
        Stopwatch elapsed = Stopwatch.StartNew();
        await CheckAsync("silent-port", down, ["SF-DB-STARTUP-001"]);
        // Startup + ready + legacy health + application each has its original five-second reader budget.
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(25), "Web checks exceeded the historical total deadline.");
        await fixture.WithoutMutationAsync(async () =>
        {
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
            elapsed.Restart();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebDatabaseReadinessTests.ExerciseRealAsync(down,
                fixture.Roles.Baseline.Web, "example-client", "Test", false, null, cancellation.Token));
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), "Web startup did not honor cancellation promptly.");
        }, Token);
        Complete("startup-cancellation");
        await CheckAsync("fresh-host-recovery", web, ["PASS"]); // New TestHosts/host, not invented reload/recovery.
        Assert.Equal(15, scenarios);
        await fixture.AssertSessionsClosedAsync(Token);

        async Task CheckAsync(string name, string file, string[] codes, string client = "example-client", string environment = "Test")
        {
            await fixture.WithoutMutationAsync(async () => Assert.Equal(codes,
                await WebDatabaseReadinessTests.ExerciseRealAsync(file, fixture.Roles.Baseline.Web, client, environment,
                    codes.SequenceEqual(["PASS"]), null, Token)), Token);
            Complete(name);
        }
        void Complete(string name)
        {
            scenarios++;
            output.WriteLine("P03 Web scenario {0}: {1} Passed", scenarios, name);
        }
    }
}
