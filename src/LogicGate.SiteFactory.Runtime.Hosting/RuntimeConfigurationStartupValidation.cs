using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;

namespace LogicGate.SiteFactory.Runtime.Hosting;

/// <summary>Reuses the closed SF-TASK-034 reader; configuration is captured only on successful validation.</summary>
public sealed class RuntimeConfigurationStartupValidation(
    IConfiguration configuration, string hostEnvironment, RuntimeComponent component) : IStartupValidation
{
    public StartupValidationId Id { get; } = new("startup.configuration.schema");
    public RuntimeConfiguration? Configuration { get; private set; }

    public Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Configuration = null;
        try
        {
            RuntimeConfiguration snapshot = RuntimeConfigurationReader.Read(configuration, hostEnvironment, component);
            cancellationToken.ThrowIfCancellationRequested();
            Configuration = snapshot;
            return Task.FromResult<IReadOnlyList<StartupValidationResult>>(
                [new(Id, StartupValidationSeverity.Success, null, "Runtime configuration is valid.")]);
        }
        catch (RuntimeConfigurationException exception)
        {
            return Task.FromResult(exception.Diagnostics);
        }
    }
}
