using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

// Catalog-level before/after assertions must not race another test allocating owned resources.
// This affects only the new P02 collection, not the existing G04/G05/P01/P03 collections.
[CollectionDefinition(nameof(P02TestScope), DisableParallelization = true)]
public sealed class P02TestScope;

[Collection(nameof(P02TestScope))]
[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P02")]
public sealed class PostgreSqlIntegrationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see docs/extraction/SF-TASK-062-G01-allocation.md.")]
    public async Task RealPostgreSqlPersistsDataEnforcesConstraintsAndIsolatesDatabases()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string firstName;
        string secondName;
        await using (PostgreSqlTestDatabase first = await PostgreSqlTestDatabase.CreateAsync(configuration, Token))
        await using (PostgreSqlTestDatabase second = await PostgreSqlTestDatabase.CreateAsync(configuration, Token))
        {
            firstName = first.Name;
            secondName = second.Name;
            Assert.NotEqual(firstName, secondName);
            await first.ExecuteAsync(async (connection, token) =>
            {
                await using NpgsqlCommand command = new("CREATE SCHEMA harness; CREATE TABLE harness.probe (id integer PRIMARY KEY); INSERT INTO harness.probe VALUES (1)", connection);
                await command.ExecuteNonQueryAsync(token);
                command.CommandText = "INSERT INTO harness.probe VALUES (1)";
                PostgresException duplicate = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(token));
                Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
            }, Token);
            // A different, non-pooled connection must observe the committed insertion.
            await first.ExecuteAsync(async (connection, token) =>
            {
                await using NpgsqlCommand command = new("SELECT id FROM harness.probe", connection);
                Assert.Equal(1, await command.ExecuteScalarAsync(token));
                command.CommandText = "SELECT pg_catalog.count(*) FROM harness.probe";
                Assert.Equal(1L, await command.ExecuteScalarAsync(token));
            }, Token);
            await second.ExecuteAsync(async (connection, token) =>
            {
                await using NpgsqlCommand command = new("SELECT pg_catalog.to_regclass('harness.probe') IS NULL", connection);
                Assert.Equal(true, await command.ExecuteScalarAsync(token));
            }, Token);
        }
        await AssertAbsentAsync(configuration, firstName, secondName);
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see docs/extraction/SF-TASK-062-G01-allocation.md.")]
    public async Task CancellationDoesNotPreventCleanupOrCreateAnotherDatabase()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string[] initial = await OwnedResourcesAsync(configuration);
        using CancellationTokenSource cancelled = new();
        string name;
        await using (PostgreSqlTestDatabase database = await PostgreSqlTestDatabase.CreateAsync(configuration, Token))
        {
            name = database.Name;
            string[] before = await OwnedResourcesAsync(configuration);
            await cancelled.CancelAsync();
            bool operationCalled = false;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.ExecuteAsync((_, _) =>
            {
                operationCalled = true;
                return Task.CompletedTask;
            }, cancelled.Token));
            Assert.False(operationCalled);
            PostgreSqlTestDatabase? unexpected = null;
            try
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    unexpected = await PostgreSqlTestDatabase.CreateAsync(configuration, cancelled.Token));
            }
            finally
            {
                if (unexpected is not null) await unexpected.DisposeAsync();
            }
            string[] after = await OwnedResourcesAsync(configuration);
            Assert.True(before.SequenceEqual(after, StringComparer.Ordinal),
                "Cancellation changed the owned database/role inventory.");
        }
        // The cancelled token is deliberately not used for disposal or its verification.
        await AssertAbsentAsync(configuration, name);
        string[] final = await OwnedResourcesAsync(configuration);
        Assert.True(initial.SequenceEqual(final, StringComparer.Ordinal),
            "Cancellation left an owned database or role behind.");
        // TestDatabase requires no secret hand-off file; this scenario creates no filesystem resources.
    }

    private static async Task<string[]> OwnedResourcesAsync(PostgreSqlTestConfiguration configuration)
    {
        await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
        await admin.OpenAsync(Token);
        await using NpgsqlCommand query = new("""
            SELECT 'database|' || datname::pg_catalog.text FROM pg_catalog.pg_database
            WHERE datname OPERATOR(pg_catalog.~) '^sf_test_[0-9a-f]{32}$'
            UNION ALL
            SELECT 'role|' || rolname::pg_catalog.text FROM pg_catalog.pg_roles
            WHERE rolname OPERATOR(pg_catalog.~) '^sf_[0-9a-f]{32}_test_(owner|migrator|web|worker)$'
            ORDER BY 1
            """, admin);
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(Token);
        List<string> rows = [];
        while (await reader.ReadAsync(Token)) rows.Add(reader.GetString(0));
        return rows.ToArray();
    }

    private static async Task AssertAbsentAsync(PostgreSqlTestConfiguration configuration, params string[] names)
    {
        try
        {
            await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await admin.OpenAsync(Token);
            await using NpgsqlCommand query = new("SELECT pg_catalog.count(*) FROM pg_catalog.pg_database WHERE datname = ANY($1)", admin);
            query.Parameters.AddWithValue(names);
            Assert.Equal(0L, await query.ExecuteScalarAsync(Token));
            query.CommandText = "SELECT pg_catalog.count(*) FROM pg_catalog.pg_stat_activity WHERE datname = ANY($1)";
            Assert.Equal(0L, await query.ExecuteScalarAsync(Token));
        }
        catch (NpgsqlException)
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-009 Could not verify temporary database cleanup.");
        }
    }
}
