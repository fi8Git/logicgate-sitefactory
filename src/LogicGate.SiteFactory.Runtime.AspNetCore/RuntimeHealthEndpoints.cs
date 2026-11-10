using System.Diagnostics;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LogicGate.SiteFactory.Runtime.AspNetCore;

/// <summary>Public probes expose only availability, never the startup report or health check details.</summary>
public static class RuntimeHealthEndpoints
{
    public static IHealthChecksBuilder AddRuntimeHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks().AddCheck<StartupReadinessHealthCheck>("startup", tags: ["ready"]);

    public static void MapRuntimeHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // A functioning HTTP pipeline is the liveness proof. No registered check runs on this route.
        endpoints.MapHealthChecks("/health/live", Options(static _ => false))
            .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]), new PublicHealthEndpoint());
        endpoints.MapHealthChecks("/health/ready", Options(static check => check.Tags.Contains("ready"), "HEALTH_READY"))
            .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]), new PublicHealthEndpoint());
        // Compatibility with the original route and its deliberately minimal plain-text body.
        HealthCheckOptions legacy = Options(static check => check.Tags.Contains("ready"), "HEALTH_LEGACY");
        legacy.ResponseWriter = static (context, report) => WriteResponseAsync(context, report, json: false);
        endpoints.MapHealthChecks("/health", legacy).WithMetadata(new PublicHealthEndpoint());
    }

    public static void UseRuntimeReadinessGate(this WebApplication application) => application.Use(async (context, next) =>
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<PublicHealthEndpoint>() is null)
        {
            Activity? activity = Activity.Current;
            string outcome = "THREW";
            bool? refreshResult = null;
            try
            {
                IHostApplicationLifetime lifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
                using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
                IRuntimeReadinessRefresh database = context.RequestServices.GetRequiredService<IRuntimeReadinessRefresh>();
                // Keep the original short-circuit order and read each lifecycle condition only where it was evaluated.
                bool started = lifetime.ApplicationStarted.IsCancellationRequested;
                bool stopping = false;
                bool refused = !started || !(refreshResult = await database.RefreshAsync(deadline.Token)).Value
                    || (stopping = lifetime.ApplicationStopping.IsCancellationRequested);
                outcome = !started ? "HOST_NOT_STARTED" : refreshResult != true ? "REFRESH_REJECTED"
                    : stopping ? "HOST_STOPPING" : "ACCEPTED";
                if (refused)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync("{\"status\":\"unavailable\"}", context.RequestAborted);
                    return;
                }
            }
            finally { ReadinessHttpTelemetry.Record(activity, "TRAFFIC_GATE", outcome, refreshResult); }
        }
        await next(context);
    });

    private sealed class PublicHealthEndpoint;

    private static HealthCheckOptions Options(Func<HealthCheckRegistration, bool> predicate, string? site = null) => new()
    {
        // The framework schedules checks in child tasks. Carry a closed route identity on the request's own Activity.
        Predicate = site is null ? predicate : check =>
        {
            ReadinessHttpTelemetry.SetSite(Activity.Current, site);
            return predicate(check);
        },
        AllowCachingResponses = false,
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        },
        ResponseWriter = static (context, report) => WriteResponseAsync(context, report, json: true),
    };

    private static Task WriteResponseAsync(HttpContext context, HealthReport report, bool json)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = json ? "application/json; charset=utf-8" : "text/plain; charset=utf-8";
        bool healthy = report.Status == HealthStatus.Healthy;
        string body = json
            ? (healthy ? "{\"status\":\"healthy\"}" : "{\"status\":\"unavailable\"}")
            : (healthy ? "Healthy" : "Unhealthy");
        return context.Response.WriteAsync(body, context.RequestAborted);
    }
}

internal sealed class StartupReadinessHealthCheck(RuntimeStartupService startup, IHostApplicationLifetime lifetime,
    IRuntimeReadinessRefresh? database = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Activity? activity = Activity.Current;
        string? site = ReadinessHttpTelemetry.Site(activity);
        string outcome = "THREW";
        bool? refreshResult = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool started = lifetime.ApplicationStarted.IsCancellationRequested;
            bool stopping = false;
            if (!started || (stopping = lifetime.ApplicationStopping.IsCancellationRequested))
            {
                outcome = !started ? "HOST_NOT_STARTED" : "HOST_STOPPING";
                return HealthCheckResult.Unhealthy();
            }
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.ApplicationStopping);
            // Decide from this verification, not a second read of shared database readiness.
            bool ready = database is null ? startup.IsReady : (refreshResult = await database.RefreshAsync(deadline.Token)).Value;
            bool healthy = ready && !(stopping = lifetime.ApplicationStopping.IsCancellationRequested);
            outcome = !ready ? "REFRESH_REJECTED" : stopping ? "HOST_STOPPING" : "ACCEPTED";
            return healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        finally { ReadinessHttpTelemetry.Record(activity, site, outcome, refreshResult); }
    }
}

internal static class ReadinessHttpTelemetry
{
    private const string SiteProperty = "sitefactory.readiness.http.site";
    internal static void SetSite(Activity? activity, string site)
    {
        if (activity is { IsAllDataRequested: true }) activity.SetCustomProperty(SiteProperty, site);
    }
    internal static string? Site(Activity? activity) => activity?.GetCustomProperty(SiteProperty) as string;
    internal static void Record(Activity? activity, string? site, string outcome, bool? refreshResult)
    {
        if (activity is { IsAllDataRequested: true } && site is "HEALTH_READY" or "HEALTH_LEGACY" or "TRAFFIC_GATE")
            activity.AddEvent(new("sitefactory.readiness.http.decision", tags: new ActivityTagsCollection
            {
                { "site", site }, { "outcome", outcome }, { "refreshResult", refreshResult },
            }));
    }
}
