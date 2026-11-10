using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.AspNetCore;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

// Local ownership only: no shared test assembly, private composition, or substituted lifetime.
internal sealed class TestHosts : IAsyncDisposable
{
    private const string DirectoryPrefix = "sitefactory-postgresql-host-test-";
    private readonly List<(IHost Host, IHostApplicationLifetime Lifetime)> hosts = [];
    private readonly List<HttpClient> clients = [];
    private readonly List<Uri> listeners = [];
    private readonly CaptureLoggerProvider logs = new();

    public string DirectoryPath { get; } = Directory.CreateTempSubdirectory(DirectoryPrefix).FullName;
    public IReadOnlyList<string> LogMessages => logs.Messages;

    public WebApplication CreateWeb(string[] args, Action<IServiceCollection>? configureServices = null, bool withDatabase = true,
        PostgreSql.P03Observations? observations = null, ReadinessActivityCapture? readinessObservations = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = DirectoryPath,
            EnvironmentName = EnvironmentFrom(args),
            ApplicationName = typeof(TestHosts).Assembly.GetName().Name,
        });
        Configure(builder.Configuration, builder.Logging, args);
        builder.Services.AddRuntimeStartupValidation(builder.Configuration, builder.Environment.EnvironmentName, RuntimeComponent.Web);
        if (withDatabase)
        {
            builder.Services.AddPostgreSqlStartupValidation(builder.Configuration, PostgreSqlComponent.Web, builder.Environment.EnvironmentName);
            builder.Services.AddWebDatabaseReadiness(builder.Configuration);
        }
        builder.Services.AddRuntimeHealthChecks();
        // Offline tests replace only the read port, after all public production registrations.
        configureServices?.Invoke(builder.Services);
        WebApplication web = builder.Build();
        hosts.Add((web, web.Services.GetRequiredService<IHostApplicationLifetime>()));
        web.UseRouting();
        observations?.Observe(web);
        readinessObservations?.Observe(web);
        if (withDatabase) web.UseRuntimeReadinessGate();
        web.MapRuntimeHealthEndpoints();
        web.MapGet("/", static () => Results.Text("runtime-host-test"));
        return web;
    }

    public IHost CreateWorker(string[] args, Action<IServiceCollection>? configureServices = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ContentRootPath = DirectoryPath,
            EnvironmentName = EnvironmentFrom(args),
        });
        Configure(builder.Configuration, builder.Logging, args);
        builder.Services.AddRuntimeStartupValidation(builder.Configuration, builder.Environment.EnvironmentName, RuntimeComponent.Worker);
        builder.Services.AddPostgreSqlStartupValidation(builder.Configuration, PostgreSqlComponent.Worker, builder.Environment.EnvironmentName);
        builder.Services.AddWorkerDatabaseStartupValidation(builder.Configuration);
        configureServices?.Invoke(builder.Services);
        IHost host = builder.Build();
        hosts.Add((host, host.Services.GetRequiredService<IHostApplicationLifetime>()));
        return host;
    }

    public HttpClient Client(WebApplication web, TimeSpan? timeout = null)
    {
        Uri address = new(web.Urls.Single());
        Assert.True(address.IsLoopback && address.Port != 0);
        HttpClient client = new() { BaseAddress = address };
        if (timeout is not null) client.Timeout = timeout.Value;
        clients.Add(client);
        listeners.Add(address);
        return client;
    }

    private void Configure(ConfigurationManager configuration, ILoggingBuilder logging, string[] args)
    {
        // No ambient credentials, appsettings, environment variables or reload in this test composition.
        configuration.Sources.Clear();
        configuration.SetBasePath(DirectoryPath).AddCommandLine(args);
        logging.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace);
    }

    private static string EnvironmentFrom(string[] args)
    {
        int index = Array.IndexOf(args, "--environment");
        Assert.True(index >= 0 && index + 1 < args.Length, "A test host requires an explicit environment.");
        return args[index + 1];
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (HttpClient client in clients) client.Dispose();
            foreach ((IHost host, IHostApplicationLifetime lifetime) in hosts.AsEnumerable().Reverse())
            {
                try
                {
                    if (lifetime.ApplicationStarted.IsCancellationRequested && !lifetime.ApplicationStopped.IsCancellationRequested)
                    {
                        using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
                        await host.StopAsync(cleanup.Token);
                    }
                }
                finally
                {
                    if (host is IAsyncDisposable asynchronous) await asynchronous.DisposeAsync();
                    else host.Dispose();
                }
            }
            // Probe only addresses owned by this fixture after disposing their actual Kestrel hosts.
            foreach (Uri address in listeners.Distinct())
            {
                using TcpClient socket = new();
                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));
                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, address.Port, cleanup.Token);
                    Assert.Fail("The test's Kestrel listener remained active after host disposal.");
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionRefused) { }
            }
        }
        finally
        {
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, DirectoryPath, comparison);
            Assert.StartsWith(DirectoryPrefix, Path.GetFileName(DirectoryPath), StringComparison.Ordinal);
            Assert.False((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0);
            Assert.Empty(Directory.GetFileSystemEntries(DirectoryPath));
            Directory.Delete(DirectoryPath);
            Assert.False(Directory.Exists(DirectoryPath));
        }
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();
        public IReadOnlyList<string> Messages => messages.ToArray();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception) + exception?.ToString());
    }
}

// Test-only listener and bounded request capture. No service replacement or readiness state inspection.
internal sealed class ReadinessActivityCapture : IDisposable
{
    internal const string RequestHeader = "X-SiteFactory-Readiness-Test";
    private readonly RequestTrace?[] traces = new RequestTrace[768];
    private readonly ActivityListener listener = new()
    {
        ShouldListenTo = static source => source.Name == "Microsoft.AspNetCore",
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    internal ReadinessActivityCapture() => ActivitySource.AddActivityListener(listener);

    internal void Observe(WebApplication application) => application.Use((HttpContext context, RequestDelegate next) =>
    {
        if (!int.TryParse(context.Request.Headers[RequestHeader], out int id) || (uint)id >= traces.Length) return next(context);
        Activity? activity = Activity.Current;
        RequestTrace trace = new(activity?.TraceId.ToHexString(), activity?.SpanId.ToHexString(), activity?.Kind == ActivityKind.Server);
        if (Interlocked.CompareExchange(ref traces[id], trace, null) is not null)
            throw new InvalidOperationException("A readiness test request ID cannot be reused.");
        try
        {
            Task original = next(context);
            if (original.IsCompleted) Complete();
            else _ = original.ContinueWith(_ => Complete(), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return original;
        }
        catch { Complete(); throw; }

        void Complete()
        {
            trace.Events = Read(activity);
            trace.Completed.TrySetResult();
        }
    });

    internal async Task<RequestTrace> CompletedAsync(int id, CancellationToken token)
    {
        RequestTrace trace = traces[id] ?? throw new InvalidOperationException("No HTTP trace was captured.");
        await trace.Completed.Task.WaitAsync(TimeSpan.FromSeconds(12), token);
        return trace;
    }

    internal static ReadinessEvent[] Read(Activity? activity) => activity is null ? [] : activity.Events
        .Where(static item => item.Name is "sitefactory.readiness.refresh.completed" or "sitefactory.readiness.http.decision")
        .Take(3).Select(item => new ReadinessEvent(item.Name, activity.TraceId.ToHexString(), activity.SpanId.ToHexString(),
            item.Tags.Take(6).Where(static tag => SafeTag(tag.Key, tag.Value)).ToDictionary(static tag => tag.Key, static tag => tag.Value,
                StringComparer.Ordinal))).ToArray();

    private static bool SafeTag(string key, object? value) => key switch
    {
        "startTicks" or "endTicks" => value is long,
        "result" or "refreshResult" => value is null or bool,
        "site" => value is "HEALTH_READY" or "HEALTH_LEGACY" or "TRAFFIC_GATE",
        "outcome" => value is "NOT_INITIALIZED" or "QUEUE_TIMEOUT" or "READY" or "BLOCKED" or "CANCELLED"
            or "UNEXPECTED_ERROR" or "HOST_NOT_STARTED" or "HOST_STOPPING" or "REFRESH_REJECTED" or "ACCEPTED" or "THREW",
        "diagnosticCode" => value is null or "SF-DB-STARTUP-001" or "SF-DB-STARTUP-003" or "SF-DB-STARTUP-004"
            or "SF-DB-STARTUP-005" or "SF-DB-STARTUP-006" or "SF-DB-STARTUP-007" or "SF-DB-STARTUP-008" or "SF-DB-STARTUP-009"
            or "SF-SCHEMA-STARTUP-001" or "SF-SCHEMA-STARTUP-004" or "SF-SCHEMA-STARTUP-005" or "SF-SCHEMA-STARTUP-006"
            or "SF-SCHEMA-STARTUP-007" or "SF-SCHEMA-STARTUP-008" or "SF-SCHEMA-STARTUP-009",
        _ => false,
    };

    internal static void AssertDecision(RequestTrace trace, IReadOnlyList<ReadinessEvent> events, string site,
        string outcome, bool result, string? code = null)
    {
        Assert.True(trace.ServerActivity);
        Assert.NotNull(trace.TraceId);
        Assert.NotNull(trace.SpanId);
        Assert.Equal(2, events.Count);
        Assert.All(events, item => { Assert.Equal(trace.TraceId, item.TraceId); Assert.Equal(trace.SpanId, item.SpanId); });
        ReadinessEvent refresh = Assert.Single(events, static item => item.Name == "sitefactory.readiness.refresh.completed");
        Assert.Equal(["diagnosticCode", "endTicks", "outcome", "result", "startTicks"], refresh.Tags.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(outcome, refresh.Tags["outcome"]);
        Assert.Equal(result, refresh.Tags["result"]);
        Assert.Equal(code, refresh.Tags["diagnosticCode"]);
        Assert.True((long)refresh.Tags["startTicks"]! > 0 && (long)refresh.Tags["endTicks"]! >= (long)refresh.Tags["startTicks"]!);
        ReadinessEvent decision = Assert.Single(events, static item => item.Name == "sitefactory.readiness.http.decision");
        Assert.Equal(["outcome", "refreshResult", "site"], decision.Tags.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(site, decision.Tags["site"]);
        Assert.Equal(result ? "ACCEPTED" : "REFRESH_REJECTED", decision.Tags["outcome"]);
        Assert.Equal(result, decision.Tags["refreshResult"]);
    }

    public void Dispose() => listener.Dispose();
    internal sealed class RequestTrace(string? traceId, string? spanId, bool serverActivity)
    {
        internal string? TraceId { get; } = traceId;
        internal string? SpanId { get; } = spanId;
        internal bool ServerActivity { get; } = serverActivity;
        internal ReadinessEvent[] Events { get; set; } = [];
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    internal sealed record ReadinessEvent(string Name, string TraceId, string SpanId, Dictionary<string, object?> Tags);
}
