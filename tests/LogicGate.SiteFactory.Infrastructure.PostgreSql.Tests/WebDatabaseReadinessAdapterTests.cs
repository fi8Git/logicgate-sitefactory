using System.Diagnostics;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class WebDatabaseReadinessAdapterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("NOT_INITIALIZED", false, null)]
    [InlineData("READY", true, null)]
    [InlineData("BLOCKED", false, "SF-DB-STARTUP-004")]
    [InlineData("CANCELLED", null, null)]
    [InlineData("UNEXPECTED_ERROR", null, null)]
    public async Task RefreshEventsDescribeOnlyTheLocalOutcomeAndPreserveExceptions(string outcome, bool? expected, string? code)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        if (outcome != "NOT_INITIALIZED") await readiness.ValidateAsync(Token);
        if (outcome == "BLOCKED")
        {
            RuntimeDatabaseObservation observation = fixture.Observation("client");
            fixture.Reader.Read = _ => Task.FromResult(new RuntimeDatabaseObservation(DatabaseReadOutcome.Read,
                observation.Role, [observation.Identities[0] with { Environment = "Production" }], observation.Schemas, observation.Migrations));
        }
        InvalidOperationException failure = new("PRIVATE-PROVIDER-MATERIAL");
        if (outcome == "UNEXPECTED_ERROR") fixture.Reader.Read = _ => throw failure;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        if (outcome == "CANCELLED") cancellation.Cancel();
        using Activity activity = new("offline-readiness-proof");
        activity.Start();
        if (outcome == "CANCELLED") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness.RefreshAsync(cancellation.Token));
        else if (outcome == "UNEXPECTED_ERROR") Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.RefreshAsync(Token)));
        else Assert.Equal(expected, await readiness.RefreshAsync(Token));
        ActivityEvent item = Assert.Single(activity.Events);
        Assert.Equal("sitefactory.readiness.refresh.completed", item.Name);
        Dictionary<string, object?> tags = item.Tags.ToDictionary();
        Assert.Equal(["diagnosticCode", "endTicks", "outcome", "result", "startTicks"], tags.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(outcome, tags["outcome"]);
        Assert.Equal(expected, tags["result"]);
        Assert.Equal(code, tags["diagnosticCode"]);
        Assert.True((long)tags["startTicks"]! > 0 && (long)tags["endTicks"]! >= (long)tags["startTicks"]!);
        Assert.DoesNotContain("PRIVATE-PROVIDER-MATERIAL", System.Text.Json.JsonSerializer.Serialize(tags), StringComparison.Ordinal);
        if (outcome == "BLOCKED") Assert.Equal(2, readiness.CurrentReport!.Results.Count); // The report is not reduced to its selected code.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveTelemetryDoesNotChangeTheRefreshOrAllocateEvents(bool unsampledActivity)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(Token);
        Activity? previous = Activity.Current;
        using Activity activity = new("unsampled-readiness-proof");
        try
        {
            Activity.Current = null;
            if (unsampledActivity) { activity.Start(); activity.IsAllDataRequested = false; }
            Assert.True(await readiness.RefreshAsync(Token));
            fixture.Reader.Read = _ => Task.FromResult(fixture.Observation("unavailable"));
            Assert.False(await readiness.RefreshAsync(Token));
            Assert.Empty(activity.Events);
            Assert.Equal(3, fixture.Reader.Calls);
        }
        finally
        {
            activity.Stop();
            Activity.Current = previous;
        }
    }

    [Fact]
    public async Task InitialStateIsNotReadyAndRefreshBeforeInitializationDoesNotAccessTheReader()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        Assert.False(readiness.ConfigurationValidated);
        Assert.Null(readiness.CurrentReport);
        Assert.False(readiness.IsReady);
        Assert.False(await readiness.RefreshAsync(Token));
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Reader.Calls);
    }

    [Fact]
    public async Task LocalPrerequisitesCompleteBeforeReaderConstructionAndTheInitialProbe()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        TaskCompletionSource entered = ReadinessFixture.Signal();
        TaskCompletionSource release = ReadinessFixture.Signal();
        fixture.Local.Check = async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return fixture.Local.Success();
        };
        using WebDatabaseReadiness readiness = fixture.Web();
        Task<RuntimeStartupReport> startup = readiness.ValidateAsync(Token);
        await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.False(readiness.ConfigurationValidated);
        Assert.Null(readiness.CurrentReport);
        Assert.Equal(0, fixture.FactoryCalls);
        release.SetResult();
        RuntimeStartupReport report = await startup.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.True(report.IsReady);
        Assert.True(readiness.IsReady);
        Assert.Equal(1, fixture.FactoryCalls);
        Assert.Equal(1, fixture.Reader.Calls);
        Assert.Equal(["startup.configuration.schema", "startup.test.local", "startup.database.compatibility"],
            report.Results.Select(static result => result.ValidationId.Value));
        Assert.Single(readiness.CurrentReport!.Results);
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)readiness.CurrentReport.Results).Clear());
    }

    [Theory]
    [InlineData("local")]
    [InlineData("binding")]
    public async Task LocalBlockersPreventEveryDatabaseAccess(string failure)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        if (failure == "local") fixture.Local.Check = _ => Task.FromResult(fixture.Local.Failure());
        else fixture.Configuration["SiteFactory:Runtime:ClientSlug"] = null;
        using WebDatabaseReadiness readiness = fixture.Web();
        Assert.False((await readiness.ValidateAsync(Token)).IsReady);
        Assert.False(readiness.ConfigurationValidated);
        Assert.Null(readiness.CurrentReport);
        Assert.False(readiness.IsReady);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" role ")]
    [InlineData("bad\nrole")]
    [InlineData("too-long")]
    public async Task InvalidExpectedRoleIsRefusedBeforeReaderConstructionWithoutEchoingItsValue(string? role)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        fixture.Configuration[WebDatabaseReadiness.ExpectedRoleKey] = role == "too-long" ? new string('x', 64) : role;
        using WebDatabaseReadiness readiness = fixture.Web();
        RuntimeStartupReport report = await readiness.ValidateAsync(Token);
        StartupValidationResult error = Assert.Single(report.Results, static result => result.Severity == StartupValidationSeverity.BlockingFailure);
        Assert.Equal("SF-CONFIG-STARTUP-003", error.ErrorCode);
        Assert.Equal("PostgreSql.ExpectedRole", error.PropertyPath);
        Assert.False(readiness.ConfigurationValidated);
        Assert.Null(readiness.CurrentReport);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Reader.Calls);
        Assert.DoesNotContain("bad\nrole", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebKeepsItsHistoricalCharacterLimitRatherThanTheWorkerUtf8Limit()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        string role = new('é', 63);
        fixture.Configuration[WebDatabaseReadiness.ExpectedRoleKey] = role;
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible(role));
        using WebDatabaseReadiness readiness = fixture.Web();
        Assert.True((await readiness.ValidateAsync(Token)).IsReady);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData("unavailable", "SF-DB-STARTUP-001")]
    [InlineData("client", "SF-DB-STARTUP-004")]
    [InlineData("environment", "SF-DB-STARTUP-005")]
    [InlineData("schema", "SF-SCHEMA-STARTUP-009")]
    [InlineData("role", "SF-DB-STARTUP-007")]
    [InlineData("privileges", "SF-DB-STARTUP-008")]
    public async Task InitialDatabaseBlockersDoNotInvalidateSuccessfulLocalConfiguration(string failure, string code)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        fixture.Reader.Read = _ => Task.FromResult(fixture.Observation(failure));
        using WebDatabaseReadiness readiness = fixture.Web();
        RuntimeStartupReport report = await readiness.ValidateAsync(Token);
        Assert.False(report.IsReady);
        Assert.True(readiness.ConfigurationValidated);
        Assert.False(readiness.IsReady);
        Assert.Equal(code, Assert.Single(readiness.CurrentReport!.Results).ErrorCode);
        Assert.Equal(code, Assert.Single(report.Results, static result => result.Severity == StartupValidationSeverity.BlockingFailure).ErrorCode);
    }

    [Fact]
    public async Task EachVerificationCanMoveFromCompatibleToIncompatibleAndBackWithoutCachingASuccess()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        RuntimeStartupReport startup = await readiness.ValidateAsync(Token);
        RuntimeStartupReport previous = readiness.CurrentReport!;
        fixture.Reader.Read = _ => Task.FromResult(fixture.Observation("privileges"));
        Assert.False(await readiness.RefreshAsync(Token));
        Assert.False(readiness.IsReady);
        Assert.True(previous.IsReady);
        Assert.True(startup.IsReady);
        Assert.NotSame(previous, readiness.CurrentReport);
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible());
        Assert.True(await readiness.RefreshAsync(Token));
        Assert.True(readiness.IsReady);
        Assert.Equal(3, fixture.Reader.Calls);
        Assert.Equal(1, fixture.FactoryCalls);
    }

    [Fact]
    public async Task SerializedRefreshKeepsACompleteReportAndEachCallerRetainsItsOwnResult()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(Token);
        RuntimeStartupReport previous = readiness.CurrentReport!;
        TaskCompletionSource firstEntered = ReadinessFixture.Signal(), firstRelease = ReadinessFixture.Signal();
        TaskCompletionSource secondEntered = ReadinessFixture.Signal(), secondRelease = ReadinessFixture.Signal();
        int sequence = 0;
        fixture.Reader.Read = async token =>
        {
            int index = Interlocked.Increment(ref sequence);
            (index == 1 ? firstEntered : secondEntered).SetResult();
            await (index == 1 ? firstRelease : secondRelease).Task.WaitAsync(token).ConfigureAwait(false);
            return index == 1 ? fixture.Compatible() : fixture.Observation("unavailable");
        };
        Task<bool> first = readiness.RefreshAsync(Token);
        await firstEntered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Task<bool> second = readiness.RefreshAsync(Token);
        Assert.Equal(1, sequence);
        Assert.Same(previous, readiness.CurrentReport);
        firstRelease.SetResult();
        await secondEntered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.True(await first.WaitAsync(ReadinessFixture.TestDeadline, Token));
        Assert.True(readiness.IsReady);
        Assert.True(readiness.CurrentReport!.IsReady);
        secondRelease.SetResult();
        Assert.False(await second.WaitAsync(ReadinessFixture.TestDeadline, Token));
        Assert.False(readiness.IsReady);
        Assert.True(await first); // B has already published an incompatible report; A still owns its success.
        Assert.Equal(3, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondInitializationIsRejectedBeforeTouchingASuspendedFirstCall(bool suspendInReader)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        TaskCompletionSource entered = ReadinessFixture.Signal(), release = ReadinessFixture.Signal();
        if (suspendInReader)
            fixture.Reader.Read = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return fixture.Compatible(); };
        else
            fixture.Local.Check = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return fixture.Local.Success(); };
        using WebDatabaseReadiness readiness = fixture.Web();
        Task<RuntimeStartupReport> first = readiness.ValidateAsync(Token);
        await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        bool configurationValidated = readiness.ConfigurationValidated;
        int localCalls = fixture.Local.Calls, factoryCalls = fixture.FactoryCalls, readerCalls = fixture.Reader.Calls;
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.ValidateAsync(Token));
        Assert.Equal("Web database startup validation can only run once per instance.", error.Message);
        Assert.Equal(configurationValidated, readiness.ConfigurationValidated);
        Assert.Null(readiness.CurrentReport);
        Assert.Equal(localCalls, fixture.Local.Calls);
        Assert.Equal(factoryCalls, fixture.FactoryCalls);
        Assert.Equal(readerCalls, fixture.Reader.Calls);
        release.SetResult();
        Assert.True((await first.WaitAsync(ReadinessFixture.TestDeadline, Token)).IsReady);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("local-failure")]
    [InlineData("db-failure")]
    [InlineData("cancelled")]
    public async Task InitializationCannotBeRetriedAndNeverErasesTheFirstCallsState(string outcome)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        if (outcome == "local-failure") fixture.Local.Check = _ => Task.FromResult(fixture.Local.Failure());
        if (outcome == "db-failure") fixture.Reader.Read = _ => Task.FromResult(fixture.Observation("unavailable"));
        using CancellationTokenSource cancelled = new();
        if (outcome == "cancelled") cancelled.Cancel();
        using WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(outcome == "cancelled" ? cancelled.Token : Token);
        RuntimeStartupReport? previous = readiness.CurrentReport;
        bool configurationValidated = readiness.ConfigurationValidated, wasReady = readiness.IsReady;
        int calls = fixture.Local.Calls;
        await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.ValidateAsync(Token));
        Assert.Same(previous, readiness.CurrentReport);
        Assert.Equal(configurationValidated, readiness.ConfigurationValidated);
        Assert.Equal(wasReady, readiness.IsReady);
        Assert.Equal(calls, fixture.Local.Calls);
    }

    [Fact]
    public async Task StartupUsesItsLocalInitialReportEvenWhenRefreshPublishesBeforeFinalAggregation()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        TaskCompletionSource entered = ReadinessFixture.Signal(), release = ReadinessFixture.Signal();
        fixture.Reader.Read = async token => { entered.SetResult(); await release.Task.WaitAsync(token).ConfigureAwait(false); return fixture.Compatible(); };
        using WebDatabaseReadiness readiness = fixture.Web();
        PausedAggregation pause = new();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task<RuntimeStartupReport> startup;
        try
        {
            SynchronizationContext.SetSynchronizationContext(pause);
            startup = readiness.ValidateAsync(Token);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }

        await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.True(readiness.ConfigurationValidated);
        Assert.Null(readiness.CurrentReport);
        release.SetResult();
        Action aggregate = await pause.Posted.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        try
        {
            Assert.False(startup.IsCompleted); // A is published; its final startup aggregation is held, not guessed by timing.
            Assert.True(readiness.CurrentReport!.IsReady);
            fixture.Reader.Read = _ => Task.FromResult(fixture.Observation("unavailable"));
            Assert.False(await readiness.RefreshAsync(Token));
            Assert.Equal("SF-DB-STARTUP-001", Assert.Single(readiness.CurrentReport!.Results).ErrorCode);
        }
        finally { aggregate(); }
        RuntimeStartupReport result = await startup.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.True(result.IsReady);
        Assert.Null(Assert.Single(result.Results, static item => item.ValidationId == DatabaseCompatibilityProbe.Id).ErrorCode);
        Assert.False(readiness.IsReady);
        Assert.Equal(2, fixture.Reader.Calls); // Exactly A and B; no corrective second startup probe.
    }

    [Fact]
    public async Task SemaphoreQueueTimeoutReturnsFalseWithoutPublishingOrCallingTheReader()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(Token);
        RuntimeStartupReport previous = readiness.CurrentReport!;
        TaskCompletionSource entered = ReadinessFixture.Signal(), release = ReadinessFixture.Signal();
        fixture.Reader.Read = async token => { entered.SetResult(); await release.Task.WaitAsync(token).ConfigureAwait(false); return fixture.Compatible(); };
        Task<bool> active = readiness.RefreshAsync(Token);
        await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        try
        {
            Assert.False(await readiness.RefreshAsync(Token).WaitAsync(ReadinessFixture.TestDeadline, Token));
            Assert.Same(previous, readiness.CurrentReport);
            Assert.True(readiness.IsReady);
            Assert.Equal(2, fixture.Reader.Calls);
            Assert.Equal(TimeSpan.FromSeconds(5), PostgreSqlDatabaseReader.MaximumDuration);
        }
        finally { release.SetResult(); }
        Assert.True(await active.WaitAsync(ReadinessFixture.TestDeadline, Token));
    }

    [Theory]
    [InlineData("before")]
    [InlineData("reader")]
    [InlineData("queue")]
    public async Task CancelledRefreshNeverPublishesOrErasesTheLastCompletedReport(string phase)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        fixture.Reader.Read = _ => Task.FromResult(fixture.Observation("unavailable"));
        using WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(Token);
        RuntimeStartupReport previous = readiness.CurrentReport!;
        using CancellationTokenSource cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using CancellationTokenSource activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = ReadinessFixture.Signal();
        TaskCompletionSource release = ReadinessFixture.Signal();
        fixture.Reader.Read = async token => { entered.SetResult(); await release.Task.WaitAsync(token).ConfigureAwait(false); return fixture.Compatible(); };
        Task<bool>? active = null;
        if (phase == "queue")
        {
            active = readiness.RefreshAsync(activeCancellation.Token);
            await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        }
        if (phase == "before") cancelled.Cancel();
        Task<bool> check = readiness.RefreshAsync(cancelled.Token);
        if (phase == "reader") await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
        Assert.Same(previous, readiness.CurrentReport);
        Assert.False(readiness.IsReady);
        if (active is not null)
        {
            activeCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
            Assert.Same(previous, readiness.CurrentReport);
        }
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible());
        Assert.True(await readiness.RefreshAsync(Token));
    }

    [Fact]
    public async Task UnexpectedErrorsPreserveTheReportPropagateAndReleaseTheSemaphore()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(Token);
        RuntimeStartupReport previous = readiness.CurrentReport!;
        InvalidOperationException error = new("Controlled programming failure.");
        fixture.Reader.Read = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.RefreshAsync(Token)));
        Assert.Same(previous, readiness.CurrentReport);
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible());
        Assert.True(await readiness.RefreshAsync(Token));
    }

    [Fact]
    public async Task DisposingTheAdapterDisposesItsOwnedSemaphore()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        WebDatabaseReadiness readiness = fixture.Web();
        await readiness.ValidateAsync(Token);
        readiness.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => readiness.RefreshAsync(Token));
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Fact]
    public async Task WebRegistrationDefersTheReaderSharesAllAliasesAndUsesOnlyTheExistingLifecycle()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using PostgreSqlSecretFixture secrets = new();
        using ServiceProvider provider = fixture.Register(secrets, web: true);
        WebDatabaseReadiness readiness = provider.GetRequiredService<WebDatabaseReadiness>();
        Assert.Same(readiness, provider.GetRequiredService<IRuntimeStartupValidator>());
        Assert.Same(readiness, provider.GetRequiredService<IRuntimeStartupReadiness>());
        Assert.Same(readiness, provider.GetRequiredService<IRuntimeReadinessRefresh>());
        Assert.Equal(0, fixture.FactoryCalls);
        RuntimeStartupService startup = provider.GetRequiredService<RuntimeStartupService>();
        Assert.Null(startup.Report);
        Assert.False(startup.ConfigurationIsValidated);
        await startup.StartingAsync(Token); // Direct offline lifecycle callback, no host/server is constructed or started.
        Assert.True(startup.ConfigurationIsValidated);
        Assert.True(readiness.IsReady);
        Assert.False(startup.IsReady); // Host-started remains a separate condition.
        Assert.Equal(1, fixture.FactoryCalls);
        Assert.Same(provider.GetRequiredService<IRuntimeDatabaseReader>(), provider.GetRequiredService<IRuntimeDatabaseReader>());
        Assert.Same(startup, Assert.Single(provider.GetServices<IHostedService>()));
        secrets.AssertRedacted(string.Join('\n', startup.Report!.Results));
        provider.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => readiness.RefreshAsync(Token));
    }

    [Fact]
    public async Task DedicatedCredentialsAreNotTheExpectedRoleOrExposedByDiagnostics()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Web);
        using PostgreSqlSecretFixture secrets = new();
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible("wrong_observed_session"));
        using ServiceProvider provider = fixture.Register(secrets, web: true);
        RuntimeStartupService startup = provider.GetRequiredService<RuntimeStartupService>();
        await startup.StartingAsync(Token); // DB blocker is not a local configuration failure for Web.
        Assert.True(startup.ConfigurationIsValidated);
        Assert.False(provider.GetRequiredService<IRuntimeStartupReadiness>().IsReady);
        Assert.Equal("SF-DB-STARTUP-007", Assert.Single(startup.Report!.Results, static item => item.Severity == StartupValidationSeverity.BlockingFailure).ErrorCode);
        secrets.AssertRedacted(string.Join('\n', startup.Report.Results));
        Assert.DoesNotContain("wrong_observed_session", string.Join('\n', startup.Report.Results), StringComparison.Ordinal);
    }

    private sealed class PausedAggregation : SynchronizationContext
    {
        public TaskCompletionSource<Action> Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void Post(SendOrPostCallback callback, object? state) => Posted.SetResult(() => callback(state));
    }
}

// Shared offline doubles only; no generated assembly, test host, SQL or permanent template dependency.
internal sealed class ReadinessFixture : IDisposable
{
    public static TimeSpan TestDeadline => TimeSpan.FromSeconds(20);
    public ConfigurationManager Configuration { get; } = new();
    public RuntimeConfigurationStartupValidation Runtime { get; }
    public ControlledLocalValidation Local { get; } = new();
    public ControlledDatabaseReader Reader { get; } = new();
    public string ExpectedRole { get; }
    public int FactoryCalls { get; private set; }

    public ReadinessFixture(RuntimeComponent component)
    {
        Configuration["environment"] = "Test";
        Configuration["SiteFactory:Runtime:ClientSlug"] = "example-client";
        Configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://example.invalid";
        ExpectedRole = component == RuntimeComponent.Web ? "expected_web" : "expected_worker";
        Configuration[component == RuntimeComponent.Web ? WebDatabaseReadiness.ExpectedRoleKey : WorkerDatabaseStartupValidator.ExpectedRoleKey] = ExpectedRole;
        Runtime = new(Configuration, "Test", component);
        Reader.Read = _ => Task.FromResult(Compatible());
    }

    public WebDatabaseReadiness Web() => new([Local, Runtime], Runtime, Configuration, CreateReader);
    public WorkerDatabaseStartupValidator Worker() => new([Local, Runtime], Runtime, Configuration, CreateReader);

    private ControlledDatabaseReader CreateReader()
    {
        Assert.NotNull(Runtime.Configuration);
        Assert.Equal(1, Local.Calls);
        FactoryCalls++;
        return Reader;
    }

    public RuntimeDatabaseObservation Compatible(string? role = null) => Observation("", role);

    public RuntimeDatabaseObservation Observation(string failure, string? role = null)
    {
        RuntimeIdentity identity = Runtime.Configuration!.Identity;
        if (failure == "unavailable") return new(DatabaseReadOutcome.Unavailable);
        string session = failure == "role" ? "unexpected_role" : role ?? ExpectedRole;
        return new(DatabaseReadOutcome.Read,
            new(session, session, true, false, "pg_catalog, sitefactory_runtime",
                failure == "privileges" ? DatabaseRolePrivileges.SchemaCreate : DatabaseRolePrivileges.None),
            [new(Guid.Parse("13a5d8ef-7341-49d9-80b3-3935844a846a"), failure == "client" ? "other-client" : identity.ClientSlug,
                failure == "environment" ? "Production" : identity.Environment.ToString())],
            [new(DatabaseCompatibilityProbe.SchemaFamily, failure == "schema" ? "999" : DatabaseCompatibilityProbe.SchemaVersion,
                DatabaseCompatibilityProbe.InitialMigration, "stable")], [DatabaseCompatibilityProbe.InitialMigration]);
    }

    public ServiceProvider Register(PostgreSqlSecretFixture secrets, bool web)
    {
        PostgreSqlComponent component = web ? PostgreSqlComponent.Web : PostgreSqlComponent.Worker;
        Configuration[PostgreSqlConfigurationReader.ConnectionFileKey(component)] = secrets.PathFor(component);
        ServiceCollection services = new();
        services.AddSingleton<ILogger<RuntimeStartupService>>(NullLogger<RuntimeStartupService>.Instance);
        services.AddSingleton<IHostApplicationLifetime>(new OfflineLifetime());
        services.AddRuntimeStartupValidation(Configuration, "Test", web ? RuntimeComponent.Web : RuntimeComponent.Worker);
        services.AddSingleton(Runtime);
        services.AddSingleton<IStartupValidation>(Local);
        services.AddPostgreSqlStartupValidation(Configuration, component, "Test");
        int hostedBefore = services.Count(static descriptor => descriptor.ServiceType == typeof(IHostedService));
        if (web) Assert.Same(services, services.AddWebDatabaseReadiness(Configuration));
        else Assert.Same(services, services.AddWorkerDatabaseStartupValidation(Configuration));
        Assert.Equal(hostedBefore, services.Count(static descriptor => descriptor.ServiceType == typeof(IHostedService)));
        Assert.All(services, static descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
        Assert.DoesNotContain(services, static descriptor => descriptor.ServiceType == typeof(WorkerDatabaseStartupValidator));
        services.AddSingleton<IRuntimeDatabaseReader>(provider =>
        {
            RuntimeConfigurationStartupValidation runtime = provider.GetRequiredService<RuntimeConfigurationStartupValidation>();
            Assert.NotNull(runtime.Configuration);
            Assert.Equal(component, provider.GetRequiredService<PostgreSqlStartupValidation>().Configuration!.Component);
            return CreateReader();
        });
        return services.BuildServiceProvider();
    }

    public static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() => Configuration.Dispose();

    private sealed class OfflineLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => throw new InvalidOperationException("No host is started by G06 adapter tests.");
    }
}

internal sealed class ControlledLocalValidation : IStartupValidation
{
    public StartupValidationId Id { get; } = new("startup.test.local");
    public int Calls { get; private set; }
    public Func<CancellationToken, Task<IReadOnlyList<StartupValidationResult>>>? Check { get; set; }
    public IReadOnlyList<StartupValidationResult> Success() => [new(Id, StartupValidationSeverity.Success, null, "Local prerequisite is valid.")];
    public IReadOnlyList<StartupValidationResult> Failure() => [new(Id, StartupValidationSeverity.BlockingFailure, "SF-TEST-LOCAL", "Local prerequisite is invalid.")];
    public Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Check?.Invoke(cancellationToken) ?? Task.FromResult(Success());
    }
}

internal sealed class ControlledDatabaseReader : IRuntimeDatabaseReader
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public Func<CancellationToken, Task<RuntimeDatabaseObservation>> Read { get; set; } = null!;
    public Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return Read(cancellationToken);
    }
}
