using System.Diagnostics;
using System.Net;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

public sealed class WebDatabaseReadinessTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" role ")]
    [InlineData("bad\nrole")]
    public async Task ExpectedRoleIsRequiredBeforeNetworkIo(string login)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader, ["--" + WebDatabaseReadiness.ExpectedRoleKey, login]);
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => web.StartAsync(Token));
        Assert.Contains(error.Report.Results, static result => result.PropertyPath == "PostgreSql.ExpectedRole");
        Assert.Equal(0, reader.Calls);
        Assert.False(web.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
    }

    [Fact]
    public async Task StructuralFailureNeverInvokesTheDatabasePort()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader, ["--SiteFactory:Runtime:ClientSlug", ""]);
        await Assert.ThrowsAsync<RuntimeStartupException>(() => web.StartAsync(Token));
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task UnavailableDatabaseKeepsHttpLiveButBlocksReadinessAndApplicationTraffic()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new() { Unavailable = true };
        WebApplication web = Create(hosts, secrets, reader);
        await web.StartAsync(Token);
        try
        {
            Assert.False(web.Services.GetRequiredService<RuntimeStartupService>().IsReady);
            Assert.Contains(web.Services.GetRequiredService<RuntimeStartupService>().Report!.Results,
                static result => result.ErrorCode == "SF-DB-STARTUP-001");
            using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
            int beforeLive = reader.Calls;
            await HttpAsync(http, "/health/live", true, Token);
            Assert.Equal(beforeLive, reader.Calls);
            await HttpAsync(http, "/health/ready", false, Token);
            await HttpAsync(http, "/", false, Token);
            reader.Unavailable = false; // Recovery still requires a fresh successful read, never an optimistic flag.
            await HttpAsync(http, "/health/ready", true, Token);
            Assert.True(web.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        }
        finally { await web.StopAsync(Token); }
    }

    [Fact]
    public async Task APreviouslySuccessfulStartupDoesNotHideDatabaseOrRoleDrift()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader);
        await web.StartAsync(Token);
        try
        {
            RuntimeStartupService startup = web.Services.GetRequiredService<RuntimeStartupService>();
            RuntimeStartupReport? original = startup.Report;
            using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
            await HttpAsync(http, "/health/ready", true, Token);
            reader.Excessive = true;
            await HttpAsync(http, "/health/ready", false, Token);
            await HttpAsync(http, "/", false, Token);
            Assert.False(startup.IsReady);
            Assert.True(original!.IsReady); // Historical report is intentionally not current readiness.
            Assert.Same(original, startup.Report);
            Assert.Equal("SF-DB-STARTUP-008", Assert.Single(web.Services.GetRequiredService<WebDatabaseReadiness>().CurrentReport!.Results).ErrorCode);
            await HttpAsync(http, "/health/live", true, Token);
        }
        finally { await web.StopAsync(Token); }
    }

    [Fact]
    public async Task RefreshCancellationIsPropagatedAndKeepsTheLastCompleteReport()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader);
        await web.StartAsync(Token);
        try
        {
            WebDatabaseReadiness readiness = web.Services.GetRequiredService<WebDatabaseReadiness>();
            RuntimeStartupReport? previous = readiness.CurrentReport;
            reader.Block = true;
            using CancellationTokenSource cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
            Task<bool> refresh = web.Services.GetRequiredService<WebDatabaseReadiness>().RefreshAsync(cancelled.Token);
            await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
            await HttpAsync(http, "/health/live", true, Token); // Listener remains usable while the DB check is blocked.
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
            Assert.Same(previous, readiness.CurrentReport);
            Assert.True(web.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        }
        finally { await web.StopAsync(Token); }
    }

    [Theory]
    [InlineData("/health/ready", false)]
    [InlineData("/health/ready", true)]
    [InlineData("/health", false)]
    [InlineData("/health", true)]
    [InlineData("/", false)]
    [InlineData("/", true)]
    public async Task CompletedRequestUsesItsOwnResultWhileAnotherProbeRuns(string route, bool finishIncompatible)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        using ReadinessActivityCapture traces = new();
        SequencedReader reader = new();
        PausedContinuation pause = new();
        WebApplication web = hosts.CreateWeb(secrets.Arguments(Arguments("example-client", "Test")), services =>
        {
            services.AddSingleton<IRuntimeDatabaseReader>(provider =>
            {
                reader.Identity = provider.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration!.Identity;
                return reader;
            });
            // Schedule the real middleware/check continuation; never replace its decision.
            if (route == "/") services.AddSingleton<IStartupFilter>(new SchedulingFilter(pause));
            else services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                HealthCheckRegistration registration = Assert.Single(options.Registrations, static item => item.Name == "startup");
                options.Registrations.Remove(registration);
                options.Registrations.Add(new(registration.Name,
                    provider => new ScheduledCheck(registration.Factory(provider), pause),
                    registration.FailureStatus, registration.Tags, registration.Timeout));
            });
        }, readinessObservations: traces);
        await web.StartAsync(Token);
        try
        {
            using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
            Task<HttpResponseMessage> first = SendObservedAsync(http, route, 0);
            await reader.FirstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            await pause.Registered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            reader.FirstRelease.TrySetResult();
            // A's probe is finished and published, but its HTTP decision has not resumed.
            Action continuation = await pause.Posted.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            WebDatabaseReadiness readiness = web.Services.GetRequiredService<WebDatabaseReadiness>();
            Assert.True(readiness.CurrentReport!.IsReady);
            Task<HttpResponseMessage> second = SendObservedAsync(http, route, 1);
            await reader.SecondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.False(second.IsCompleted);
            if (finishIncompatible)
            {
                reader.SecondRelease.TrySetResult(new(DatabaseReadOutcome.Unavailable));
                using HttpResponseMessage failed = await second;
                Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
                Assert.False(readiness.IsReady);
            }
            continuation();
            using HttpResponseMessage succeeded = await first;
            Assert.Equal(HttpStatusCode.OK, succeeded.StatusCode);
            string body = await succeeded.Content.ReadAsStringAsync(Token);
            if (route == "/") Assert.Equal("runtime-host-test", body);
            else Assert.Equal(route == "/health" ? "Healthy" : "{\"status\":\"healthy\"}", body);
            if (!finishIncompatible)
            {
                Assert.True(readiness.IsReady); // Checking is not a published failure.
                reader.SecondRelease.TrySetResult(OfflineDatabaseFixture.Compatible(reader.Identity));
                using HttpResponseMessage completed = await second;
                Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            }
            ReadinessActivityCapture.RequestTrace a = await traces.CompletedAsync(0, Token);
            ReadinessActivityCapture.RequestTrace b = await traces.CompletedAsync(1, Token);
            Assert.NotEqual(a.SpanId, b.SpanId);
            ReadinessActivityCapture.AssertDecision(a, a.Events, Site(route), "READY", true);
            ReadinessActivityCapture.AssertDecision(b, b.Events, Site(route), finishIncompatible ? "BLOCKED" : "READY",
                !finishIncompatible, finishIncompatible ? "SF-DB-STARTUP-001" : null);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                ReadinessActivityCapture.AssertDecision(a, b.Events, Site(route), "READY", true));
        }
        finally
        {
            reader.FirstRelease.TrySetResult();
            reader.SecondRelease.TrySetResult(new(DatabaseReadOutcome.Unavailable));
            if (pause.Posted.Task.IsCompletedSuccessfully) (await pause.Posted.Task)();
            await web.StopAsync(Token);
        }
    }

    [Theory]
    [InlineData("/health/ready", true)]
    [InlineData("/health/ready", false)]
    [InlineData("/health", true)]
    [InlineData("/health", false)]
    [InlineData("/", true)]
    [InlineData("/", false)]
    public async Task ReadinessEventsIdentifyOwnDecisionOnEveryRealHttpRoute(string route, bool ready)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        using ReadinessActivityCapture traces = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader, traces: traces);
        await web.StartAsync(Token);
        reader.Excessive = !ready;
        using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
        using HttpResponseMessage response = await SendObservedAsync(http, route, 0);
        Assert.Equal(ready ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        ReadinessActivityCapture.RequestTrace trace = await traces.CompletedAsync(0, Token);
        string outcome = ready ? "READY" : "BLOCKED";
        string? code = ready ? null : "SF-DB-STARTUP-008";
        ReadinessActivityCapture.AssertDecision(trace, trace.Events, Site(route), outcome, ready, code);
        // Strict negative assertions, not production mutations or a reproduction of historical F1.
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            ReadinessActivityCapture.AssertDecision(trace, trace.Events.Skip(1).ToArray(), Site(route), outcome, ready, code));
        ReadinessActivityCapture.ReadinessEvent[] wrong = trace.Events.Select(item => item.Name.EndsWith("refresh.completed", StringComparison.Ordinal)
            ? item with { Tags = new(item.Tags, StringComparer.Ordinal) { ["outcome"] = ready ? "BLOCKED" : "READY", ["result"] = !ready } }
            : item).ToArray();
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            ReadinessActivityCapture.AssertDecision(trace, wrong, Site(route), outcome, ready, code));
        secrets.AssertRedacted(System.Text.Json.JsonSerializer.Serialize(trace.Events));
        Assert.Equal(2, reader.Calls);
    }

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    [InlineData("/")]
    public async Task QueueTimeoutProducesCausallyMatched503WithoutAnotherReaderCall(string route)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        using ReadinessActivityCapture traces = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader, traces: traces);
        await web.StartAsync(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Read = async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return OfflineDatabaseFixture.Compatible(reader.Identity);
        };
        using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
        Task<HttpResponseMessage> active = SendObservedAsync(http, route, 0);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            using HttpResponseMessage queued = await SendObservedAsync(http, route, 1);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, queued.StatusCode);
            Assert.False(active.IsCompleted);
            Assert.Equal(2, reader.Calls); // Startup + A, never B.
            Assert.Equal(TimeSpan.FromSeconds(5), PostgreSqlDatabaseReader.MaximumDuration);
            ReadinessActivityCapture.RequestTrace b = await traces.CompletedAsync(1, Token);
            ReadinessActivityCapture.AssertDecision(b, b.Events, Site(route), "QUEUE_TIMEOUT", false);
            ReadinessActivityCapture.ReadinessEvent[] wrong = b.Events.Select(item => item.Name.EndsWith("refresh.completed", StringComparison.Ordinal)
                ? item with { Tags = new(item.Tags, StringComparer.Ordinal) { ["outcome"] = "BLOCKED" } } : item).ToArray();
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                ReadinessActivityCapture.AssertDecision(b, wrong, Site(route), "QUEUE_TIMEOUT", false));
        }
        finally { release.TrySetResult(); }
        using HttpResponseMessage completed = await active;
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        ReadinessActivityCapture.RequestTrace a = await traces.CompletedAsync(0, Token);
        ReadinessActivityCapture.AssertDecision(a, a.Events, Site(route), "READY", true);
    }

    private static string Site(string route) => route switch
    {
        "/health/ready" => "HEALTH_READY",
        "/health" => "HEALTH_LEGACY",
        "/" => "TRAFFIC_GATE",
        _ => throw new ArgumentException("Only fixed test routes are supported.", nameof(route)),
    };

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task ThrownCheckEventIsDistinctFromTheFrameworksFinal503(string route)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        using ReadinessActivityCapture traces = new();
        ControlledReader reader = new();
        WebApplication web = Create(hosts, secrets, reader, traces: traces);
        await web.StartAsync(Token);
        reader.Read = _ => throw new InvalidOperationException("PRIVATE-CORR004-ERROR");
        using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
        using HttpResponseMessage response = await SendObservedAsync(http, route, 0);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(route == "/health" ? "Unhealthy" : "{\"status\":\"unavailable\"}", await response.Content.ReadAsStringAsync(Token));
        ReadinessActivityCapture.RequestTrace trace = await traces.CompletedAsync(0, Token);
        Assert.True(trace.ServerActivity);
        Assert.Equal(2, trace.Events.Length);
        ReadinessActivityCapture.ReadinessEvent refresh = Assert.Single(trace.Events, static item => item.Name.EndsWith("refresh.completed", StringComparison.Ordinal));
        Assert.Equal("UNEXPECTED_ERROR", refresh.Tags["outcome"]);
        Assert.Null(refresh.Tags["result"]);
        Assert.Null(refresh.Tags["diagnosticCode"]);
        ReadinessActivityCapture.ReadinessEvent decision = Assert.Single(trace.Events, static item => item.Name.EndsWith("http.decision", StringComparison.Ordinal));
        Assert.Equal("THREW", decision.Tags["outcome"]);
        Assert.Null(decision.Tags["refreshResult"]);
        Assert.Equal(Site(route), decision.Tags["site"]);
        Assert.All(trace.Events, item => { Assert.Equal(trace.TraceId, item.TraceId); Assert.Equal(trace.SpanId, item.SpanId); });
        Assert.DoesNotContain("PRIVATE-CORR004-ERROR", System.Text.Json.JsonSerializer.Serialize(trace.Events), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendObservedAsync(HttpClient http, string route, int id)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, route);
        request.Headers.Add(ReadinessActivityCapture.RequestHeader, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return await http.SendAsync(request, Token);
    }

    [Fact]
    public async Task CompatibleRealHttpPipelineHasNoFalseFailuresUnderConcurrentTraffic()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new() { Yield = true };
        WebApplication web = Create(hosts, secrets, reader);
        await web.StartAsync(Token);
        try
        {
            using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
            string[] routes = ["/health/ready", "/health", "/"];
            // 32 concurrent clients, 768 real Kestrel requests, with asynchronous reader completion.
            Stopwatch measurement = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ =>
            {
                for (int request = 0; request < 24; request++)
                {
                    using HttpResponseMessage response = await http.GetAsync(routes[request % routes.Length], Token);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }));
            measurement.Stop();
            output.WriteLine("Stress: requests=768, concurrency=32, status200=768, reads={0}, elapsedMilliseconds={1}", reader.Calls, measurement.Elapsed.TotalMilliseconds);
            Assert.Equal(769, reader.Calls); // Includes startup. No cached success bypasses a check.
        }
        finally { await web.StopAsync(Token); }
    }

    [Fact]
    public async Task CancelledQueuedCheckNeverPublishesSuccessOrErasesAFailure()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        ControlledReader reader = new() { Unavailable = true };
        WebApplication web = Create(hosts, secrets, reader);
        await web.StartAsync(Token);
        try
        {
            WebDatabaseReadiness readiness = web.Services.GetRequiredService<WebDatabaseReadiness>();
            RuntimeStartupReport? previous = readiness.CurrentReport;
            reader.Block = true;
            using CancellationTokenSource active = CancellationTokenSource.CreateLinkedTokenSource(Token);
            using CancellationTokenSource queued = CancellationTokenSource.CreateLinkedTokenSource(Token);
            Task<bool> first = readiness.RefreshAsync(active.Token);
            await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Task<bool> second = readiness.RefreshAsync(queued.Token);
            await queued.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
            Assert.Equal(2, reader.Calls);
            await active.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.Same(previous, readiness.CurrentReport);
            Assert.False(readiness.IsReady);
        }
        finally { await web.StopAsync(Token); }
    }

    private sealed class SequencedReader : IRuntimeDatabaseReader
    {
        private int calls;
        public RuntimeIdentity Identity { get; set; } = null!;
        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<RuntimeDatabaseObservation> SecondRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 2)
            {
                FirstEntered.TrySetResult();
                await FirstRelease.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
            if (call == 3)
            {
                SecondEntered.TrySetResult();
                return await SecondRelease.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
            return OfflineDatabaseFixture.Compatible(Identity);
        }
    }

    private sealed class ScheduledCheck(IHealthCheck inner, PausedContinuation pause) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!pause.TryArm()) return inner.CheckHealthAsync(context, cancellationToken);
            SynchronizationContext? original = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(pause);
                Task<HealthCheckResult> result = inner.CheckHealthAsync(context, cancellationToken);
                pause.Registered.TrySetResult(); // The real check has installed its await continuation.
                return result;
            }
            finally { SynchronizationContext.SetSynchronizationContext(original); }
        }
    }

    private sealed class SchedulingFilter(PausedContinuation pause) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
        {
            builder.Use(nextRequest => context =>
            {
                if (!pause.TryArm()) return nextRequest(context);
                SynchronizationContext? original = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(pause);
                    Task result = nextRequest(context);
                    pause.Registered.TrySetResult(); // The real gate has installed its await continuation.
                    return result;
                }
                finally { SynchronizationContext.SetSynchronizationContext(original); }
            });
            next(builder);
        };
    }

    private sealed class PausedContinuation : SynchronizationContext
    {
        private int armed;
        public TaskCompletionSource Registered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Action> Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryArm() => Interlocked.Exchange(ref armed, 1) == 0;
        public override void Post(SendOrPostCallback callback, object? state)
        {
            int resumed = 0;
            Posted.SetResult(() => { if (Interlocked.Exchange(ref resumed, 1) == 0) callback(state); });
        }
    }

    private static WebApplication Create(TestHosts hosts, OfflineDatabaseFixture secrets, ControlledReader reader, string[]? extra = null,
        ReadinessActivityCapture? traces = null) =>
        hosts.CreateWeb([.. secrets.Arguments(Arguments("example-client", "Test")), .. extra ?? []],
            services => services.AddSingleton<IRuntimeDatabaseReader>(provider =>
            {
                reader.Identity = provider.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration!.Identity;
                return reader;
            }), readinessObservations: traces);

    private static string[] Arguments(string client, string environment) => ["--urls", "http://127.0.0.1:0", "--environment", environment,
        "--SiteFactory:Runtime:ClientSlug", client, "--SiteFactory:Runtime:PublicBaseUrl", "https://example.invalid"];

    private static async Task HttpAsync(HttpClient http, string path, bool ready, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await http.GetAsync(path, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string expected = ready ? "{\"status\":\"healthy\"}" : "{\"status\":\"unavailable\"}";
        Assert.True(response.StatusCode == (ready ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable), "Unexpected public HTTP status.");
        Assert.True(body == expected, "Health/application failure response disclosed unexpected data.");
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    private sealed class ControlledReader : IRuntimeDatabaseReader
    {
        public RuntimeIdentity Identity { get; set; } = null!;
        public int Calls { get; private set; }
        public bool Unavailable { get; set; }
        public bool Excessive { get; set; }
        public bool Block { get; set; }
        public bool Yield { get; set; }
        public Func<CancellationToken, Task<RuntimeDatabaseObservation>>? Read { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (Read is not null) return await Read(cancellationToken);
            if (Yield) await Task.Yield();
            if (Block)
            {
                Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeDatabaseObservation valid = OfflineDatabaseFixture.Compatible(Identity);
            return Unavailable ? new(DatabaseReadOutcome.Unavailable)
                : Excessive ? new(DatabaseReadOutcome.Read, valid.Role! with { Privileges = DatabaseRolePrivileges.CreateRole }, valid.Identities, valid.Schemas, valid.Migrations)
                : valid;
        }
    }

    // P03 invokes this existing bridge with the real reader and Kestrel; no service replacement or provisioning.
    public static async Task<string[]> ExerciseRealAsync(string file, string login, string client, string environment,
        bool expectedReady, Func<Task>? afterStartup, CancellationToken cancellationToken)
    {
        await using TestHosts hosts = new();
        WebApplication web = hosts.CreateWeb([.. Arguments(client, environment),
            "--SITEFACTORY_WEB_CONNECTION_FILE", file, "--" + WebDatabaseReadiness.ExpectedRoleKey, login]);
        await web.StartAsync(cancellationToken);
        try
        {
            Assert.IsType<PostgreSqlDatabaseReader>(web.Services.GetRequiredService<IRuntimeDatabaseReader>());
            if (afterStartup is not null)
            {
                Assert.True(web.Services.GetRequiredService<RuntimeStartupService>().IsReady);
                await afterStartup();
            }
            using HttpClient http = hosts.Client(web, TimeSpan.FromSeconds(12));
            await HttpAsync(http, "/health/live", true, cancellationToken);
            await HttpAsync(http, "/health/ready", expectedReady, cancellationToken);
            using (HttpResponseMessage legacy = await http.GetAsync("/health", cancellationToken))
            {
                Assert.Equal(expectedReady ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, legacy.StatusCode);
                Assert.Equal(expectedReady ? "Healthy" : "Unhealthy", await legacy.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("no-store", legacy.Headers.CacheControl?.ToString());
            }
            if (!expectedReady) await HttpAsync(http, "/", false, cancellationToken);
            else
            {
                using HttpResponseMessage response = await http.GetAsync("/", cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("runtime-host-test", await response.Content.ReadAsStringAsync(cancellationToken));
            }
            Assert.Equal(expectedReady, web.Services.GetRequiredService<RuntimeStartupService>().IsReady);
            await PostgreSql.RealPostgreSqlHostFixture.AssertRedactedAsync(file,
                string.Join("\n", hosts.LogMessages) + string.Join("\n", web.Services.GetRequiredService<RuntimeStartupService>().Report!.Results)
                    + string.Join("\n", web.Services.GetRequiredService<WebDatabaseReadiness>().CurrentReport!.Results), cancellationToken);
            return web.Services.GetRequiredService<WebDatabaseReadiness>().CurrentReport!.Results
                .Select(static result => result.ErrorCode ?? "PASS").ToArray();
        }
        finally
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
            await web.StopAsync(cleanup.Token);
        }
    }
}
