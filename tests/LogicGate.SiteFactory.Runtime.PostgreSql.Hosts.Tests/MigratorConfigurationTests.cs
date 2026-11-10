using System.Globalization;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Migrator;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

public sealed class MigratorConfigurationTests
{
    [Fact]
    public async Task MigratorMapsReaderFailuresWithoutAttemptingMigration()
    {
        using OfflineDatabaseFixture secrets = new();
        secrets.Write(PostgreSqlComponent.Migrator, "invalid=" + secrets.PasswordFor(PostgreSqlComponent.Migrator));
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        Assert.Equal(2, await MigrationCommand.RunAsync(["apply", "--environment", "Test", "--confirm-migration"],
            secrets.PathFor(PostgreSqlComponent.Migrator), output, error, TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith("SF-MIGRATION-CONFIG-001 ", error.ToString(), StringComparison.Ordinal);
        secrets.AssertRedacted(error.ToString());
    }
}
