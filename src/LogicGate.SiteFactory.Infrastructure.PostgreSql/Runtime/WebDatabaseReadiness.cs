using System.Diagnostics;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;

/// <summary>Web-only orchestration: local configuration first, then the existing read-only compatibility probe.</summary>
public sealed class WebDatabaseReadiness(
    IEnumerable<IStartupValidation> validations,
    RuntimeConfigurationStartupValidation runtime,
    IConfiguration configuration,
    Func<IRuntimeDatabaseReader> readerFactory) : IRuntimeStartupValidator, IRuntimeStartupReadiness, IRuntimeReadinessRefresh, IDisposable
{
    public const string ExpectedRoleKey = "SITEFACTORY_WEB_EXPECTED_ROLE";
    private static readonly StartupValidationId RoleConfigurationId = new("startup.postgresql.expected-role");
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RuntimeStartupValidator local = new(validations);
    private DatabaseCompatibilityProbe? probe;
    private DatabaseCompatibilityExpectation? expected;
    private RuntimeStartupReport? currentReport;
    private bool configurationValidated;
    private int initializationStarted;

    public bool ConfigurationValidated => Volatile.Read(ref configurationValidated);
    public RuntimeStartupReport? CurrentReport => Volatile.Read(ref currentReport);
    public bool IsReady => ConfigurationValidated && CurrentReport?.IsReady == true;

    /// <summary>One-shot initialization. Any subsequent call is a programming error, even after failure or cancellation.</summary>
    public async Task<RuntimeStartupReport> ValidateAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref initializationStarted, 1, 0) != 0)
            throw new InvalidOperationException("Web database startup validation can only run once per instance.");

        RuntimeStartupReport prerequisites = await local.ValidateAsync(cancellationToken).ConfigureAwait(false);
        if (!prerequisites.IsReady) return prerequisites;
        cancellationToken.ThrowIfCancellationRequested();
        string? login = configuration[ExpectedRoleKey];
        // Preserve the Web's historical character limit, distinct from the Worker's UTF-8 byte limit.
        if (string.IsNullOrWhiteSpace(login) || login.Length > 63 || login.Any(char.IsControl)
            || login != login.Trim())
            return new([.. prerequisites.Results, new(RoleConfigurationId, StartupValidationSeverity.BlockingFailure,
                "SF-CONFIG-STARTUP-003", "An explicit valid expected Web database role is required.", "PostgreSql.ExpectedRole")]);

        expected = new(runtime.Configuration?.Identity
            ?? throw new InvalidOperationException("SF-STARTUP-008 Validated runtime identity is missing."), login);
        probe = new(readerFactory());
        // Retain this verification's report across the final aggregation, never reread shared CurrentReport.
        RuntimeStartupReport report = await RefreshReportAsync(cancellationToken, initialize: true)
            ?? throw new InvalidOperationException("Initial Web database validation did not produce a report.");
        return new([.. prerequisites.Results, .. report.Results]);
    }

    /// <summary>Fresh serialized verification. Each caller owns its result; only completed reports are published.</summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        Activity? activity = Activity.Current is { IsAllDataRequested: true } current ? current : null;
        long startTicks = activity is null ? 0 : Stopwatch.GetTimestamp();
        string outcome = "UNEXPECTED_ERROR";
        bool? result = null;
        RuntimeStartupReport? report = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ConfigurationValidated)
            {
                outcome = "NOT_INITIALIZED";
                result = false;
                return false;
            }
            report = await RefreshReportAsync(cancellationToken).ConfigureAwait(false);
            result = report?.IsReady == true;
            outcome = report is null ? "QUEUE_TIMEOUT" : result.Value ? "READY" : "BLOCKED";
            return result.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "CANCELLED";
            throw;
        }
        finally
        {
            if (activity is not null)
            {
                // Lowest ordinal blocker code, from this immutable local report only. Never inspect shared state.
                string? code = null;
                if (report is not null)
                    foreach (StartupValidationResult validation in report.Results)
                        if (validation.Severity == StartupValidationSeverity.BlockingFailure && validation.ErrorCode is { } candidate
                            && (code is null || StringComparer.Ordinal.Compare(candidate, code) < 0)) code = candidate;
                activity.AddEvent(new("sitefactory.readiness.refresh.completed", tags: new ActivityTagsCollection
                {
                    { "startTicks", startTicks }, { "endTicks", Stopwatch.GetTimestamp() },
                    { "outcome", outcome }, { "result", result }, { "diagnosticCode", code },
                }));
            }
        }
    }

    private async Task<RuntimeStartupReport?> RefreshReportAsync(CancellationToken cancellationToken, bool initialize = false)
    {
        // At most one probe; queue wait and the actual reader retain separate bounded deadlines.
        if (!await gate.WaitAsync(PostgreSqlDatabaseReader.MaximumDuration, cancellationToken).ConfigureAwait(false)) return null;
        try
        {
            // The initial probe owns the gate before refresh becomes available; its first report cannot time out in the queue.
            if (initialize) Volatile.Write(ref configurationValidated, true);
            IReadOnlyList<StartupValidationResult> results = await probe!.ValidateAsync(expected!, cancellationToken).ConfigureAwait(false);
            RuntimeStartupReport report = new(results);
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref currentReport, report);
            return report;
        }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();
}

public static class WebDatabaseStartupRegistration
{
    public static IServiceCollection AddWebDatabaseReadiness(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IRuntimeDatabaseReader>(static provider => new PostgreSqlDatabaseReader(
            provider.GetRequiredService<PostgreSqlStartupValidation>().Configuration
                ?? throw new InvalidOperationException("SF-STARTUP-008 Validated Web connection configuration is missing.")));
        services.AddSingleton(provider => new WebDatabaseReadiness(provider.GetServices<IStartupValidation>(),
            provider.GetRequiredService<RuntimeConfigurationStartupValidation>(), configuration,
            () => provider.GetRequiredService<IRuntimeDatabaseReader>()));
        services.AddSingleton<IRuntimeStartupValidator>(static provider => provider.GetRequiredService<WebDatabaseReadiness>());
        services.AddSingleton<IRuntimeStartupReadiness>(static provider => provider.GetRequiredService<WebDatabaseReadiness>());
        services.AddSingleton<IRuntimeReadinessRefresh>(static provider => provider.GetRequiredService<WebDatabaseReadiness>());
        return services;
    }
}
