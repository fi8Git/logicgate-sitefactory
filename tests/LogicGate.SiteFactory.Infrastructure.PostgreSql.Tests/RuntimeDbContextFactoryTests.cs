using System.Data;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

// Configuration is read from fictitious files only; contexts are inspected offline, never opened.
public sealed class RuntimeDbContextFactoryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(PostgreSqlComponent.Web)]
    [InlineData(PostgreSqlComponent.Worker)]
    [InlineData(PostgreSqlComponent.Migrator)]
    public async Task ValidatedComponentKeepsItsCredentialsProviderAndMigrationHistory(PostgreSqlComponent component)
    {
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlRuntimeConfiguration configuration = await ReadAsync(secrets, component);
        using RuntimeDbContext context = RuntimeDbContextFactory.Create(configuration);
        NpgsqlConnection connection = Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection());
        NpgsqlConnectionStringBuilder actual = new(connection.ConnectionString);
        Assert.Equal(component, configuration.Component);
        Assert.Equal("test_" + component, actual.Username);
        Assert.True(actual.Password == secrets.PasswordFor(component), "The component's password changed.");
        Assert.True(connection.ConnectionString == new NpgsqlConnectionStringBuilder(secrets.Connection(component)).ConnectionString,
            "The validated connection settings changed.");
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);

        IDbContextOptions options = context.GetService<IDbContextOptions>();
        RelationalOptionsExtension relational = RelationalOptionsExtension.Extract(options);
        DbContextOptions<RuntimeDbContext> reference = new DbContextOptionsBuilder<RuntimeDbContext>()
            .UseNpgsql(secrets.Connection(component), options => options.MigrationsHistoryTable("__EFMigrationsHistory", "sitefactory_runtime"))
            .Options;
        RelationalOptionsExtension referenceRelational = RelationalOptionsExtension.Extract(reference);
        Assert.Equal(referenceRelational.GetType(), relational.GetType());
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal("sitefactory_runtime", relational.MigrationsHistoryTableSchema);
        Assert.Equal(referenceRelational.MigrationsAssembly, relational.MigrationsAssembly);
        Assert.Equal(referenceRelational.CommandTimeout, relational.CommandTimeout);
        Assert.Same(connection, relational.Connection);
        Assert.True(relational.IsConnectionOwned);
        CoreOptionsExtension core = Assert.IsType<CoreOptionsExtension>(options.FindExtension<CoreOptionsExtension>());
        Assert.False(core.IsSensitiveDataLoggingEnabled);
        Assert.False(core.DetailedErrorsEnabled);
        secrets.AssertRedacted(configuration.ToString());
        secrets.AssertRedacted(Assert.IsType<string>(context.ToString()));
    }

    [Fact]
    public void NullConfigurationIsRejectedBeforeAllocatingAContext()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => RuntimeDbContextFactory.Create(null!));
        Assert.Equal("configuration", error.ParamName);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Utf8QuotesWhitespaceAndTerminalNewlineKeepTheExactPassword(string ending)
    {
        using PostgreSqlSecretFixture secrets = new();
        secrets.Write(PostgreSqlComponent.Worker, secrets.Connection(PostgreSqlComponent.Worker) + ending);
        PostgreSqlRuntimeConfiguration configuration = await ReadAsync(secrets, PostgreSqlComponent.Worker);
        using RuntimeDbContext context = RuntimeDbContextFactory.Create(configuration);
        Assert.True(new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString()).Password == secrets.PasswordFor(PostgreSqlComponent.Worker),
            "Password characters changed.");
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RuntimeConnectionOptionsArePreservedRatherThanReplacedByProbeSettings(bool pooling)
    {
        using PostgreSqlSecretFixture secrets = new();
        NpgsqlConnectionStringBuilder expected = new(secrets.Connection(PostgreSqlComponent.Web))
        {
            Pooling = pooling,
            Timeout = 17,
            CommandTimeout = 41,
            CancellationTimeout = 2000,
            ApplicationName = "offline-factory-test",
            SearchPath = "sitefactory_runtime, pg_catalog",
            NoResetOnClose = true,
        };
        secrets.Write(PostgreSqlComponent.Web, expected.ConnectionString);
        PostgreSqlRuntimeConfiguration configuration = await ReadAsync(secrets, PostgreSqlComponent.Web);
        using RuntimeDbContext context = RuntimeDbContextFactory.Create(configuration);
        Assert.True(context.Database.GetConnectionString() == expected.ConnectionString, "Runtime connection options changed.");
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task RepeatedCallsDoNotShareMutableContextsOrConnections()
    {
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlRuntimeConfiguration configuration = await ReadAsync(secrets, PostgreSqlComponent.Web);
        using RuntimeDbContext first = RuntimeDbContextFactory.Create(configuration);
        using RuntimeDbContext second = RuntimeDbContextFactory.Create(configuration);
        NpgsqlConnection firstConnection = Assert.IsType<NpgsqlConnection>(first.Database.GetDbConnection());
        NpgsqlConnection secondConnection = Assert.IsType<NpgsqlConnection>(second.Database.GetDbConnection());
        Assert.NotSame(first, second);
        Assert.NotSame(firstConnection, secondConnection);
        string original = secondConnection.ConnectionString;
        firstConnection.ConnectionString = new NpgsqlConnectionStringBuilder(original) { ApplicationName = "modified-first-context" }.ConnectionString;
        Assert.True(secondConnection.ConnectionString == original, "One context changed the other's connection.");
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => firstConnection.ConnectionString = original);
        secondConnection.ConnectionString = original;
        Assert.Equal(ConnectionState.Closed, secondConnection.State);
        second.Dispose();
        Assert.Throws<ObjectDisposedException>(() => secondConnection.ConnectionString = original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposingTheContextReleasesItsDedicatedConnection(bool asynchronous)
    {
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlRuntimeConfiguration configuration = await ReadAsync(secrets, PostgreSqlComponent.Web);
        await using RuntimeDbContext context = RuntimeDbContextFactory.Create(configuration);
        NpgsqlConnection connection = Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection());
        string original = connection.ConnectionString;
        if (asynchronous) await context.DisposeAsync();
        else context.Dispose();
        Assert.Throws<ObjectDisposedException>(() => connection.ConnectionString = original);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task CreationUsesTheValidatedSnapshotWithoutRereadingFilesOrContactingAnUnavailableServer()
    {
        using PostgreSqlSecretFixture secrets = new();
        NpgsqlConnectionStringBuilder connection = new(secrets.Connection(PostgreSqlComponent.Migrator))
        {
            Host = "sitefactory-offline-factory.invalid",
            Port = 1,
            Timeout = 1,
        };
        secrets.Write(PostgreSqlComponent.Migrator, connection.ConnectionString);
        PostgreSqlRuntimeConfiguration configuration = await ReadAsync(secrets, PostgreSqlComponent.Migrator);
        File.Delete(secrets.PathFor(PostgreSqlComponent.Migrator));
        using RuntimeDbContext context = RuntimeDbContextFactory.Create(configuration);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Equal("sitefactory_runtime", context.Model.GetDefaultSchema());
        Assert.False(File.Exists(secrets.PathFor(PostgreSqlComponent.Migrator)));
        secrets.AssertRedacted(Assert.IsType<string>(context.ToString()));
    }

    private static Task<PostgreSqlRuntimeConfiguration> ReadAsync(PostgreSqlSecretFixture secrets, PostgreSqlComponent component) =>
        PostgreSqlConfigurationReader.ReadAsync(component, secrets.PathFor(component), "Test", Token);
}
