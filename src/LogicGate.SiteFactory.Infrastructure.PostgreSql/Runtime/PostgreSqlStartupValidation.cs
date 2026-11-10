using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;

public sealed class PostgreSqlStartupValidation(IConfiguration configuration, PostgreSqlComponent component, string environment) : IStartupValidation
{
    public StartupValidationId Id { get; } = new("startup.postgresql.configuration");
    public PostgreSqlRuntimeConfiguration? Configuration { get; private set; }

    public async Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken)
    {
        Configuration = null;
        try
        {
            Configuration = await PostgreSqlConfigurationReader.ReadAsync(component,
                configuration[PostgreSqlConfigurationReader.ConnectionFileKey(component)], environment, cancellationToken).ConfigureAwait(false);
            return [new(Id, StartupValidationSeverity.Success, null, "PostgreSQL configuration is valid; connectivity was not tested.")];
        }
        catch (PostgreSqlConfigurationException exception)
        {
            return [new(Id, StartupValidationSeverity.BlockingFailure, exception.Code,
                "The dedicated PostgreSQL connection configuration is missing, unreadable or invalid.", "PostgreSql")];
        }
    }
}

public static class PostgreSqlStartupRegistration
{
    public static IServiceCollection AddPostgreSqlStartupValidation(this IServiceCollection services,
        IConfiguration configuration, PostgreSqlComponent component, string environment)
    {
        _ = PostgreSqlConfigurationReader.ConnectionFileKey(component);
        services.AddSingleton(new PostgreSqlStartupValidation(configuration, component, environment));
        services.AddSingleton<IStartupValidation>(static provider => provider.GetRequiredService<PostgreSqlStartupValidation>());
        services.AddSingleton(static provider =>
        {
            if (!provider.GetRequiredService<RuntimeStartupService>().ConfigurationIsValidated)
                throw new InvalidOperationException("SF-STARTUP-008 PostgreSQL configuration is unavailable before successful startup validation.");
            return provider.GetRequiredService<PostgreSqlStartupValidation>().Configuration
                ?? throw new InvalidOperationException("SF-STARTUP-008 PostgreSQL configuration is unavailable.");
        });
        return services;
    }
}
