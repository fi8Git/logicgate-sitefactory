using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

public sealed class WorkerDatabaseReadinessTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" role ")]
    [InlineData("bad\nrole")]
    [InlineData("éééééééééééééééééééééééééééééééé")]
    public async Task ExpectedWorkerRoleIsRequiredBeforeNetworkIo(string login)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        IHost host = Create(hosts, secrets, reader, ["--" + WorkerDatabaseStartupValidator.ExpectedRoleKey, login]);
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        Assert.Contains(error.Report.Results, static result => result.PropertyPath == "PostgreSql.ExpectedRole"
            && result.ErrorCode == "SF-CONFIG-STARTUP-003");
        AssertBlocked(host);
        Assert.Equal(0, reader.Calls);
        secrets.AssertRedacted(error.ToString());
    }

    [Fact]
    public async Task StructuralFailureNeverInvokesTheDatabasePort()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        IHost host = Create(hosts, secrets, reader, ["--SiteFactory:Runtime:ClientSlug", ""]);
        await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        AssertBlocked(host);
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData("unavailable", "SF-DB-STARTUP-001")]
    [InlineData("client", "SF-DB-STARTUP-004")]
    [InlineData("environment", "SF-DB-STARTUP-005")]
    [InlineData("schema", "SF-SCHEMA-STARTUP-009")]
    [InlineData("role", "SF-DB-STARTUP-007")]
    [InlineData("privileges", "SF-DB-STARTUP-008")]
    public async Task DatabaseBlockerPreventsEveryHostedServiceFromStarting(string failure, string code)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new() { Failure = failure };
        Sentinel service = new(); // Test-only observer, never a generated production job.
        IHost host = Create(hosts, secrets, reader, sentinel: service);
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        Assert.Equal(code, Assert.Single(error.Report.Results, static result => result.ErrorCode is not null).ErrorCode);
        AssertBlocked(host);
        Assert.False(service.Started);
        Assert.Equal(1, reader.Calls); // No retry, alternate role or alternate credential.
        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<PostgreSqlRuntimeConfiguration>());
        secrets.AssertRedacted(error.ToString());
    }

    [Fact]
    public async Task ValidStartupIsReadyAndIdleUntilGracefulCancellation()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        IHost host = Create(hosts, secrets, reader);
        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration signal = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using CancellationTokenSource shutdown = CancellationTokenSource.CreateLinkedTokenSource(Token);
        Task running = host.RunAsync(shutdown.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            Assert.True(startup.IsReady);
            Assert.True(startup.Report!.IsReady);
            Assert.Contains(startup.Report.Results, static result => result.ValidationId == DatabaseCompatibilityProbe.Id
                && result.Severity == StartupValidationSeverity.Success);
            Assert.Equal(RuntimeComponent.Worker, reader.Identity.Component);
            Assert.Equal(PostgreSqlComponent.Worker, host.Services.GetRequiredService<PostgreSqlRuntimeConfiguration>().Component);
            Assert.Null(host.Services.GetService<WebDatabaseReadiness>());
            Assert.IsType<RuntimeStartupService>(Assert.Single(host.Services.GetServices<IHostedService>()));
            Assert.False(running.IsCompleted);
            Assert.Equal(1, reader.Calls);
        }
        finally
        {
            await shutdown.CancelAsync();
            await running.WaitAsync(TimeSpan.FromSeconds(10), Token);
        }
        Assert.True(running.IsCompletedSuccessfully);
        Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
        Assert.False(startup.IsReady);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task CancellationDuringDatabaseValidationCannotActivateReadinessOrWork()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new() { Block = true };
        Sentinel service = new();
        IHost host = Create(hosts, secrets, reader, sentinel: service);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        Task starting = host.StartAsync(cancellation.Token);
        try
        {
            await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            AssertBlocked(host);
            Assert.False(service.Started);
        }
        finally { await cancellation.CancelAsync(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        AssertBlocked(host);
        Assert.False(service.Started);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task MissingWorkerReferenceNeverFallsBackToAvailableWebOrMigratorSecrets()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        IHost host = Create(hosts, secrets, reader, ["--SITEFACTORY_WORKER_CONNECTION_FILE", "",
            "--SITEFACTORY_MIGRATOR_CONNECTION_FILE", secrets.PathFor(PostgreSqlComponent.Migrator)]);
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        Assert.Contains(error.Report.Results, static result => result.ErrorCode == "SF-SECRET-001");
        Assert.Equal(0, reader.Calls);
        AssertBlocked(host);
    }

    [Fact]
    public async Task ProgrammingErrorsAreNotConvertedIntoUserConfigurationFailures()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        InvalidOperationException failure = new("Test-only programming failure.");
        ControlledReader reader = new() { Error = failure };
        IHost host = Create(hosts, secrets, reader);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(Token)));
        AssertBlocked(host);
    }

    private static void AssertBlocked(IHost host)
    {
        Assert.False(host.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
    }

    private static IHost Create(TestHosts hosts, OfflineDatabaseFixture secrets, ControlledReader reader, string[]? extra = null, Sentinel? sentinel = null) =>
        hosts.CreateWorker([.. secrets.Arguments("--environment", "Test", "--SiteFactory:Runtime:ClientSlug", "example-client",
            "--SiteFactory:Runtime:PublicBaseUrl", "https://example.invalid"), .. extra ?? []], services =>
        {
            services.AddSingleton<IRuntimeDatabaseReader>(provider =>
            {
                reader.Identity = provider.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration!.Identity;
                return reader;
            });
            if (sentinel is not null) services.AddSingleton<IHostedService>(sentinel);
        });

    private sealed class Sentinel : IHostedService
    {
        public bool Started { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { Started = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ControlledReader : IRuntimeDatabaseReader
    {
        public RuntimeIdentity Identity { get; set; } = null!;
        public string? Failure { get; set; }
        public Exception? Error { get; set; }
        public bool Block { get; set; }
        public int Calls { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            Entered.TrySetResult();
            if (Block) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Error is not null) throw Error;
            RuntimeDatabaseObservation valid = OfflineDatabaseFixture.Compatible(Identity);
            return Failure switch
            {
                "unavailable" => new(DatabaseReadOutcome.Unavailable),
                "client" => new(DatabaseReadOutcome.Read, valid.Role, [valid.Identities[0] with { ClientSlug = "other-client" }], valid.Schemas, valid.Migrations),
                "environment" => new(DatabaseReadOutcome.Read, valid.Role, [valid.Identities[0] with { Environment = "Development" }], valid.Schemas, valid.Migrations),
                "schema" => new(DatabaseReadOutcome.Read, valid.Role, valid.Identities, [valid.Schemas[0] with { Version = "2" }], valid.Migrations),
                "role" => new(DatabaseReadOutcome.Read, valid.Role! with { SessionUser = "test_Web", CurrentUser = "test_Web" }, valid.Identities, valid.Schemas, valid.Migrations),
                "privileges" => new(DatabaseReadOutcome.Read, valid.Role! with { Privileges = DatabaseRolePrivileges.CreateRole }, valid.Identities, valid.Schemas, valid.Migrations),
                _ => valid,
            };
        }
    }
}
