using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P03")]
public sealed class PostgreSqlWebConcurrencyTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires explicitly configured dedicated PostgreSQL 18 with verified TLS.")]
    public async Task RealWebReadinessHasNoFalseFailuresUnderConcurrentTraffic()
    {
        using P03Observations observations = new();
        try
        {
            await using RealPostgreSqlHostFixture fixture = await RealPostgreSqlHostFixture.CreateAsync(Token);
            ConcurrentBag<(HttpStatusCode Status, bool ExactBody, bool NoStore)> results = [];
            await fixture.WithoutMutationAsync(async () =>
            {
                await using TestHosts hosts = new();
                WebApplication web = hosts.CreateWeb(["--urls", "http://127.0.0.1:0", "--environment", "Test",
                "--SiteFactory:Runtime:ClientSlug", "example-client", "--SiteFactory:Runtime:PublicBaseUrl", "https://example.invalid",
                "--SITEFACTORY_WEB_CONNECTION_FILE", fixture.FileFor(PostgreSqlTestLogin.Web),
                "--" + WebDatabaseReadiness.ExpectedRoleKey, fixture.Roles.Baseline.Web], observations: observations);
                await web.StartAsync(Token);
                Assert.True(web.Services.GetRequiredService<RuntimeStartupService>().IsReady);
                Assert.IsType<PostgreSqlDatabaseReader>(web.Services.GetRequiredService<IRuntimeDatabaseReader>());
                WebDatabaseReadiness readiness = web.Services.GetRequiredService<WebDatabaseReadiness>();
                Assert.Same(readiness, web.Services.GetRequiredService<IRuntimeStartupValidator>());
                Assert.Same(readiness, web.Services.GetRequiredService<IRuntimeStartupReadiness>());
                Assert.Same(readiness, web.Services.GetRequiredService<IRuntimeReadinessRefresh>());
                TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Task[] workers = Enumerable.Range(0, 32).Select(async worker =>
                {
                    using HttpClient client = hosts.Client(web, TimeSpan.FromSeconds(12));
                    await start.Task.WaitAsync(Token);
                    string[] routes = ["/health/ready", "/health", "/"];
                    for (int index = 0; index < 24; index++)
                    {
                        string route = routes[(worker + index) % routes.Length];
                        int requestId = worker * 24 + index;
                        P03Observations.HttpObservation observation = observations.BeginClient(requestId, route);
                        using HttpRequestMessage request = new(HttpMethod.Get, route);
                        request.Headers.Add(P03Observations.RequestHeader, requestId.ToString(CultureInfo.InvariantCulture));
                        try
                        {
                            using HttpResponseMessage response = await client.SendAsync(request, Token);
                            observation.Status = (int)response.StatusCode;
                            string body = await response.Content.ReadAsStringAsync(Token);
                            string expected = route switch { "/health/ready" => "{\"status\":\"healthy\"}", "/health" => "Healthy", _ => "runtime-host-test" };
                            results.Add((response.StatusCode, body == expected, route == "/" || response.Headers.CacheControl?.ToString() == "no-store"));
                        }
                        catch (Exception exception)
                        {
                            observation.ExceptionKind = P03Observations.SafeExceptionKind(exception);
                            throw; // Observation only: retain the original failure and cancellation.
                        }
                        finally { observations.EndClient(observation); }
                    }
                }).ToArray();
                start.SetResult();
                try { await Task.WhenAll(workers); }
                finally
                {
                    // Retain partial/failing observations before either host or credential fixture is destroyed.
                    observations.Save(output);
                    await fixture.AssertAllRedactedAsync(observations.SafeJson(), Token);
                }
                await fixture.AssertAllRedactedAsync(string.Join("\n", hosts.LogMessages)
                    + string.Join("\n", web.Services.GetRequiredService<WebDatabaseReadiness>().CurrentReport!.Results), Token);
            }, Token);
            output.WriteLine("P03 concurrency: workers=32 requests={0} HTTP200={1} HTTP503={2}", results.Count,
                results.Count(static result => result.Status == HttpStatusCode.OK), results.Count(static result => result.Status == HttpStatusCode.ServiceUnavailable));
            Assert.Equal(768, results.Count);
            Assert.All(results, static result =>
            {
                Assert.Equal(HttpStatusCode.OK, result.Status);
                Assert.True(result.ExactBody && result.NoStore, "Concurrent HTTP response violated the public payload/cache contract.");
            });
            observations.AssertSuccessfulCoverage();
            await fixture.AssertSessionsClosedAsync(Token);
        }
        finally { observations.Save(output); } // Final lifecycle/resources, even when historical assertions fail.
    }
}

// Passive, bounded telemetry. CORR-004 consumes the production Activity events without a reader/refresh decorator.
// No extra probe, DI registration, scheduling gate or timeout. Never read CurrentReport for attribution.
internal sealed class P03Observations : IDisposable
{
    internal const string RequestHeader = "X-P03-Observation";
    private const int RequestCount = 768;
    private const int MaximumEvents = 32768;
    private static readonly AsyncLocal<(P03Observations Owner, HttpObservation Request)?> Current = new();
    private static readonly int[] EventIds = [NpgsqlEventId.OpeningConnection, NpgsqlEventId.OpenedConnection,
        NpgsqlEventId.ClosingConnection, NpgsqlEventId.ClosedConnection, NpgsqlEventId.BreakingConnection,
        NpgsqlEventId.ExecutingCommand, NpgsqlEventId.CommandExecutionCompleted, NpgsqlEventId.CancellingCommand];
    private static readonly long[] AggregateCounts = new long[EventIds.Length];
    private static readonly ILoggerFactory ProviderLogging = LoggerFactory.Create(static builder =>
        builder.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(new ProviderEvents()));
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static readonly string[] Limitations = ["Reader outcome and the origin of SF-DB-STARTUP-001 remain UNKNOWN; no reader instrumentation or timeout inference.",
        "A check/gate decision is not the final framework HTTP status; missing/mismatched Activity evidence remains UNKNOWN.",
        "Npgsql events are associated only through the HTTP ExecutionContext, not a backend/session identifier.",
        "Connection counters are not pg_stat_activity; middleware completion is not a network acknowledgement."];
    private readonly HttpObservation?[] requests = new HttpObservation[RequestCount];
    private readonly ConcurrentQueue<ProviderObservation> events = new();
    private readonly ConcurrentQueue<LifecycleObservation> lifecycle = new();
    private readonly List<CancellationTokenRegistration> registrations = [];
    private readonly ReadinessActivityCapture activityListener = new();
    private readonly object before = Resources();
    private readonly string directory = CampaignDirectory();
    private int eventCount;
    private int droppedEvents;
    private int completionOrder;
    private int activeConnections;
    private int maximumConnections;

    internal static void InitializeProviderLogging() =>
        NpgsqlLoggingConfiguration.InitializeLogging(ProviderLogging, parameterLoggingEnabled: false);

    internal HttpObservation BeginClient(int id, string route)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, RequestCount);
        if (route is not ("/" or "/health" or "/health/ready")) throw new ArgumentException("Only the three P03 routes may be observed.", nameof(route));
        HttpObservation request = new() { RequestId = id, Route = route, StartTimestamp = Stopwatch.GetTimestamp() };
        if (Interlocked.CompareExchange(ref requests[id], request, null) is not null)
            throw new InvalidOperationException("A P03 request ID cannot be reused.");
        return request;
    }

    internal void EndClient(HttpObservation request)
    {
        request.EndTimestamp = Stopwatch.GetTimestamp();
        request.CompletionOrder = Interlocked.Increment(ref completionOrder);
    }

    internal void Observe(WebApplication application)
    {
        IHostApplicationLifetime lifetime = application.Services.GetRequiredService<IHostApplicationLifetime>();
        Register("APPLICATION_STARTED", lifetime.ApplicationStarted);
        Register("APPLICATION_STOPPING", lifetime.ApplicationStopping);
        Register("APPLICATION_STOPPED", lifetime.ApplicationStopped);
        application.Use((HttpContext context, RequestDelegate next) =>
        {
            if (!int.TryParse(context.Request.Headers[RequestHeader].ToString(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int id) || (uint)id >= RequestCount || requests[id] is not { } request
                || context.Request.Path.Value != request.Route) return next(context);
            request.ServerStartTimestamp = Stopwatch.GetTimestamp();
            request.StartedBefore = lifetime.ApplicationStarted.IsCancellationRequested;
            request.StoppingBefore = lifetime.ApplicationStopping.IsCancellationRequested;
            Activity? activity = Activity.Current;
            request.TraceId = activity?.TraceId.ToHexString();
            request.SpanId = activity?.SpanId.ToHexString();
            request.ServerActivity = activity?.Kind == ActivityKind.Server;
            CancellationToken requestAborted = context.RequestAborted;
            (P03Observations Owner, HttpObservation Request)? previous = Current.Value;
            Current.Value = (this, request);
            try
            {
                Task original = next(context);
                if (original.IsCompleted) CompleteServer(original, lifetime, request, activity, requestAborted);
                else
                {
                    // Return the ORIGINAL pipeline Task; no extra await, delay or decision in the HTTP path.
                    // This non-cancellable completion observation also records aborted requests.
                    _ = original.ContinueWith(completed => CompleteServer(completed, lifetime, request, activity, requestAborted),
                        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
                return original;
            }
            catch (Exception exception)
            {
                request.ServerExceptionKind = SafeExceptionKind(exception);
                request.ServerEndTimestamp = Stopwatch.GetTimestamp();
                request.ReadinessEvents = ReadinessActivityCapture.Read(activity);
                throw;
            }
            finally { Current.Value = previous; } // Callee async continuations retain their own ExecutionContext.
        });
    }

    private void Register(string kind, CancellationToken token) => registrations.Add(token.Register(() =>
        lifecycle.Enqueue(new(kind, Stopwatch.GetTimestamp()))));

    private static void CompleteServer(Task task, IHostApplicationLifetime lifetime, HttpObservation request, Activity? activity,
        CancellationToken requestAborted)
    {
        request.ServerEndTimestamp = Stopwatch.GetTimestamp();
        request.StartedAfter = lifetime.ApplicationStarted.IsCancellationRequested;
        request.StoppingAfter = lifetime.ApplicationStopping.IsCancellationRequested;
        request.RequestAborted = requestAborted.IsCancellationRequested;
        request.ServerExceptionKind = task.IsCanceled ? "CANCELLED"
            : task.IsFaulted ? SafeExceptionKind(task.Exception!.InnerExceptions[0]) : "NONE";
        request.ReadinessEvents = ReadinessActivityCapture.Read(activity);
    }

    internal static string SafeExceptionKind(Exception exception) => exception switch
    {
        OperationCanceledException => "CANCELLED",
        TimeoutException => "TIMEOUT_EXCEPTION",
        HttpRequestException => "HTTP_REQUEST_EXCEPTION",
        PostgresException => "POSTGRES_EXCEPTION",
        NpgsqlException => "NPGSQL_EXCEPTION",
        IOException => "IO_EXCEPTION",
        InvalidOperationException => "INVALID_OPERATION_EXCEPTION",
        _ => "OTHER_EXCEPTION",
    };

    private void Record(HttpObservation request, string category, int eventId, Exception? exception)
    {
        if (Interlocked.Increment(ref eventCount) > MaximumEvents)
        {
            Interlocked.Increment(ref droppedEvents);
            return;
        }
        long timestamp = Stopwatch.GetTimestamp();
        events.Enqueue(new(request.RequestId, category, eventId, timestamp,
            exception is null ? "NONE" : SafeExceptionKind(exception)));
        if (eventId == NpgsqlEventId.OpeningConnection) Interlocked.Increment(ref request.OpenAttempts);
        if (eventId == NpgsqlEventId.OpenedConnection)
        {
            Interlocked.Increment(ref request.OpenedConnections);
            int active = Interlocked.Increment(ref activeConnections);
            int previous;
            do { previous = maximumConnections; }
            while (active > previous && Interlocked.CompareExchange(ref maximumConnections, active, previous) != previous);
        }
        if (eventId == NpgsqlEventId.ClosedConnection)
        {
            Interlocked.Increment(ref request.ClosedConnections);
            Interlocked.Decrement(ref activeConnections);
        }
    }

    private sealed class ProviderEvents : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => categoryName is "Npgsql.Connection" or "Npgsql.Command"
            ? new ProviderLogger(categoryName) : NullLogger.Instance;
        public void Dispose() { }
    }

    private sealed class ProviderLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            int index = Array.IndexOf(EventIds, eventId.Id);
            if (index < 0) return;
            // NEVER inspect state, formatter, EventId.Name, parameter values or exception messages.
            Interlocked.Increment(ref AggregateCounts[index]);
            if (Current.Value is { } scope) scope.Owner.Record(scope.Request, category, eventId.Id, exception);
        }
    }

    private static string Category(HttpObservation request)
    {
        if (request.Status != StatusCodes.Status503ServiceUnavailable)
            return request.ExceptionKind == "CANCELLED" ? "CANCELLED" : request.ExceptionKind == "NONE" ? "NONE" : "UNKNOWN";
        ReadinessActivityCapture.ReadinessEvent? decision = Event(request, "sitefactory.readiness.http.decision");
        ReadinessActivityCapture.ReadinessEvent? refresh = Event(request, "sitefactory.readiness.refresh.completed");
        // Only causal events, never lifecycle snapshots, CurrentReport or provider event absence.
        return decision?.Tags.GetValueOrDefault("outcome") switch
        {
            "HOST_NOT_STARTED" => "HOST_NOT_STARTED",
            "HOST_STOPPING" => "HOST_STOPPING",
            "REFRESH_REJECTED" => refresh?.Tags.GetValueOrDefault("outcome") switch
            {
                "QUEUE_TIMEOUT" => "QUEUE_TIMEOUT",
                "BLOCKED" => "PROBE_BLOCKED",
                "NOT_INITIALIZED" => "NOT_INITIALIZED",
                _ => "UNKNOWN",
            },
            "THREW" => refresh?.Tags.GetValueOrDefault("outcome") switch
            {
                "CANCELLED" => "CANCELLED",
                "UNEXPECTED_ERROR" => "UNEXPECTED_ERROR",
                _ => "UNKNOWN",
            },
            _ => "UNKNOWN",
        };
    }

    private static ReadinessActivityCapture.ReadinessEvent? Event(HttpObservation request, string name) =>
        request.ServerActivity && request.TraceId is not null && request.SpanId is not null && request.ReadinessEvents.Length is 1 or 2
        && request.ReadinessEvents.All(item => item.TraceId == request.TraceId && item.SpanId == request.SpanId)
        && request.ReadinessEvents.Count(item => item.Name == name) == 1
            ? request.ReadinessEvents.SingleOrDefault(item => item.Name == name) : null;

    private static object RefreshSummary(HttpObservation request)
    {
        ReadinessActivityCapture.ReadinessEvent? refresh = Event(request, "sitefactory.readiness.refresh.completed");
        object? outcome = refresh?.Tags.GetValueOrDefault("outcome");
        return new
        {
            startTimestamp = refresh?.Tags.GetValueOrDefault("startTicks"),
            endTimestamp = refresh?.Tags.GetValueOrDefault("endTicks"),
            result = refresh is null ? "UNKNOWN" : refresh.Tags.GetValueOrDefault("result"),
            outcome = outcome ?? "UNKNOWN",
            semaphoreTimeout = outcome is "QUEUE_TIMEOUT" ? (object)true : outcome is "READY" or "BLOCKED" ? false : "UNKNOWN",
            readerOutcome = "UNKNOWN",
            probeOutcome = outcome is "READY" ? "COMPATIBLE" : outcome is "BLOCKED" ? "BLOCKED" : "UNKNOWN",
            diagnosticCode = refresh?.Tags.GetValueOrDefault("diagnosticCode"),
        };
    }

    internal string SafeJson()
    {
        HttpObservation[] observations = requests.OfType<HttpObservation>().OrderBy(static request => request.RequestId).ToArray();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            correction = "P03-CORR-004",
            stopwatchFrequency = Stopwatch.Frequency,
            workers = 32,
            requestsPerWorker = 24,
            http = observations.Select(static request => new
            {
                request.RequestId,
                request.Route,
                request.StartTimestamp,
                request.EndTimestamp,
                request.DurationMilliseconds,
                request.CompletionOrder,
                request.Status,
                request.ExceptionKind,
                request.ServerStartTimestamp,
                request.ServerEndTimestamp,
                request.ServerExceptionKind,
                request.StartedBefore,
                request.StartedAfter,
                request.StoppingBefore,
                request.StoppingAfter,
                request.RequestAborted,
                request.OpenAttempts,
                request.OpenedConnections,
                request.ClosedConnections,
                request.TraceId,
                request.SpanId,
                request.ServerActivity,
                request.ReadinessEvents,
                refresh = RefreshSummary(request),
                refusalCategory = Category(request),
            }),
            routes = observations.GroupBy(static request => request.Route).OrderBy(static group => group.Key, StringComparer.Ordinal).Select(static group => new
            {
                route = group.Key,
                requests = group.Count(),
                http200 = group.Count(static request => request.Status == 200),
                http503 = group.Count(static request => request.Status == 503),
                durationsMilliseconds = Distribution(group.Where(static request => request.EndTimestamp != 0).Select(static request => request.DurationMilliseconds)),
            }),
            refusalCategories = observations.Where(static request => request.Status == 503 || request.ExceptionKind != "NONE")
                .GroupBy(Category).OrderBy(static group => group.Key, StringComparer.Ordinal).Select(static group => new { category = group.Key, count = group.Count() }),
            providerEvents = events.OrderBy(static item => item.Timestamp),
            providerStatistics = new
            {
                maximumRequestCorrelatedConnections = maximumConnections,
                remainingRequestCorrelatedConnections = activeConnections,
                droppedEvents,
                aggregateScope = "All Npgsql activity in this test process; uncorrelated events are aggregate-only, not attributed to a request.",
                aggregateCounts = EventIds.Select((eventId, index) => new { eventId, count = Volatile.Read(ref AggregateCounts[index]) }),
            },
            lifecycle = lifecycle.OrderBy(static item => item.Timestamp),
            resources = new { before, after = Resources() },
            limitations = Limitations,
        }, Json);
    }

    internal void Save(ITestOutputHelper output)
    {
        // Batched after Task.WhenAll (or in its finally), never disk I/O per request.
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "observations.json"), SafeJson(), new UTF8Encoding(false, true));
        output.WriteLine("P03-CORR-004 observations: requests={0} HTTP200={1} HTTP503={2} droppedEvents={3}; attribution from own Activity events only.",
            requests.Count(static request => request is not null), requests.Count(static request => request?.Status == 200),
            requests.Count(static request => request?.Status == 503), droppedEvents);
        output.WriteLine("P03-CORR-004 artifact: artifacts/p03-f1-observability/{0}/observations.json", Path.GetFileName(directory));
    }

    internal void AssertSuccessfulCoverage()
    {
        Assert.Equal(RequestCount, requests.Count(static request => request is not null));
        Assert.Equal(0, droppedEvents);
        Assert.All(requests, static request =>
        {
            Assert.NotNull(request);
            Assert.True(request.EndTimestamp >= request.StartTimestamp && request.StartTimestamp > 0);
            Assert.True(request.ServerStartTimestamp > 0);
            string site = request.Route switch { "/health/ready" => "HEALTH_READY", "/health" => "HEALTH_LEGACY", _ => "TRAFFIC_GATE" };
            ReadinessActivityCapture.AssertDecision(new(request.TraceId, request.SpanId, request.ServerActivity),
                request.ReadinessEvents, site, "READY", true);
            ReadinessActivityCapture.ReadinessEvent refresh = Event(request, "sitefactory.readiness.refresh.completed")!;
            Assert.True((long)refresh.Tags["startTicks"]! >= request.ServerStartTimestamp
                && (long)refresh.Tags["endTicks"]! <= request.ServerEndTimestamp);
            Assert.Equal(1, request.OpenAttempts);
            Assert.Equal(1, request.OpenedConnections);
            Assert.Equal(1, request.ClosedConnections);
        });
        Assert.Equal(1, maximumConnections);
        Assert.Equal(0, activeConnections);
    }

    private static object Distribution(IEnumerable<double> durations)
    {
        double[] values = durations.Order().ToArray();
        return new
        {
            count = values.Length,
            min = values.Length == 0 ? (double?)null : values[0],
            max = values.Length == 0 ? (double?)null : values[^1],
            p50 = Percentile(values, .50),
            p95 = Percentile(values, .95),
            p99 = Percentile(values, .99)
        };
    }

    private static double? Percentile(double[] sorted, double percentile) => sorted.Length == 0
        ? null : sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];

    private static object Resources()
    {
        using Process process = Process.GetCurrentProcess();
        return new
        {
            timestamp = Stopwatch.GetTimestamp(),
            processId = process.Id,
            workingSetBytes = process.WorkingSet64,
            totalProcessorMilliseconds = process.TotalProcessorTime.TotalMilliseconds,
            logicalProcessors = Environment.ProcessorCount,
            threadPoolThreads = ThreadPool.ThreadCount,
            pendingWorkItems = ThreadPool.PendingWorkItemCount
        };
    }

    private static string CampaignDirectory()
    {
        string? supplied = Environment.GetEnvironmentVariable("SITEFACTORY_P03_OBSERVABILITY_RUN");
        string id = supplied is null ? Guid.NewGuid().ToString("N")
            : Guid.TryParseExact(supplied, "N", out Guid run) ? run.ToString("N")
            : throw new InvalidOperationException("The P03 observation run must be an opaque test-only identifier.");
        for (DirectoryInfo? root = new(AppContext.BaseDirectory); root is not null; root = root.Parent)
            if (File.Exists(Path.Combine(root.FullName, "SiteFactory.slnx")))
                return Path.Combine(root.FullName, "artifacts", "p03-f1-observability", id);
        throw new InvalidOperationException("The P03 repository root is unavailable.");
    }

    public void Dispose()
    {
        activityListener.Dispose();
        foreach (CancellationTokenRegistration registration in registrations) registration.Dispose();
    }

    internal sealed class HttpObservation
    {
        public int RequestId { get; init; }
        public string Route { get; init; } = "";
        public long StartTimestamp { get; init; }
        public long EndTimestamp { get; set; }
        public double DurationMilliseconds => EndTimestamp == 0 ? 0 : (EndTimestamp - StartTimestamp) * 1000d / Stopwatch.Frequency;
        public int CompletionOrder { get; set; }
        public int? Status { get; set; }
        public string ExceptionKind { get; set; } = "NONE";
        public long ServerStartTimestamp { get; set; }
        public long ServerEndTimestamp { get; set; }
        public string ServerExceptionKind { get; set; } = "UNKNOWN";
        public bool? StartedBefore { get; set; }
        public bool? StartedAfter { get; set; }
        public bool? StoppingBefore { get; set; }
        public bool? StoppingAfter { get; set; }
        public bool RequestAborted { get; set; }
        public int OpenAttempts;
        public int OpenedConnections;
        public int ClosedConnections;
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
        public bool ServerActivity { get; set; }
        public ReadinessActivityCapture.ReadinessEvent[] ReadinessEvents { get; set; } = [];
    }

    private sealed record ProviderObservation(int RequestId, string Category, int EventId, long Timestamp, string ExceptionKind);
    private sealed record LifecycleObservation(string Kind, long Timestamp);
}
