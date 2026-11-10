using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class WorkerDatabaseStartupValidatorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LocalPrerequisitesCompleteBeforeTheWorkerFactoryAndProbe()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        TaskCompletionSource entered = ReadinessFixture.Signal(), release = ReadinessFixture.Signal();
        fixture.Local.Check = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return fixture.Local.Success(); };
        WorkerDatabaseStartupValidator validator = fixture.Worker();
        Task<RuntimeStartupReport> startup = validator.ValidateAsync(Token);
        await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Reader.Calls);
        release.SetResult();
        RuntimeStartupReport report = await startup.WaitAsync(ReadinessFixture.TestDeadline, Token);
        Assert.True(report.IsReady);
        Assert.Equal(["startup.configuration.schema", "startup.test.local", "startup.database.compatibility"],
            report.Results.Select(static result => result.ValidationId.Value));
        Assert.Equal(1, fixture.FactoryCalls);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("binding")]
    public async Task LocalBlockerDoesNotInvokeTheWorkerReader(string failure)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        if (failure == "local") fixture.Local.Check = _ => Task.FromResult(fixture.Local.Failure());
        else fixture.Configuration["SiteFactory:Runtime:ClientSlug"] = null;
        Assert.False((await fixture.Worker().ValidateAsync(Token)).IsReady);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" role ")]
    [InlineData("bad\nrole")]
    [InlineData("utf8-too-long")]
    public async Task InvalidExpectedWorkerRoleCannotFallbackToWebOrCredentials(string? role)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        fixture.Configuration[WorkerDatabaseStartupValidator.ExpectedRoleKey] = role == "utf8-too-long" ? new string('é', 32) : role;
        fixture.Configuration[WebDatabaseReadiness.ExpectedRoleKey] = "available_web_role";
        RuntimeStartupReport report = await fixture.Worker().ValidateAsync(Token);
        StartupValidationResult error = Assert.Single(report.Results, static result => result.Severity == StartupValidationSeverity.BlockingFailure);
        Assert.Equal("SF-CONFIG-STARTUP-003", error.ErrorCode);
        Assert.Equal("PostgreSql.ExpectedRole", error.PropertyPath);
        Assert.Equal(0, fixture.FactoryCalls);
        Assert.Equal(0, fixture.Reader.Calls);
        Assert.DoesNotContain("bad\nrole", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exactly63Utf8BytesAreAcceptedForTheWorkerExpectation()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        string role = new string('é', 31) + "a";
        fixture.Configuration[WorkerDatabaseStartupValidator.ExpectedRoleKey] = role;
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible(role));
        Assert.True((await fixture.Worker().ValidateAsync(Token)).IsReady);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData("unavailable", "SF-DB-STARTUP-001")]
    [InlineData("client", "SF-DB-STARTUP-004")]
    [InlineData("environment", "SF-DB-STARTUP-005")]
    [InlineData("schema", "SF-SCHEMA-STARTUP-009")]
    [InlineData("role", "SF-DB-STARTUP-007")]
    [InlineData("privileges", "SF-DB-STARTUP-008")]
    public async Task WorkerReturnsThePureProbesBlockingDiagnostic(string failure, string code)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        fixture.Reader.Read = _ => Task.FromResult(fixture.Observation(failure));
        RuntimeStartupReport report = await fixture.Worker().ValidateAsync(Token);
        Assert.False(report.IsReady);
        Assert.Equal(code, Assert.Single(report.Results, static result => result.Severity == StartupValidationSeverity.BlockingFailure).ErrorCode);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("local")]
    [InlineData("database")]
    public async Task CancellationCannotPublishAReadyWorkerReport(string phase)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        TaskCompletionSource entered = ReadinessFixture.Signal(), release = ReadinessFixture.Signal();
        using CancellationTokenSource cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        if (phase == "before") cancelled.Cancel();
        if (phase == "local") fixture.Local.Check = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return fixture.Local.Success(); };
        if (phase == "database") fixture.Reader.Read = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return fixture.Compatible(); };
        Task<RuntimeStartupReport> startup = fixture.Worker().ValidateAsync(cancelled.Token);
        if (phase != "before") await entered.Task.WaitAsync(ReadinessFixture.TestDeadline, Token);
        cancelled.Cancel();
        if (phase == "database") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup);
        else
        {
            RuntimeStartupReport report = await startup.WaitAsync(ReadinessFixture.TestDeadline, Token);
            Assert.True(report.Cancelled);
            Assert.False(report.IsReady);
            Assert.Equal("SF-STARTUP-005", report.Results[^1].ErrorCode);
            Assert.Equal(0, fixture.FactoryCalls);
        }
    }

    [Theory]
    [InlineData("local")]
    [InlineData("database")]
    public async Task ProgrammingErrorsArePropagatedWithoutAnInventedCompatibilityResult(string phase)
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        InvalidOperationException error = new("Controlled programming failure.");
        if (phase == "local") fixture.Local.Check = _ => throw error;
        else fixture.Reader.Read = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Worker().ValidateAsync(Token)));
        if (phase == "local") Assert.Equal(0, fixture.FactoryCalls);
    }

    [Fact]
    public async Task WorkerRegistrationIsDeferredSingletonWithNoSpecializedReadinessOrAdditionalHostedService()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        using PostgreSqlSecretFixture secrets = new();
        using ServiceProvider provider = fixture.Register(secrets, web: false);
        IRuntimeStartupValidator validator = provider.GetRequiredService<IRuntimeStartupValidator>();
        Assert.IsType<WorkerDatabaseStartupValidator>(validator);
        Assert.Same(validator, provider.GetRequiredService<IRuntimeStartupValidator>());
        Assert.Null(provider.GetService<WorkerDatabaseStartupValidator>());
        Assert.Null(provider.GetService<IRuntimeStartupReadiness>());
        Assert.Equal(0, fixture.FactoryCalls);
        RuntimeStartupService startup = provider.GetRequiredService<RuntimeStartupService>();
        await startup.StartingAsync(Token);
        Assert.True(startup.Report!.IsReady);
        Assert.Equal(1, fixture.FactoryCalls);
        Assert.Same(startup, Assert.Single(provider.GetServices<IHostedService>()));
        Assert.False(typeof(WorkerDatabaseStartupValidator).IsAssignableTo(typeof(IHostedService)));
        Assert.Null(typeof(WorkerDatabaseStartupValidator).GetMethod("RefreshAsync"));
        secrets.AssertRedacted(string.Join('\n', startup.Report.Results));
    }

    [Fact]
    public async Task AWorkerCredentialCannotReplaceItsIndependentExpectedRoleAndBlockersRemainFailFast()
    {
        using ReadinessFixture fixture = new(RuntimeComponent.Worker);
        using PostgreSqlSecretFixture secrets = new();
        fixture.Reader.Read = _ => Task.FromResult(fixture.Compatible("wrong_observed_session"));
        using ServiceProvider provider = fixture.Register(secrets, web: false);
        RuntimeStartupService startup = provider.GetRequiredService<RuntimeStartupService>();
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => startup.StartingAsync(Token));
        Assert.False(startup.Report!.IsReady);
        Assert.Equal("SF-DB-STARTUP-007", Assert.Single(startup.Report.Results, static item => item.Severity == StartupValidationSeverity.BlockingFailure).ErrorCode);
        secrets.AssertRedacted(error.ToString());
        Assert.DoesNotContain("wrong_observed_session", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReferenceReaderRegistrationIsOfflineAndUnavailableUntilItsComponentConfigurationIsValid(bool web)
    {
        using ReadinessFixture fixture = new(web ? RuntimeComponent.Web : RuntimeComponent.Worker);
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlComponent component = web ? PostgreSqlComponent.Web : PostgreSqlComponent.Worker;
        fixture.Configuration[PostgreSqlConfigurationReader.ConnectionFileKey(component)] = secrets.PathFor(component);
        ServiceCollection services = new();
        services.AddPostgreSqlStartupValidation(fixture.Configuration, component, "Test");
        if (web) services.AddWebDatabaseReadiness(fixture.Configuration);
        else services.AddWorkerDatabaseStartupValidation(fixture.Configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Null(provider.GetRequiredService<PostgreSqlStartupValidation>().Configuration);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IRuntimeDatabaseReader>());
        Assert.StartsWith("SF-STARTUP-008", error.Message, StringComparison.Ordinal);
        await provider.GetRequiredService<PostgreSqlStartupValidation>().ValidateAsync(Token);
        IRuntimeDatabaseReader reader = provider.GetRequiredService<IRuntimeDatabaseReader>();
        Assert.IsType<PostgreSqlDatabaseReader>(reader); // Constructor only. Never invoke ReadAsync or open a connection.
        Assert.Same(reader, provider.GetRequiredService<IRuntimeDatabaseReader>());
        secrets.AssertRedacted(error.ToString());
    }
}
