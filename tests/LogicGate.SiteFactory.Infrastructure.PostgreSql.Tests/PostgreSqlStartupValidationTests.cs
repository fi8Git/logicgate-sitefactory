using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class PostgreSqlStartupValidationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(PostgreSqlComponent.Web)]
    [InlineData(PostgreSqlComponent.Worker)]
    public async Task OnlyTheSelectedComponentsReferenceIsReadAndNoOtherReferenceIsAFallback(PostgreSqlComponent component)
    {
        using PostgreSqlSecretFixture secrets = new();
        using ConfigurationManager configuration = Configure(secrets);
        foreach (PostgreSqlComponent other in Enum.GetValues<PostgreSqlComponent>().Where(candidate => candidate != component))
            secrets.Write(other, "deliberately-invalid");
        PostgreSqlStartupValidation validation = new(configuration, component, "Test");
        Assert.Equal(StartupValidationSeverity.Success, Assert.Single(await validation.ValidateAsync(Token)).Severity);
        Assert.Equal(component, validation.Configuration!.Component);
        configuration[PostgreSqlConfigurationReader.ConnectionFileKey(component)] = null;
        Assert.Equal("SF-SECRET-001", Assert.Single(await validation.ValidateAsync(Token)).ErrorCode);
        Assert.Null(validation.Configuration); // No stale previously validated credential.
    }

    [Theory]
    [InlineData(PostgreSqlComponent.Web)]
    [InlineData(PostgreSqlComponent.Worker)]
    public async Task InvalidSelectedFileCannotFallbackToAnotherValidComponent(PostgreSqlComponent component)
    {
        using PostgreSqlSecretFixture secrets = new();
        using ConfigurationManager configuration = Configure(secrets);
        secrets.Write(component, "invalid=" + secrets.PasswordFor(component));
        PostgreSqlStartupValidation validation = new(configuration, component, "Test");
        StartupValidationResult result = Assert.Single(await validation.ValidateAsync(Token));
        Assert.Equal("SF-RUNTIME-CONFIG-004", result.ErrorCode);
        Assert.Equal(StartupValidationSeverity.BlockingFailure, result.Severity);
        Assert.Null(validation.Configuration);
        secrets.AssertRedacted(result.ToString());
    }

    [Theory]
    [InlineData("missing", "SF-SECRET-001")]
    [InlineData("unreadable", "SF-SECRET-004")]
    [InlineData("empty", "SF-SECRET-002")]
    [InlineData("invalid", "SF-RUNTIME-CONFIG-004")]
    [InlineData("environment", "SF-RUNTIME-CONFIG-007")]
    public async Task FailuresInvalidateThePreviousSnapshotAndRetainOnlySafeStructuredDiagnostics(string failure, string code)
    {
        using PostgreSqlSecretFixture secrets = new();
        using ConfigurationManager configuration = Configure(secrets);
        PostgreSqlStartupValidation validation = new(configuration, PostgreSqlComponent.Web, "Test");
        Assert.Null(Assert.Single(await validation.ValidateAsync(Token)).ErrorCode);
        Assert.NotNull(validation.Configuration);
        string key = PostgreSqlConfigurationReader.ConnectionFileKey(PostgreSqlComponent.Web);
        switch (failure)
        {
            case "missing": configuration[key] = null; break;
            case "unreadable": configuration[key] = Path.Combine(secrets.DirectoryPath, "absent"); break;
            case "empty": secrets.Write(PostgreSqlComponent.Web, ""); break;
            case "invalid": secrets.Write(PostgreSqlComponent.Web, "invalid=" + secrets.PasswordFor(PostgreSqlComponent.Web)); break;
            case "environment": validation = new(configuration, PostgreSqlComponent.Web, "undecided"); break;
            default: throw new ArgumentOutOfRangeException(nameof(failure));
        }
        StartupValidationResult result = Assert.Single(await validation.ValidateAsync(Token));
        Assert.Equal("startup.postgresql.configuration", result.ValidationId.Value);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal("PostgreSql", result.PropertyPath);
        Assert.Equal(StartupValidationSeverity.BlockingFailure, result.Severity);
        Assert.Equal("The dedicated PostgreSQL connection configuration is missing, unreadable or invalid.", result.Summary);
        Assert.Null(validation.Configuration);
        secrets.AssertRedacted(result.ToString());
    }

    [Theory]
    [InlineData(PostgreSqlComponent.Web)]
    [InlineData(PostgreSqlComponent.Worker)]
    public async Task RegistrationIsDeferredSingletonAndCannotPublishBeforeFullLocalValidation(PostgreSqlComponent component)
    {
        using PostgreSqlSecretFixture secrets = new();
        using ConfigurationManager configuration = Configure(secrets);
        configuration["environment"] = "Test";
        configuration["SiteFactory:Runtime:ClientSlug"] = "example-client";
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://example.invalid";
        ServiceCollection services = new();
        services.AddSingleton<ILogger<RuntimeStartupService>>(NullLogger<RuntimeStartupService>.Instance);
        services.AddSingleton<IHostApplicationLifetime>(new OfflineLifetime());
        services.AddRuntimeStartupValidation(configuration, "Test", component == PostgreSqlComponent.Web ? RuntimeComponent.Web : RuntimeComponent.Worker);
        Assert.Same(services, services.AddPostgreSqlStartupValidation(configuration, component, "Test"));
        Assert.All(services, static descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
        using ServiceProvider provider = services.BuildServiceProvider();
        PostgreSqlStartupValidation validation = provider.GetRequiredService<PostgreSqlStartupValidation>();
        Assert.Same(validation, provider.GetServices<IStartupValidation>().Single(static check => check is PostgreSqlStartupValidation));
        Assert.Null(validation.Configuration);
        Assert.StartsWith("SF-STARTUP-008", Assert.Throws<InvalidOperationException>(provider.GetRequiredService<PostgreSqlRuntimeConfiguration>).Message, StringComparison.Ordinal);
        // A successful file read alone does not bypass the full local startup report.
        Assert.Equal(StartupValidationSeverity.Success, Assert.Single(await validation.ValidateAsync(Token)).Severity);
        Assert.StartsWith("SF-STARTUP-008", Assert.Throws<InvalidOperationException>(provider.GetRequiredService<PostgreSqlRuntimeConfiguration>).Message, StringComparison.Ordinal);
        RuntimeStartupService startup = provider.GetRequiredService<RuntimeStartupService>();
        Assert.Null(startup.Report);
        await startup.StartingAsync(Token);
        PostgreSqlRuntimeConfiguration snapshot = provider.GetRequiredService<PostgreSqlRuntimeConfiguration>();
        Assert.Same(validation.Configuration, snapshot);
        Assert.Same(snapshot, provider.GetRequiredService<PostgreSqlRuntimeConfiguration>());
        Assert.Equal(component, snapshot.Component);
        Assert.Equal("PostgreSQL configuration is valid; connectivity was not tested.",
            startup.Report!.Results.Single(static result => result.ValidationId.Value == "startup.postgresql.configuration").Summary);
        configuration[PostgreSqlConfigurationReader.ConnectionFileKey(component)] = null;
        Assert.Equal("SF-SECRET-001", Assert.Single(await validation.ValidateAsync(Token)).ErrorCode);
        Assert.Null(validation.Configuration);
        Assert.Same(snapshot, provider.GetRequiredService<PostgreSqlRuntimeConfiguration>()); // No new reload policy.
        secrets.AssertRedacted(string.Join('\n', startup.Report.Results));
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedAndClearsAPreviouslyValidatedSnapshot()
    {
        using PostgreSqlSecretFixture secrets = new();
        using ConfigurationManager configuration = Configure(secrets);
        PostgreSqlStartupValidation validation = new(configuration, PostgreSqlComponent.Web, "Test");
        await validation.ValidateAsync(Token);
        Assert.NotNull(validation.Configuration);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validation.ValidateAsync(cancellation.Token));
        Assert.Null(validation.Configuration);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigurationProgrammingFailuresAndForeignCancellationAreNotRelabeled(bool cancelled)
    {
        using CancellationTokenSource foreign = new();
        foreign.Cancel();
        Exception error = cancelled ? new OperationCanceledException(foreign.Token) : new InvalidOperationException("Controlled programming failure.");
        PostgreSqlStartupValidation validation = new(new ThrowingConfiguration(error), PostgreSqlComponent.Web, "Test");
        Exception observed = await Assert.ThrowsAnyAsync<Exception>(() => validation.ValidateAsync(Token));
        Assert.Same(error, observed);
        Assert.Null(validation.Configuration);
    }

    [Theory]
    [InlineData(PostgreSqlComponent.Migrator)]
    [InlineData((PostgreSqlComponent)99)]
    public void RegistrationDoesNotSelectAMigratorOrUnknownReference(PostgreSqlComponent component)
    {
        using ConfigurationManager configuration = new();
        ServiceCollection services = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddPostgreSqlStartupValidation(configuration, component, "Test"));
        Assert.Empty(services);
    }

    private static ConfigurationManager Configure(PostgreSqlSecretFixture secrets)
    {
        ConfigurationManager configuration = new();
        foreach (PostgreSqlComponent component in new[] { PostgreSqlComponent.Web, PostgreSqlComponent.Worker })
            configuration[PostgreSqlConfigurationReader.ConnectionFileKey(component)] = secrets.PathFor(component);
        return configuration;
    }

    private sealed class OfflineLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => throw new InvalidOperationException("No host is started by these local adapter tests.");
    }

    private sealed class ThrowingConfiguration(Exception error) : IConfiguration
    {
        public string? this[string key] { get => throw error; set => throw new NotSupportedException(); }
        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotSupportedException();
        public IChangeToken GetReloadToken() => throw new NotSupportedException();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }
}
