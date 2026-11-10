using System.Net;
using System.Text;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

public sealed class RuntimeHealthTests : IAsyncLifetime
{
    private readonly OfflineDatabaseFixture secrets = new();
    private readonly TestHosts hosts = new();
    private string directory => hosts.DirectoryPath;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task RealHostReturnsOnlyStablePublicStatusWithoutCaching(string route)
    {
        WebApplication application = hosts.CreateWeb(Arguments(), secrets.ConfigureDatabaseServices);
        await application.StartAsync(Token);
        try
        {
            RuntimeStartupService startup = application.Services.GetRequiredService<RuntimeStartupService>();
            RuntimeStartupReport? report = startup.Report;
            Assert.True(startup.IsReady);
            using HttpClient client = hosts.Client(application);
            for (int index = 0; index < 2; index++)
            {
                using HttpResponseMessage response = await client.GetAsync(route, Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
                Assert.Equal("{\"status\":\"healthy\"}", await response.Content.ReadAsStringAsync(Token));
            }
            Assert.Same(report, startup.Report); // Probes must not execute startup validation again.
            using HttpResponseMessage legacy = await client.GetAsync("/health", Token);
            Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
            Assert.Equal("no-store", legacy.Headers.CacheControl?.ToString());
            Assert.Equal("Healthy", await legacy.Content.ReadAsStringAsync(Token));
        }
        finally
        {
            await application.StopAsync(Token);
        }
        Assert.False(application.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task PendingStartupAndValidationAloneNeverMeanReady()
    {
        WebApplication application = hosts.CreateWeb(Arguments(), secrets.ConfigureDatabaseServices);
        RuntimeStartupService startup = application.Services.GetRequiredService<RuntimeStartupService>();
        Assert.Null(startup.Report);
        Assert.False(startup.IsReady);
        await AssertProbeAsync(application, "/health/live", 200, "{\"status\":\"healthy\"}");
        await AssertProbeAsync(application, "/health/ready", 503, "{\"status\":\"unavailable\"}");

        // Exercise the actual pipeline without starting Kestrel or signalling ApplicationStarted.
        await startup.StartingAsync(Token);
        Assert.True(startup.Report!.IsReady);
        Assert.False(startup.IsReady);
        await AssertProbeAsync(application, "/health/ready", 503, "{\"status\":\"unavailable\"}");
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task BlockedRealStartupKeepsReadinessFalseWithoutPublishingDiagnostics()
    {
        WebApplication application = hosts.CreateWeb(Arguments(slug: ""), secrets.ConfigureDatabaseServices);
        RuntimeStartupService startup = application.Services.GetRequiredService<RuntimeStartupService>();
        RuntimeStartupException error = await Assert.ThrowsAsync<RuntimeStartupException>(() => application.StartAsync(Token));
        Assert.Contains(error.Report.Results, static result => result.ErrorCode == "SF-CONFIG-STARTUP-003");
        Assert.False(startup.IsReady);
        Assert.False(application.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
        // Fail-fast means no listener. Invoke the real mapped middleware delegates for the negative HTTP contract.
        await AssertProbeAsync(application, "/health/live", 200, "{\"status\":\"healthy\"}");
        await AssertProbeAsync(application, "/health/ready", 503, "{\"status\":\"unavailable\"}");
        await AssertProbeAsync(application, "/health", 503, "Unhealthy");
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task LivenessNeverInvokesReadinessChecksAndUnhealthyResponsesNeverExposeDetails()
    {
        SensitiveCheck check = new();
        WebApplication application = hosts.CreateWeb(Arguments(),
            services => services.AddHealthChecks().AddCheck("private-check", check, tags: ["ready"]), withDatabase: false);
        await application.StartAsync(Token);
        try
        {
            using HttpClient client = hosts.Client(application);
            using HttpResponseMessage live = await client.GetAsync("/health/live", Token);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal("{\"status\":\"healthy\"}", await live.Content.ReadAsStringAsync(Token));
            Assert.Equal("no-store", live.Headers.CacheControl?.ToString());
            Assert.Equal(0, check.Calls);

            using HttpResponseMessage ready = await client.GetAsync("/health/ready", Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());
            Assert.Equal("{\"status\":\"unavailable\"}", await ready.Content.ReadAsStringAsync(Token));
            Assert.Equal(1, check.Calls);
        }
        finally
        {
            await application.StopAsync(Token);
        }
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task WorkerReadinessIsInternalAndFollowsStartupAndShutdown()
    {
        IHost worker = hosts.CreateWorker(Arguments(), secrets.ConfigureDatabaseServices);
        Assert.Null(worker.Services.GetService<WebDatabaseReadiness>());
        Assert.True(worker.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IRuntimeDatabaseReader)));
        RuntimeStartupService startup = worker.Services.GetRequiredService<RuntimeStartupService>();
        Assert.False(startup.IsReady);
        Assert.Null(startup.Report);
        await worker.StartAsync(Token);
        try
        {
            Assert.True(startup.IsReady);
            worker.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            Assert.False(startup.IsReady);
            Assert.True(startup.Report!.IsReady); // The successful historical report is not current readiness.
        }
        finally
        {
            await worker.StopAsync(Token);
        }
        Assert.False(startup.IsReady);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    private static async Task AssertProbeAsync(WebApplication application, string route, int status, string body)
    {
        RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)application).DataSources
            .SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText == route);
        using MemoryStream stream = new();
        DefaultHttpContext context = new() { RequestServices = application.Services, RequestAborted = Token };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = route;
        context.Response.Body = stream;
        await endpoint.RequestDelegate!(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(body, Encoding.UTF8.GetString(stream.ToArray()));
    }

    private string[] Arguments(string slug = "client-test") => secrets.Arguments(["--contentRoot", directory, "--environment", "Development",
        "--urls", "http://127.0.0.1:0", "--SiteFactory:Runtime:ClientSlug", slug,
        "--SiteFactory:Runtime:PublicBaseUrl", "http://localhost"]);

    // Synthetic sensitive details prove that even a failing future check cannot leak its report through this writer.
    private sealed class SensitiveCheck : IHealthCheck
    {
        public int Calls { get; private set; }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(HealthCheckResult.Unhealthy("PRIVATE-DIAGNOSTIC-SENTINEL",
                new InvalidOperationException("PRIVATE-EXCEPTION-SENTINEL"),
                new Dictionary<string, object> { ["ClientSlug"] = "PRIVATE-CLIENT-SENTINEL", ["path"] = Path.GetTempPath() }));
        }
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        try { await hosts.DisposeAsync(); }
        finally { secrets.Dispose(); }
    }
}
