using System.Reflection;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Runtime.Database;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class PostgreSqlDatabaseReaderTests
{
    [Fact]
    public async Task ConstructionIsOfflineAndPreCancellationPreventsAnyConnectionAttempt()
    {
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlRuntimeConfiguration configuration = await PostgreSqlConfigurationReader.ReadAsync(PostgreSqlComponent.Web,
            secrets.PathFor(PostgreSqlComponent.Web), "Test", TestContext.Current.CancellationToken);
        PostgreSqlDatabaseReader reader = new(configuration);
        Assert.IsAssignableFrom<IRuntimeDatabaseReader>(reader);
        Assert.Equal(TimeSpan.FromSeconds(5), PostgreSqlDatabaseReader.MaximumDuration);
        File.Delete(secrets.PathFor(PostgreSqlComponent.Web)); // The reader receives a snapshot, not a file reference.
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        secrets.AssertRedacted(error.ToString());
    }

    [Fact]
    public void PublicReaderApiContainsOnlyTheApprovedPortAndBound()
    {
        Assert.True(typeof(PostgreSqlDatabaseReader).IsSealed);
        Assert.Equal([typeof(IRuntimeDatabaseReader)], typeof(PostgreSqlDatabaseReader).GetInterfaces());
        Assert.Equal(typeof(PostgreSqlRuntimeConfiguration), Assert.Single(Assert.Single(typeof(PostgreSqlDatabaseReader).GetConstructors()).GetParameters()).ParameterType);
        MethodInfo read = Assert.Single(typeof(PostgreSqlDatabaseReader).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("ReadAsync", read.Name);
        Assert.Equal(typeof(Task<RuntimeDatabaseObservation>), read.ReturnType);
        Assert.Equal(typeof(CancellationToken), Assert.Single(read.GetParameters()).ParameterType);
        PropertyInfo maximum = Assert.Single(typeof(PostgreSqlDatabaseReader).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("MaximumDuration", maximum.Name);
        Assert.Null(maximum.SetMethod);
        Assert.Equal("LogicGate.SiteFactory.Runtime", typeof(DatabaseCompatibilityProbe).Assembly.GetName().Name);
        Assert.Null(typeof(PostgreSqlRuntimeConfiguration).GetMethod("CreateProbeConnection"));
    }
}
