using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.AspNetCore.Tests;

public sealed class RuntimeHealthEndpointsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExactlyThreeRoutesPreserveTheirMethodMetadataAndRegistration()
    {
        await using TestHttpFixture fixture = new();
        Assert.Equal(["/health", "/health/live", "/health/ready"], ((IEndpointRouteBuilder)fixture.Application).DataSources
            .SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText).Order(StringComparer.Ordinal));
        foreach (string route in new[] { "/health/live", "/health/ready" })
            Assert.Equal([HttpMethods.Get], fixture.Endpoint(route).Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.Null(fixture.Endpoint("/health").Metadata.GetMetadata<HttpMethodMetadata>());
        Assert.NotNull(fixture.StartupCheck());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LivenessNeverExecutesRefreshOrAnyRegisteredCheckEvenWhenStartupIsNotReady(bool stopping)
    {
        CountingCheck forbidden = new(HealthStatus.Unhealthy);
        await using TestHttpFixture fixture = new(started: false, configureChecks: checks => checks.AddCheck("forbidden", forbidden, tags: ["ready"]));
        fixture.Refresh.Handler = static _ => throw new InvalidOperationException("Liveness invoked readiness.");
        if (stopping) fixture.Lifetime.StopApplication();
        AssertResponse(await fixture.ProbeAsync("/health/live", Token), "/health/live", ready: true);
        Assert.Equal(0, fixture.Refresh.Calls);
        Assert.Equal(0, forbidden.Calls);
        Assert.Equal(0, fixture.Validator.Calls);
        Assert.Null(fixture.Startup.Report);
    }

    [Theory]
    [InlineData("/health/ready", true)]
    [InlineData("/health/ready", false)]
    [InlineData("/health", true)]
    [InlineData("/health", false)]
    public async Task EachReadinessRequestUsesItsOwnRefreshResultRatherThanSharedStartupReadiness(string route, bool ready)
    {
        await using TestHttpFixture fixture = new();
        fixture.Refresh.Handler = _ =>
        {
            fixture.Refresh.SharedReady = !ready;
            return Task.FromResult(ready);
        };
        for (int call = 1; call <= 2; call++)
        {
            AssertResponse(await fixture.ProbeAsync(route, Token), route, ready);
            Assert.Equal(call, fixture.Refresh.Calls);
            Assert.Equal(!ready, fixture.Startup.IsReady);
        }
        Assert.Equal(0, fixture.Validator.Calls);
    }

    [Theory]
    [InlineData("/health/ready", false)]
    [InlineData("/health/ready", true)]
    [InlineData("/health", false)]
    [InlineData("/health", true)]
    public async Task BeforeStartupOrDuringShutdownReadinessRefusesWithoutRefreshing(string route, bool stopping)
    {
        await using TestHttpFixture fixture = new(started: stopping);
        if (stopping) fixture.Lifetime.StopApplication();
        fixture.Refresh.Handler = static _ => throw new InvalidOperationException("Refresh must not run.");
        AssertResponse(await fixture.ProbeAsync(route, Token), route, ready: false);
        Assert.Equal(0, fixture.Refresh.Calls);
    }

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task AbsentRefreshRetainsTheLifecycleFallbackWithoutRevalidatingStartup(string route)
    {
        await using TestHttpFixture fixture = new(withRefresh: false);
        AssertResponse(await fixture.ProbeAsync(route, Token), route, ready: false);
        await fixture.Startup.StartingAsync(Token);
        AssertResponse(await fixture.ProbeAsync(route, Token), route, ready: true);
        fixture.Lifetime.StopApplication();
        AssertResponse(await fixture.ProbeAsync(route, Token), route, ready: false);
        Assert.Equal(1, fixture.Validator.Calls);
        Assert.Equal(0, fixture.Refresh.Calls);
    }

    [Theory]
    [InlineData("/health/ready", HealthStatus.Healthy)]
    [InlineData("/health/ready", HealthStatus.Degraded)]
    [InlineData("/health/ready", HealthStatus.Unhealthy)]
    [InlineData("/health", HealthStatus.Healthy)]
    [InlineData("/health", HealthStatus.Degraded)]
    [InlineData("/health", HealthStatus.Unhealthy)]
    public async Task OnlyReadyChecksRunAndEvenDegradedOrSensitiveReportsHaveMinimalResponses(string route, HealthStatus status)
    {
        CountingCheck included = new(status);
        CountingCheck excluded = new(HealthStatus.Unhealthy);
        await using TestHttpFixture fixture = new(configureChecks: checks =>
        {
            checks.AddCheck("included", included, tags: ["ready"]);
            checks.AddCheck("excluded", excluded, tags: ["other"]);
        });
        AssertResponse(await fixture.ProbeAsync(route, Token), route, status == HealthStatus.Healthy);
        Assert.Equal(1, included.Calls);
        Assert.Equal(0, excluded.Calls);
        Assert.Equal(1, fixture.Refresh.Calls);
    }

    [Fact]
    public async Task ShutdownAfterSuccessfulRefreshStillProducesUnavailable()
    {
        await using TestHttpFixture fixture = new();
        fixture.Refresh.Handler = _ =>
        {
            fixture.Lifetime.StopApplication();
            return Task.FromResult(true);
        };
        AssertResponse(await fixture.ProbeAsync("/health/ready", Token), "/health/ready", ready: false);
        Assert.Equal(1, fixture.Refresh.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestOrShutdownCancellationReachesTheRealRegisteredCheck(bool shutdown)
    {
        await using TestHttpFixture fixture = new();
        using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Refresh.Handler = async cancellationToken =>
        {
            Assert.True(cancellationToken.CanBeCanceled);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        };
        // Obtain the actual check through its public registration, not its internal implementation type.
        Task<HealthCheckResult> check = fixture.StartupCheck().CheckHealthAsync(new(), request.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        if (shutdown) fixture.Lifetime.StopApplication();
        else await request.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
        Assert.Equal(1, fixture.Refresh.Calls);
    }

    [Fact]
    public async Task RequestAbortedIsPropagatedThroughTheMappedReadinessEndpoint()
    {
        await using TestHttpFixture fixture = new();
        using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Refresh.Handler = async cancellationToken =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        };
        Task<TestHttpFixture.TestResponse> response = fixture.ProbeAsync("/health/ready", request.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        await request.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
    }

    [Fact]
    public async Task TheRegisteredCheckDoesNotAbsorbProgrammingErrors()
    {
        await using TestHttpFixture fixture = new();
        InvalidOperationException error = new("PRIVATE-PROGRAMMING-ERROR");
        fixture.Refresh.Handler = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.StartupCheck().CheckHealthAsync(new(), Token)));
    }

    private static void AssertResponse(TestHttpFixture.TestResponse response, string route, bool ready)
    {
        bool json = route != "/health";
        Assert.Equal(ready ? 200 : 503, response.Status);
        Assert.Equal(json ? "application/json; charset=utf-8" : "text/plain; charset=utf-8", response.ContentType);
        Assert.Equal(json ? (ready ? "{\"status\":\"healthy\"}" : "{\"status\":\"unavailable\"}") : (ready ? "Healthy" : "Unhealthy"), response.Body);
        Assert.Equal("no-store", response.CacheControl);
        Assert.Equal("no-cache", response.Pragma);
        Assert.Equal("Thu, 01 Jan 1970 00:00:00 GMT", response.Expires);
    }

    private sealed class CountingCheck(HealthStatus status) : IHealthCheck
    {
        public int Calls { get; private set; }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new HealthCheckResult(status, "PRIVATE-ROLE-HOST-CLIENT-ENVIRONMENT-SQL-CONNECTION-PASSWORD",
                new InvalidOperationException("PRIVATE-PROVIDER-EXCEPTION"), new Dictionary<string, object> { ["secretPath"] = "PRIVATE-PATH" }));
        }
    }
}
