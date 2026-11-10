using System.Text;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.AspNetCore.Tests;

/// <summary>Mapped delegates and middleware only: the application is never started and owns no listener.</summary>
internal sealed class TestHttpFixture : IAsyncDisposable
{
    public TestHttpFixture(bool started = true, bool withRefresh = true, Action<IHealthChecksBuilder>? configureChecks = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = "Test",
            ApplicationName = typeof(RuntimeHealthEndpoints).Assembly.GetName().Name,
        });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        Startup = new(Validator, NullLogger<RuntimeStartupService>.Instance, Lifetime, withRefresh ? Refresh : null);
        // Generic Host's concrete implementation rejects a custom lifetime. Build only the WebApplication
        // with a test IHost that can never start; its services remain the real public HTTP registrations.
        builder.Services.AddSingleton<IHost>(static provider => new UnstartedTestHost(provider));
        builder.Services.AddSingleton<IHostApplicationLifetime>(Lifetime);
        builder.Services.AddSingleton(Startup);
        if (withRefresh) builder.Services.AddSingleton<IRuntimeReadinessRefresh>(Refresh);
        IHealthChecksBuilder checks = builder.Services.AddRuntimeHealthChecks();
        configureChecks?.Invoke(checks);
        Application = builder.Build();
        Application.MapRuntimeHealthEndpoints();
        if (started) Lifetime.SignalStarted();
    }

    public WebApplication Application { get; }
    public ControlledLifetime Lifetime { get; } = new();
    public ControlledRefresh Refresh { get; } = new();
    public ControlledValidator Validator { get; } = new();
    public RuntimeStartupService Startup { get; }

    public RouteEndpoint Endpoint(string route) => Assert.Single(((IEndpointRouteBuilder)Application).DataSources
        .SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>(), endpoint => endpoint.RoutePattern.RawText == route);

    public IHealthCheck StartupCheck() => Assert.Single(Application.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
        .Value.Registrations, static registration => registration.Name == "startup").Factory(Application.Services);

    public Task<TestResponse> ProbeAsync(string route, CancellationToken cancellationToken) => InvokeAsync(
        context => Endpoint(route).RequestDelegate!(context), route, null, cancellationToken);

    public Task<TestResponse> GateAsync(RequestDelegate terminal, CancellationToken cancellationToken, string? healthRoute = null)
    {
        Application.UseRuntimeReadinessGate();
        IApplicationBuilder pipeline = Application;
        pipeline.Run(terminal); // Terminal middleware registration, never WebApplication.Run/Start.
        RequestDelegate middleware = pipeline.Build();
        return InvokeAsync(middleware, healthRoute ?? "/application", healthRoute is null ? null : Endpoint(healthRoute), cancellationToken);
    }

    private async Task<TestResponse> InvokeAsync(RequestDelegate callback, string path, Endpoint? endpoint, CancellationToken cancellationToken)
    {
        using MemoryStream stream = new();
        DefaultHttpContext context = new() { RequestServices = Application.Services, RequestAborted = cancellationToken };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.SetEndpoint(endpoint);
        context.Response.Body = stream;
        await callback(context);
        return new(context.Response.StatusCode, context.Response.ContentType, Encoding.UTF8.GetString(stream.ToArray()),
            context.Response.Headers.CacheControl.ToString(), context.Response.Headers.Pragma.ToString(), context.Response.Headers.Expires.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        await Application.DisposeAsync();
        await ((IAsyncDisposable)Application.Services).DisposeAsync();
        Lifetime.Dispose();
    }

    internal sealed record TestResponse(int Status, string? ContentType, string Body, string CacheControl, string Pragma, string Expires);

    private sealed class UnstartedTestHost(IServiceProvider services) : IHost, IAsyncDisposable
    {
        public IServiceProvider Services => services;
        public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("HTTP delegate tests must never start a host.");
        public Task StopAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("HTTP delegate tests must never run a host lifecycle.");
        // The fixture owns/disposes the provider; disposing this registered host cannot recursively dispose it.
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class ControlledRefresh : IRuntimeReadinessRefresh, IRuntimeStartupReadiness
{
    public Func<CancellationToken, Task<bool>> Handler { get; set; } = static _ => Task.FromResult(true);
    public int Calls { get; private set; }
    public bool SharedReady { get; set; }
    public bool ConfigurationValidated => true;
    public bool IsReady => SharedReady;

    public Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Handler(cancellationToken);
    }
}

internal sealed class ControlledValidator : IRuntimeStartupValidator
{
    public int Calls { get; private set; }
    public Task<RuntimeStartupReport> ValidateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(new RuntimeStartupReport([new(new("test.startup"), StartupValidationSeverity.Success, null, "Validated.")]));
    }
}

internal sealed class ControlledLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource started = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly CancellationTokenSource stopped = new();
    public CancellationToken ApplicationStarted => started.Token;
    public CancellationToken ApplicationStopping => stopping.Token;
    public CancellationToken ApplicationStopped => stopped.Token;
    public void SignalStarted() => started.Cancel();
    public void StopApplication() => stopping.Cancel();
    public void Dispose()
    {
        started.Dispose();
        stopping.Dispose();
        stopped.Dispose();
    }
}
