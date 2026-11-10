using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogicGate.SiteFactory.Runtime.Hosting;

/// <summary>Runs before any hosted service's StartAsync, including the Web server.</summary>
public sealed partial class RuntimeStartupService(IRuntimeStartupValidator validator, ILogger<RuntimeStartupService> logger,
    IHostApplicationLifetime lifetime, IRuntimeStartupReadiness? database = null) : IHostedLifecycleService
{
    public RuntimeStartupReport? Report { get; private set; }
    public bool ConfigurationIsValidated => Report is not null && (database?.ConfigurationValidated ?? Report.IsReady);
    public bool IsReady => (database?.IsReady ?? Report?.IsReady == true) && lifetime.ApplicationStarted.IsCancellationRequested
        && !lifetime.ApplicationStopping.IsCancellationRequested;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        Report = null;
        Report = await validator.ValidateAsync(cancellationToken);
        foreach (StartupValidationResult result in Report.Results)
        {
            LogLevel level = result.Severity switch
            {
                StartupValidationSeverity.Success => LogLevel.Information,
                StartupValidationSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Error,
            };
            if (logger.IsEnabled(level))
                LogValidation(logger, level, result.ValidationId.Value, result.Severity, result.ErrorCode, result.PropertyPath, result.Summary);
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Only the Web's DB phase may fail while HTTP remains available for health checks.
        // Configuration/secret/expected-role failures and cancellation retain the fail-fast contract.
        if (!Report.IsReady && database?.ConfigurationValidated != true) throw new RuntimeStartupException(Report);
    }

    [LoggerMessage(EventId = 1, Message = "Startup validation {ValidationId}: {Severity} {ErrorCode} {PropertyPath} {Summary}")]
    private static partial void LogValidation(ILogger logger, LogLevel level, string validationId,
        StartupValidationSeverity severity, string? errorCode, string? propertyPath, string summary);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class RuntimeStartupException(RuntimeStartupReport report) : Exception(
    string.Join("\n", report.Results.Where(static result => result.Severity == StartupValidationSeverity.BlockingFailure)))
{
    public RuntimeStartupReport Report { get; } = report;
    // Generic Host logs startup exceptions itself. Expected configuration failures must not expose stack traces.
    public override string ToString() => Message;
}

public static class RuntimeStartupRegistration
{
    public static IServiceCollection AddRuntimeStartupValidation(this IServiceCollection services,
        IConfiguration configuration, string hostEnvironment, RuntimeComponent component)
    {
        services.AddSingleton(new RuntimeConfigurationStartupValidation(configuration, hostEnvironment, component));
        services.AddSingleton<IStartupValidation>(static provider => provider.GetRequiredService<RuntimeConfigurationStartupValidation>());
        services.AddSingleton<IRuntimeStartupValidator, RuntimeStartupValidator>();
        services.AddSingleton<RuntimeStartupService>();
        services.AddHostedService(static provider => provider.GetRequiredService<RuntimeStartupService>());
        services.AddSingleton(static provider =>
        {
            RuntimeStartupService startup = provider.GetRequiredService<RuntimeStartupService>();
            if (!startup.ConfigurationIsValidated) throw new InvalidOperationException("SF-STARTUP-008 Validated runtime configuration is not available before startup validation.");
            return provider.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration
                ?? throw new InvalidOperationException("SF-STARTUP-008 Validated runtime configuration is missing.");
        });
        services.AddSingleton(static provider => provider.GetRequiredService<RuntimeConfiguration>().Identity);
        return services;
    }
}
