using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Hosting.Tests;

public sealed class RuntimeStartupLifecycleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HostDoesNotStartAnyServiceBeforeBlockingValidationCompletes()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.extra", StartupValidationSeverity.BlockingFailure, []));
        Observer observer = new();
        builder.Services.AddSingleton<IHostedService>(observer);
        using IHost host = builder.Build();
        Assert.Null(host.Services.GetRequiredService<RuntimeStartupService>().Report);
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        Assert.False(error.Report.IsReady);
        Assert.Equal(2, error.Report.Results.Count);
        Assert.False(observer.Started);
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
        Assert.False(host.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        Assert.Equal(error.Message, error.ToString());
    }

    [Fact]
    public async Task WarningAllowsTheHostToStartWithAnObservableReport()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.extra", StartupValidationSeverity.Warning, []));
        using IHost host = builder.Build();
        await host.StartAsync(Token);
        try
        {
            RuntimeStartupReport report = Assert.IsType<RuntimeStartupReport>(host.Services.GetRequiredService<RuntimeStartupService>().Report);
            Assert.True(report.IsReady);
            Assert.True(report.HasWarnings);
            Assert.True(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
            Assert.Equal(RuntimeComponent.Worker, host.Services.GetRequiredService<RuntimeIdentity>().Component);
        }
        finally
        {
            await host.StopAsync(Token);
        }
    }

    [Fact]
    public async Task HostCancellationRetainsTheCancelledReportAndDoesNotStartServices()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.Services.AddSingleton<IStartupValidation>(new Check(new("startup.extra"), async token =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable.");
        }));
        Observer observer = new();
        builder.Services.AddSingleton<IHostedService>(observer);
        using IHost host = builder.Build();
        Task starting = host.StartAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(TimeSpan.FromSeconds(10), Token));
        RuntimeStartupReport report = Assert.IsType<RuntimeStartupReport>(host.Services.GetRequiredService<RuntimeStartupService>().Report);
        Assert.True(report.Cancelled);
        Assert.False(report.IsReady);
        Assert.False(observer.Started);
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
        Assert.False(host.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeConfiguration>());
    }

    [Theory]
    [InlineData(RuntimeComponent.Web)]
    [InlineData(RuntimeComponent.Worker)]
    public async Task SnapshotsAndReadinessBecomeAvailableAtTheirExistingLifecycleBoundaries(RuntimeComponent component)
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        Assert.Same(builder.Services, builder.Services.AddRuntimeStartupValidation(configuration, "Production", component));
        Assert.All(builder.Services.Where(static descriptor => descriptor.ServiceType == typeof(RuntimeConfigurationStartupValidation)
            || descriptor.ServiceType == typeof(IStartupValidation) || descriptor.ServiceType == typeof(IRuntimeStartupValidator)
            || descriptor.ServiceType == typeof(RuntimeStartupService) || descriptor.ServiceType == typeof(IHostedService)
            || descriptor.ServiceType == typeof(RuntimeConfiguration) || descriptor.ServiceType == typeof(RuntimeIdentity)),
            static descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
        TaskCompletionSource checking = Signal();
        TaskCompletionSource finishCheck = Signal();
        builder.Services.AddSingleton<IStartupValidation>(new Check(new("startup.extra"), async token =>
        {
            checking.SetResult();
            await finishCheck.Task.WaitAsync(token);
            return [new(new("startup.extra"), StartupValidationSeverity.Success, null, "Valid.")];
        }));
        LifecycleObserver observer = new();
        builder.Services.AddSingleton<IHostedService>(_ => observer);
        IHost host = builder.Build();
        RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
        IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        Task? starting = null;
        try
        {
            Assert.IsType<RuntimeStartupValidator>(host.Services.GetRequiredService<IRuntimeStartupValidator>());
            Assert.Same(startup, Assert.Single(host.Services.GetServices<IHostedService>(), static service => service is RuntimeStartupService));
            RuntimeConfigurationStartupValidation binding = host.Services.GetRequiredService<RuntimeConfigurationStartupValidation>();
            Assert.Same(binding, Assert.Single(host.Services.GetServices<IStartupValidation>(), static check => check is RuntimeConfigurationStartupValidation));
            Assert.Null(binding.Configuration);
            Assert.Null(startup.Report);
            Assert.False(startup.ConfigurationIsValidated);
            Assert.False(startup.IsReady);
            Assert.Contains("SF-STARTUP-008", Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeConfiguration>()).Message);
            Assert.Contains("SF-STARTUP-008", Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeIdentity>()).Message);

            starting = host.StartAsync(cancellation.Token);
            await checking.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            Assert.NotNull(binding.Configuration);
            Assert.Null(startup.Report);
            Assert.False(startup.ConfigurationIsValidated);
            Assert.False(startup.IsReady);
            Assert.False(observer.Started);
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeConfiguration>());
            finishCheck.SetResult();

            await observer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            RuntimeStartupReport report = Assert.IsType<RuntimeStartupReport>(startup.Report);
            Assert.True(report.IsReady);
            Assert.Equal(["startup.configuration.schema", "startup.extra"], report.Results.Select(static result => result.ValidationId.Value));
            Assert.True(startup.ConfigurationIsValidated);
            Assert.False(startup.IsReady);
            Assert.False(lifetime.ApplicationStarted.IsCancellationRequested);
            RuntimeConfiguration snapshot = host.Services.GetRequiredService<RuntimeConfiguration>();
            Assert.Same(binding.Configuration, snapshot);
            Assert.Same(snapshot.Identity, host.Services.GetRequiredService<RuntimeIdentity>());
            Assert.Equal(component, snapshot.Identity.Component);
            Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)report.Results).Clear());
            observer.Release.SetResult();
            await starting.WaitAsync(TimeSpan.FromSeconds(10), Token);
            Assert.True(startup.IsReady);
            Assert.True(lifetime.ApplicationStarted.IsCancellationRequested);

            await host.StopAsync(Token);
            Assert.True(observer.Stopped);
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
            Assert.False(startup.IsReady);
            Assert.Same(report, startup.Report);
            Assert.Same(snapshot, host.Services.GetRequiredService<RuntimeConfiguration>());
            Assert.Same(snapshot.Identity, host.Services.GetRequiredService<RuntimeIdentity>());
        }
        finally
        {
            finishCheck.TrySetResult();
            observer.Release.TrySetResult();
            await cancellation.CancelAsync();
            if (starting is not null && !starting.IsCompleted)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(TimeSpan.FromSeconds(10), Token));
            host.Dispose();
        }
        Assert.True(observer.Disposed);
    }

    [Fact]
    public async Task GeneratedLogsPreserveEventFieldsLevelsAndExpectedExceptionRedaction()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        CaptureLoggerProvider capture = new();
        builder.Logging.AddProvider(capture);
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.a", StartupValidationSeverity.Success, []));
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.b", StartupValidationSeverity.Warning, []));
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.c", StartupValidationSeverity.BlockingFailure, []));
        using IHost host = builder.Build();
        RuntimeStartupException exception = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        LogEntry[] entries = capture.Entries.Where(static entry => entry.Category == typeof(RuntimeStartupService).FullName).ToArray();
        Assert.Equal(4, entries.Length);
        Assert.Equal([LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Information], entries.Select(static entry => entry.Level));
        Assert.All(entries, static entry =>
        {
            Assert.Equal(1, entry.Event.Id);
            Assert.Null(entry.ExceptionText);
            Assert.Equal(["ErrorCode", "PropertyPath", "Severity", "Summary", "ValidationId", "{OriginalFormat}"],
                entry.Fields.Keys.OrderBy(static key => key, StringComparer.Ordinal));
            Assert.Equal("Startup validation {ValidationId}: {Severity} {ErrorCode} {PropertyPath} {Summary}", entry.Fields["{OriginalFormat}"]);
        });
        Assert.Equal(["startup.a", "startup.b", "startup.c", "startup.configuration.schema"], entries.Select(static entry => entry.Fields["ValidationId"]));
        for (int index = 0; index < entries.Length; index++)
        {
            StartupValidationResult result = exception.Report.Results[index];
            Assert.Equal(result.Severity, entries[index].Fields["Severity"]);
            Assert.Equal(result.ErrorCode, entries[index].Fields["ErrorCode"]);
            Assert.Equal(result.PropertyPath, entries[index].Fields["PropertyPath"]);
            Assert.Equal(result.Summary, entries[index].Fields["Summary"]);
        }
        Assert.Contains(capture.Entries, entry => entry.ExceptionText == exception.Message);
        Assert.DoesNotContain(capture.Entries, static entry => entry.ExceptionText?.Contains(" at ", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("client-test", string.Join("\n", capture.Entries.Select(static entry => entry.Message)), StringComparison.Ordinal);
        Assert.DoesNotContain("www.example.test", string.Join("\n", capture.Entries.Select(static entry => entry.Message)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidConfigurationValuesNeverEnterLogsOrExpectedErrors()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://user:do-not-log@example.test";
        CaptureLoggerProvider capture = new();
        builder.Logging.AddProvider(capture);
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Web);
        using IHost host = builder.Build();
        RuntimeStartupException exception = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        Assert.Equal("SF-CONFIG-STARTUP-005", Assert.Single(exception.Report.Results).ErrorCode);
        Assert.DoesNotContain("do-not-log", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(capture.Entries, static entry => entry.Message.Contains("do-not-log", StringComparison.Ordinal)
            || entry.ExceptionText?.Contains("do-not-log", StringComparison.Ordinal) == true
            || entry.Fields.Values.Any(static value => value?.ToString()?.Contains("do-not-log", StringComparison.Ordinal) == true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProgrammingExceptionsAndForeignCancellationArePropagatedWithoutInventingAReport(bool foreignCancellation)
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        Exception expected = foreignCancellation ? new OperationCanceledException("Foreign cancellation.") : new InvalidOperationException("Programming failure.");
        builder.Services.AddSingleton<IStartupValidation>(new Check(new("startup.extra"), _ => throw expected));
        Observer observer = new();
        builder.Services.AddSingleton<IHostedService>(observer);
        using IHost host = builder.Build();
        Exception actual = foreignCancellation
            ? await Assert.ThrowsAsync<OperationCanceledException>(() => host.StartAsync(Token))
            : await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(Token));
        Assert.Same(expected, actual);
        RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
        Assert.Null(startup.Report);
        Assert.False(startup.ConfigurationIsValidated);
        Assert.False(startup.IsReady);
        Assert.False(observer.Started);
        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeConfiguration>());
    }

    [Fact]
    public async Task CancellationBeforeLifecycleValidationPublishesTheCancelledReportWithoutRunningChecks()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        List<string> calls = [];
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.a", StartupValidationSeverity.Success, calls));
        using IHost host = builder.Build();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.StartingAsync(cancellation.Token));
        RuntimeStartupReport report = Assert.IsType<RuntimeStartupReport>(startup.Report);
        Assert.True(report.Cancelled);
        Assert.Equal("SF-STARTUP-005", Assert.Single(report.Results).ErrorCode);
        Assert.Empty(calls);
        Assert.Null(host.Services.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration);
        Assert.False(startup.ConfigurationIsValidated);
        Assert.False(startup.IsReady);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NeutralAdapterKeepsLocalPrerequisitesSeparateFromAvailability(bool localPrerequisites, bool available)
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Web);
        builder.Services.AddSingleton<IRuntimeStartupReadiness>(new FakeReadiness(localPrerequisites, available));
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.specialized", StartupValidationSeverity.BlockingFailure, []));
        Observer observer = new();
        builder.Services.AddSingleton<IHostedService>(observer);
        using IHost host = builder.Build();
        RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
        if (!localPrerequisites)
        {
            await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
            Assert.NotNull(host.Services.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration);
            Assert.False(startup.ConfigurationIsValidated);
            Assert.False(observer.Started);
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeConfiguration>());
        }
        else
        {
            await host.StartAsync(Token);
            try
            {
                Assert.True(startup.ConfigurationIsValidated);
                Assert.True(observer.Started);
                Assert.Equal(available, startup.IsReady);
                Assert.NotNull(host.Services.GetRequiredService<RuntimeConfiguration>());
            }
            finally { await host.StopAsync(Token); }
            Assert.False(startup.IsReady);
        }
        Assert.False(Assert.IsType<RuntimeStartupReport>(startup.Report).IsReady);
    }

    [Fact]
    public async Task HostedServiceConstructionCannotBypassTheSnapshotGuard()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        builder.Services.AddSingleton<IHostedService>(provider =>
        {
            _ = provider.GetRequiredService<RuntimeConfiguration>();
            return new Observer();
        });
        using IHost host = builder.Build();
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(Token));
        Assert.Contains("SF-STARTUP-008", error.Message);
        Assert.Null(host.Services.GetRequiredService<RuntimeStartupService>().Report);
        Assert.Null(host.Services.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration);
    }

    [Fact]
    public async Task ExplicitBindingRevalidationDoesNotReplaceAlreadyResolvedSingletons()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        using IHost host = builder.Build();
        await host.StartAsync(Token);
        try
        {
            RuntimeConfiguration original = host.Services.GetRequiredService<RuntimeConfiguration>();
            RuntimeStartupReport? report = host.Services.GetRequiredService<RuntimeStartupService>().Report;
            configuration["SiteFactory:Runtime:ClientSlug"] = "other-client";
            RuntimeConfigurationStartupValidation binding = host.Services.GetRequiredService<RuntimeConfigurationStartupValidation>();
            Assert.Equal(StartupValidationSeverity.Success, Assert.Single(await binding.ValidateAsync(Token)).Severity);
            Assert.Equal("other-client", Assert.IsType<RuntimeConfiguration>(binding.Configuration).Identity.ClientSlug);
            Assert.Same(original, host.Services.GetRequiredService<RuntimeConfiguration>());
            Assert.Same(original.Identity, host.Services.GetRequiredService<RuntimeIdentity>());
            Assert.Same(report, host.Services.GetRequiredService<RuntimeStartupService>().Report);
        }
        finally { await host.StopAsync(Token); }
    }

    [Fact]
    public void ExpectedExceptionContainsOnlyBlockersAndRetainsTheImmutableReport()
    {
        StartupValidationId id = new("startup.test");
        StartupValidationResult blocker = new(id, StartupValidationSeverity.BlockingFailure, "SF-STARTUP-002", "Fixed blocker.", "Test");
        RuntimeStartupReport report = new([
            new(id, StartupValidationSeverity.Success, null, "Success detail."),
            new(id, StartupValidationSeverity.Warning, "SF-STARTUP-002", "Warning detail."), blocker]);
        RuntimeStartupException exception = new(report);
        Assert.Same(report, exception.Report);
        Assert.Equal(blocker.ToString(), exception.Message);
        Assert.Equal(exception.Message, exception.ToString());
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)exception.Report.Results).Clear());
    }

    [Fact]
    public async Task OtherLifecycleCallbacksRemainWithoutEffects()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using ConfigurationManager configuration = ValidConfiguration();
        builder.Services.AddRuntimeStartupValidation(configuration, "Production", RuntimeComponent.Worker);
        List<string> calls = [];
        builder.Services.AddSingleton<IStartupValidation>(Fixed("startup.extra", StartupValidationSeverity.Success, calls));
        using IHost host = builder.Build();
        RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        await startup.StartAsync(cancellation.Token);
        await startup.StartedAsync(cancellation.Token);
        await startup.StoppingAsync(cancellation.Token);
        await startup.StopAsync(cancellation.Token);
        await startup.StoppedAsync(cancellation.Token);
        Assert.Empty(calls);
        Assert.Null(startup.Report);
        Assert.False(startup.IsReady);
        Assert.Null(host.Services.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration);
    }

    private static HostApplicationBuilder CreateBuilder() => Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        DisableDefaults = true,
        EnvironmentName = "Production",
    });

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ConfigurationManager ValidConfiguration()
    {
        ConfigurationManager configuration = new();
        configuration["environment"] = "Production";
        configuration["SiteFactory:Runtime:ClientSlug"] = "client-test";
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://www.example.test";
        return configuration;
    }

    private static Check Fixed(string name, StartupValidationSeverity severity, List<string> calls)
    {
        StartupValidationId id = new(name);
        return new(id, token =>
        {
            token.ThrowIfCancellationRequested();
            calls.Add(name);
            return Task.FromResult<IReadOnlyList<StartupValidationResult>>(
                [new(id, severity, severity == StartupValidationSeverity.Success ? null : "SF-STARTUP-002", "Fixed test diagnostic.")]);
        });
    }

    private sealed class Check(StartupValidationId id, Func<CancellationToken, Task<IReadOnlyList<StartupValidationResult>>> validate) : IStartupValidation
    {
        public StartupValidationId Id { get; } = id;
        public Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken) => validate(cancellationToken);
    }

    private sealed class Observer : IHostedService
    {
        public bool Started { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { Started = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeReadiness(bool configurationValidated, bool isReady) : IRuntimeStartupReadiness
    {
        public bool ConfigurationValidated { get; } = configurationValidated;
        public bool IsReady { get; } = isReady;
    }

    private sealed class LifecycleObserver : IHostedService, IDisposable
    {
        public TaskCompletionSource Entered { get; } = Signal();
        public TaskCompletionSource Release { get; } = Signal();
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        public Task StopAsync(CancellationToken cancellationToken) { Stopped = true; return Task.CompletedTask; }
        public void Dispose() => Disposed = true;
    }

    private sealed record LogEntry(string Category, LogLevel Level, EventId Event, string Message,
        string? ExceptionText, IReadOnlyDictionary<string, object?> Fields);

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<LogEntry> entries = new();
        public IReadOnlyList<LogEntry> Entries => entries.ToArray();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, entries);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(string category, System.Collections.Concurrent.ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, object?> fields = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal) : [];
            entries.Enqueue(new(category, logLevel, eventId, formatter(state, exception), exception?.ToString(), fields));
        }
    }
}
