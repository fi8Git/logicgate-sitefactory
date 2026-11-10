using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P03")]
public sealed class PostgreSqlWorkerReadinessTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires explicitly configured dedicated PostgreSQL 18 with verified TLS.")]
    public async Task RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites()
    {
        await using RealPostgreSqlHostFixture fixture = await RealPostgreSqlHostFixture.CreateAsync(Token);
        string connection = fixture.FileFor(PostgreSqlTestLogin.Worker);
        int scenarios = 0;
        await fixture.WithoutMutationAsync(async () =>
        {
            await using TestHosts hosts = new();
            IHost host = hosts.CreateWorker(Arguments(connection));
            RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            Assert.False(startup.IsReady);
            Assert.IsType<RuntimeStartupService>(Assert.Single(host.Services.GetServices<IHostedService>()));
            Assert.Null(host.Services.GetService<IRuntimeReadinessRefresh>()); // No Worker HTTP or refresh.
            using CancellationTokenSource shutdown = CancellationTokenSource.CreateLinkedTokenSource(Token);
            await host.StartAsync(Token);
            Task idle = host.WaitForShutdownAsync(shutdown.Token);
            try
            {
                Assert.True(lifetime.ApplicationStarted.IsCancellationRequested);
                Assert.True(startup.IsReady);
                Assert.DoesNotContain(startup.Report!.Results, static result => result.ErrorCode is not null);
                Assert.IsType<PostgreSqlDatabaseReader>(host.Services.GetRequiredService<IRuntimeDatabaseReader>());
                Assert.False(idle.IsCompleted);
                await fixture.AssertAllRedactedAsync(string.Join("\n", hosts.LogMessages)
                    + string.Join("\n", startup.Report.Results), Token);
            }
            finally
            {
                await shutdown.CancelAsync();
                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
                await idle.WaitAsync(cleanup.Token);
            }
            Assert.True(idle.IsCompletedSuccessfully);
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
            Assert.False(startup.IsReady);
        }, Token);
        Complete("baseline-idle-shutdown");
        await FailureAsync("wrong-client", connection, ["SF-DB-STARTUP-004"], "wrong-client");
        await FailureAsync("wrong-environment", connection, ["SF-DB-STARTUP-005"], environment: "Development");
        await FailureAsync("web-credential", fixture.FileFor(PostgreSqlTestLogin.Web), ["SF-DB-STARTUP-007"]);
        await FailureAsync("migrator-credential", fixture.FileFor(PostgreSqlTestLogin.Migrator), ["SF-DB-STARTUP-007", "SF-DB-STARTUP-008"]);
        await FailureAsync("missing-worker-reference-no-fallback", "", ["SF-SECRET-001"]);
        await fixture.MissingMetadataAsync(code => FailureAsync("missing-" + code, connection, [code]), Token);
        await fixture.MutationAsync("UPDATE sitefactory_runtime.schema_state SET current_version = '2'",
            "UPDATE sitefactory_runtime.schema_state SET current_version = '1'",
            () => FailureAsync("unsupported-version", connection, ["SF-SCHEMA-STARTUP-009"]), Token);
        await fixture.MutationAsync($"ALTER ROLE \"{fixture.Roles.Baseline.Worker}\" CREATEROLE",
            $"ALTER ROLE \"{fixture.Roles.Baseline.Worker}\" NOCREATEROLE",
            () => FailureAsync("createrole", connection, ["SF-DB-STARTUP-008"]), Token);
        string owner = await fixture.ChangedConnectionAsync(PostgreSqlTestLogin.Worker, "owner",
            settings => settings.Username = fixture.Roles.Baseline.Owner, Token);
        await FailureAsync("owner-nologin", owner, ["SF-DB-STARTUP-001"]);
        string invalid = await fixture.ChangedConnectionAsync(PostgreSqlTestLogin.Worker, "invalid-password",
            settings => settings.Password = RealPostgreSqlHostFixture.InvalidPassword(), Token);
        await FailureAsync("invalid-password", invalid, ["SF-DB-STARTUP-001"]);
        using TcpListener silent = new(IPAddress.Loopback, 0);
        silent.Start();
        string down = await fixture.ChangedConnectionAsync(PostgreSqlTestLogin.Worker, "silent-port", settings =>
        {
            settings.Port = ((IPEndPoint)silent.LocalEndpoint).Port;
            settings.Timeout = 999;
            settings.CommandTimeout = 0;
        }, Token);
        Stopwatch elapsed = Stopwatch.StartNew();
        await FailureAsync("silent-port", down, ["SF-DB-STARTUP-001"]);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), "Worker database validation exceeded its historical bounded deadline.");
        await fixture.WithoutMutationAsync(async () =>
        {
            await using TestHosts hosts = new();
            IHost host = hosts.CreateWorker(Arguments(down));
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
            elapsed.Restart();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.StartAsync(cancellation.Token));
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), "Worker startup did not honor cancellation promptly.");
            AssertBlocked(host);
            await fixture.AssertAllRedactedAsync(string.Join("\n", hosts.LogMessages), Token);
        }, Token);
        Complete("startup-cancellation");
        Assert.Equal(15, scenarios);
        await fixture.AssertSessionsClosedAsync(Token);

        string[] Arguments(string file, string client = "example-client", string environment = "Test") =>
            ["--environment", environment, "--SiteFactory:Runtime:ClientSlug", client,
                "--SiteFactory:Runtime:PublicBaseUrl", "https://example.invalid",
                "--SITEFACTORY_WORKER_CONNECTION_FILE", file, "--" + WorkerDatabaseStartupValidator.ExpectedRoleKey, fixture.Roles.Baseline.Worker,
                "--SITEFACTORY_WEB_CONNECTION_FILE", fixture.FileFor(PostgreSqlTestLogin.Web),
                "--SITEFACTORY_MIGRATOR_CONNECTION_FILE", fixture.FileFor(PostgreSqlTestLogin.Migrator)];

        async Task FailureAsync(string name, string file, string[] codes, string client = "example-client", string environment = "Test")
        {
            await fixture.WithoutMutationAsync(async () =>
            {
                await using TestHosts hosts = new();
                IHost host = hosts.CreateWorker(Arguments(file, client, environment));
                RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
                AssertBlocked(host);
                Assert.Equal(codes, error.Report.Results.Select(static result => result.ErrorCode).OfType<string>().ToArray());
                await fixture.AssertAllRedactedAsync(error.ToString() + string.Join("\n", hosts.LogMessages)
                    + string.Join("\n", host.Services.GetRequiredService<RuntimeStartupService>().Report!.Results), Token);
            }, Token);
            Complete(name);
        }
        void Complete(string name)
        {
            scenarios++;
            output.WriteLine("P03 Worker scenario {0}: {1} Passed", scenarios, name);
        }
    }

    private static void AssertBlocked(IHost host)
    {
        Assert.False(host.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
    }
}
