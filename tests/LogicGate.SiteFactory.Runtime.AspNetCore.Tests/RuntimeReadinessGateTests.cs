using Microsoft.AspNetCore.Http;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.AspNetCore.Tests;

public sealed class RuntimeReadinessGateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task ActualHealthMetadataExemptsEndpointsWithoutRequiringARefresh(string route)
    {
        await using TestHttpFixture fixture = new(started: false, withRefresh: false);
        int calls = 0;
        TestHttpFixture.TestResponse response = await fixture.GateAsync(context =>
        {
            calls++;
            return context.Response.WriteAsync("terminal", Token);
        }, Token, route);
        Assert.Equal(1, calls);
        Assert.Equal("terminal", response.Body);
        Assert.Equal(0, fixture.Refresh.Calls);
    }

    [Fact]
    public async Task BeforeApplicationStartedTrafficIsRefusedWithoutRefreshOrNextMiddleware()
    {
        await using TestHttpFixture fixture = new(started: false);
        fixture.Refresh.Handler = static _ => throw new InvalidOperationException("Refresh before startup.");
        AssertUnavailable(await fixture.GateAsync(static _ => throw new InvalidOperationException("Next must not run."), Token));
        Assert.Equal(0, fixture.Refresh.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheLocalRefreshResultControlsTrafficEvenIfSharedReadinessChanges(bool result)
    {
        await using TestHttpFixture fixture = new();
        fixture.Refresh.Handler = _ =>
        {
            fixture.Refresh.SharedReady = !result;
            return Task.FromResult(result);
        };
        int nextCalls = 0;
        TestHttpFixture.TestResponse response = await fixture.GateAsync(context =>
        {
            nextCalls++;
            context.Response.StatusCode = 202;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers.CacheControl = "private";
            return context.Response.WriteAsync("application-result", Token);
        }, Token);
        Assert.Equal(1, fixture.Refresh.Calls);
        Assert.Equal(!result, fixture.Startup.IsReady);
        Assert.Equal(result ? 1 : 0, nextCalls);
        if (!result) AssertUnavailable(response);
        else
        {
            Assert.Equal(202, response.Status);
            Assert.Equal("text/plain; charset=utf-8", response.ContentType);
            Assert.Equal("application-result", response.Body);
            Assert.Equal("private", response.CacheControl);
        }
    }

    [Fact]
    public async Task ShutdownAfterRefreshRefusesTrafficWithoutCallingNext()
    {
        await using TestHttpFixture fixture = new();
        fixture.Refresh.Handler = _ =>
        {
            fixture.Lifetime.StopApplication();
            return Task.FromResult(true);
        };
        AssertUnavailable(await fixture.GateAsync(static _ => throw new InvalidOperationException("Next must not run."), Token));
        Assert.Equal(1, fixture.Refresh.Calls);
    }

    [Fact]
    public async Task MissingRefreshIsACompositionErrorRatherThanASilentFallback()
    {
        await using TestHttpFixture fixture = new(withRefresh: false);
        await fixture.Startup.StartingAsync(Token);
        Assert.True(fixture.Startup.IsReady);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.GateAsync(static _ => Task.CompletedTask, Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestOrShutdownCancellationIsPropagatedInsteadOfProducing503(bool shutdown)
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
        Task<TestHttpFixture.TestResponse> gate = fixture.GateAsync(static _ => throw new InvalidOperationException("Next must not run."), request.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        if (shutdown) fixture.Lifetime.StopApplication();
        else await request.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate);
    }

    [Fact]
    public async Task ProgrammingErrorsArePropagatedRatherThanConvertedIntoUnavailable()
    {
        await using TestHttpFixture fixture = new();
        InvalidOperationException error = new("PRIVATE-PROGRAMMING-ERROR");
        fixture.Refresh.Handler = _ => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.GateAsync(static _ => Task.CompletedTask, Token)));
    }

    private static void AssertUnavailable(TestHttpFixture.TestResponse response)
    {
        Assert.Equal(503, response.Status);
        Assert.Equal("application/json; charset=utf-8", response.ContentType);
        Assert.Equal("{\"status\":\"unavailable\"}", response.Body);
        Assert.Equal("no-store", response.CacheControl);
    }
}
