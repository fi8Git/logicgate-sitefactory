using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

// Only the approved test bridge. No generated assembly, host, provider exception or connection leaves it.
public static class DatabaseCompatibilityIntegrationBridge
{
    public static async Task<string[]> ProbeRealAsync(string file, string client, string environment, string expectedLogin,
        string component, Guid? instanceId, CancellationToken cancellationToken)
    {
        if (!RuntimeConfiguration.TryCreate(client, environment, Enum.Parse<RuntimeComponent>(component),
            "https://example.invalid", out RuntimeConfiguration? runtime, out _)) throw new ArgumentException("Invalid test expectation.");
        PostgreSqlRuntimeConfiguration configuration = await PostgreSqlConfigurationReader.ReadAsync(
            Enum.Parse<PostgreSqlComponent>(component), file, environment, cancellationToken);
        DatabaseCompatibilityProbe probe = new(new PostgreSqlDatabaseReader(configuration));
        IReadOnlyList<StartupValidationResult> results = await probe.ValidateAsync(new(runtime.Identity, expectedLogin, instanceId), cancellationToken);
        return results.Select(static result => (result.ErrorCode ?? "PASS") + "|" + result.Summary).ToArray();
    }
}
