using System.Text;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;

/// <summary>Worker startup only: local prerequisites, then the existing read-only probe; every blocker remains fail-fast.</summary>
public sealed class WorkerDatabaseStartupValidator(
    IEnumerable<IStartupValidation> validations,
    RuntimeConfigurationStartupValidation runtime,
    IConfiguration configuration,
    Func<IRuntimeDatabaseReader> readerFactory) : IRuntimeStartupValidator
{
    public const string ExpectedRoleKey = "SITEFACTORY_WORKER_EXPECTED_ROLE";
    private static readonly StartupValidationId RoleConfigurationId = new("startup.postgresql.expected-role");
    private readonly RuntimeStartupValidator local = new(validations);

    public async Task<RuntimeStartupReport> ValidateAsync(CancellationToken cancellationToken)
    {
        RuntimeStartupReport prerequisites = await local.ValidateAsync(cancellationToken).ConfigureAwait(false);
        if (!prerequisites.IsReady) return prerequisites;
        cancellationToken.ThrowIfCancellationRequested();
        string? login = configuration[ExpectedRoleKey];
        // Independently supplied expectation, never taken from the credential, observed session or another component.
        if (string.IsNullOrWhiteSpace(login) || Encoding.UTF8.GetByteCount(login) > 63
            || login.Any(char.IsControl) || login != login.Trim())
            return new([.. prerequisites.Results, new(RoleConfigurationId, StartupValidationSeverity.BlockingFailure,
                "SF-CONFIG-STARTUP-003", "An explicit valid expected Worker database role is required.", "PostgreSql.ExpectedRole")]);

        DatabaseCompatibilityExpectation expected = new(runtime.Configuration?.Identity
            ?? throw new InvalidOperationException("SF-STARTUP-008 Validated runtime identity is missing."), login);
        DatabaseCompatibilityProbe probe = new(readerFactory());
        IReadOnlyList<StartupValidationResult> results = await probe.ValidateAsync(expected, cancellationToken).ConfigureAwait(false);
        return new([.. prerequisites.Results, .. results]);
    }
}

public static class WorkerDatabaseStartupRegistration
{
    public static IServiceCollection AddWorkerDatabaseStartupValidation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IRuntimeDatabaseReader>(static provider => new PostgreSqlDatabaseReader(
            provider.GetRequiredService<PostgreSqlStartupValidation>().Configuration
                ?? throw new InvalidOperationException("SF-STARTUP-008 Validated Worker connection configuration is missing.")));
        services.AddSingleton<IRuntimeStartupValidator>(provider => new WorkerDatabaseStartupValidator(
            provider.GetServices<IStartupValidation>(), provider.GetRequiredService<RuntimeConfigurationStartupValidation>(),
            configuration, () => provider.GetRequiredService<IRuntimeDatabaseReader>()));
        return services;
    }
}
