using System.Globalization;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Collection(nameof(P02TestScope))]
[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P02")]
public sealed class PostgreSqlPermissionMatrixTests
{
    private static readonly string[] MetadataTables = ["database_identity", "schema_state"];
    private static readonly string[] MetadataUpdates =
    [
        "UPDATE sitefactory_runtime.database_identity SET client_slug = 'permission-probe'",
        "UPDATE sitefactory_runtime.schema_state SET current_version = 'permission-probe'",
    ];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see docs/extraction/SF-TASK-062-G01-allocation.md.")]
    public async Task GeneratedMetadataPermissionsAreEnforcedByPostgreSqlForEveryLogin()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-p02-permissions-").FullName;
        int refused = 0, authorized = 0;
        try
        {
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string migrator = Path.Combine(sandbox, "migrator-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, migrator, Token);
            // Setup uses the current public executable and helpers, never a G04 Fact or a PME host.
            Assert.Equal(0, (await PostgreSqlMigrationTests.ApplyAsync(sandbox, migrator)).ExitCode);
            Assert.Equal(0, (await PostgreSqlBootstrapTests.BootstrapAsync(sandbox, migrator)).ExitCode);
            string[] initial = await SnapshotAsync();

            foreach (PostgreSqlTestLogin login in Enum.GetValues<PostgreSqlTestLogin>())
            {
                await fixture.ExecuteAsync(login, async (connection, token) =>
                {
                    string phase = "identity";
                    try
                    {
                        await AssertIdentityAsync(connection, fixture.Name(login), fixture.Name(login), token);
                        if (login == PostgreSqlTestLogin.Migrator)
                        {
                            phase = "before-owner";
                            foreach (string table in MetadataTables)
                                await DeniedAsync(connection, "SELECT * FROM sitefactory_runtime." + table, token);
                            foreach (string sql in MetadataUpdates) await DeniedAsync(connection, sql, token);
                            await DeniedAsync(connection, "CREATE TABLE sitefactory_runtime.permission_probe (id integer)", token);
                            await AssertClusterCreationDeniedAsync(connection, token);
                            await ExecuteAsync(connection, $"SET ROLE \"{fixture.Baseline.Owner}\"", token);
                            await AssertIdentityAsync(connection, fixture.Baseline.Migrator, fixture.Baseline.Owner, token);
                        }

                        foreach (string table in MetadataTables)
                        {
                            phase = "metadata-read";
                            await using NpgsqlCommand read = new("SELECT pg_catalog.count(*) FROM sitefactory_runtime." + table, connection);
                            Assert.Equal(1L, await read.ExecuteScalarAsync(token));
                            authorized++;
                        }
                        foreach (string sql in MetadataUpdates)
                        {
                            phase = "metadata-update";
                            if (login == PostgreSqlTestLogin.Migrator)
                            {
                                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
                                Assert.Equal(1, await ExecuteAsync(connection, sql, token));
                                authorized++;
                                await transaction.RollbackAsync(token);
                                await AssertSnapshotAsync(initial);
                            }
                            else await DeniedAsync(connection, sql, token);
                        }

                        if (login == PostgreSqlTestLogin.Migrator)
                        {
                            phase = "owner-ddl";
                            await ExecuteAsync(connection, "CREATE TABLE sitefactory_runtime.permission_probe (id integer)", token);
                            await ExecuteAsync(connection, "ALTER TABLE sitefactory_runtime.permission_probe ADD COLUMN value text", token);
                            await ExecuteAsync(connection, "DROP TABLE sitefactory_runtime.permission_probe", token);
                            authorized += 3;
                        }
                        else
                        {
                            phase = "runtime-ddl";
                            await DeniedAsync(connection, "CREATE TABLE sitefactory_runtime.permission_probe (id integer)", token);
                            foreach (string table in MetadataTables)
                            {
                                await DeniedAsync(connection, "ALTER TABLE sitefactory_runtime." + table + " ADD COLUMN forbidden integer", token);
                                await DeniedAsync(connection, "DROP TABLE sitefactory_runtime." + table, token);
                            }
                        }
                        phase = "cluster-creation";
                        await AssertClusterCreationDeniedAsync(connection, token);
                        if (login == PostgreSqlTestLogin.Migrator)
                        {
                            phase = "reset-owner";
                            await ExecuteAsync(connection, "RESET ROLE", token);
                            await AssertIdentityAsync(connection, fixture.Baseline.Migrator, fixture.Baseline.Migrator, token);
                            // Name-based table lookup itself requires schema USAGE, intentionally denied
                            // after RESET ROLE. OIDs let us inspect privileges without granting that access.
                            await using NpgsqlCommand reset = new("""
                                SELECT c.relname::pg_catalog.text,
                                       pg_catalog.has_schema_privilege(current_user, n.oid, 'CREATE'),
                                       pg_catalog.has_table_privilege(current_user, c.oid, 'SELECT'),
                                       pg_catalog.has_table_privilege(current_user, c.oid, 'UPDATE')
                                FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n
                                    ON n.oid OPERATOR(pg_catalog.=) c.relnamespace
                                WHERE n.nspname OPERATOR(pg_catalog.=) 'sitefactory_runtime'
                                    AND c.relname IN ('database_identity', 'schema_state')
                                ORDER BY c.relname
                                """, connection);
                            await using NpgsqlDataReader reader = await reset.ExecuteReaderAsync(token);
                            foreach (string table in MetadataTables)
                            {
                                Assert.True(await reader.ReadAsync(token));
                                Assert.Equal(table, reader.GetString(0));
                                for (int column = 1; column <= 3; column++) Assert.False(reader.GetBoolean(column));
                            }
                            Assert.False(await reader.ReadAsync(token));
                        }
                    }
                    catch (PostgresException error)
                    {
                        // Fixed phase/login identifiers and SQLSTATE only; never provider text or SQL.
                        throw new InvalidOperationException($"P02 {login}/{phase} failed with SQLSTATE {error.SqlState}.");
                    }
                }, Token);
                await AssertSnapshotAsync(initial);
            }
            Assert.Equal(27, refused);
            Assert.Equal(11, authorized);

            Task<string[]> SnapshotAsync() => PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token);
            async Task AssertSnapshotAsync(string[] expected)
            {
                string[] actual = await SnapshotAsync();
                Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal), "Permission operation changed structures, metadata or ACLs.");
            }

            async Task DeniedAsync(NpgsqlConnection connection, string sql, CancellationToken token)
            {
                string[] before = await SnapshotAsync();
                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
                await using NpgsqlCommand command = new(sql, connection, transaction);
                PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(token));
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
                await transaction.RollbackAsync(token);
                refused++;
                await AssertSnapshotAsync(before);
            }

            async Task AssertClusterCreationDeniedAsync(NpgsqlConnection connection, CancellationToken token)
            {
                string name = "sf_test_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                await DeniedAsync(connection, $"CREATE ROLE \"{name}\" NOLOGIN", token);
                Assert.True(PostgreSqlTestDatabase.IsOwnedName(name));
                string[] before = await SnapshotAsync();
                try
                {
                    await using NpgsqlCommand create = new($"CREATE DATABASE \"{name}\" TEMPLATE template0", connection);
                    PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync(token));
                    Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
                    refused++;
                }
                finally
                {
                    using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
                    await fixture.Database.ExecuteAsync(async (admin, cleanupToken) =>
                        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", cleanupToken), cleanup.Token);
                }
                await AssertSnapshotAsync(before);
            }
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-p02-permissions-", Path.GetFileName(sandbox), StringComparison.Ordinal);
            Directory.Delete(sandbox, recursive: true);
        }
        Assert.False(Directory.Exists(sandbox));
    }

    private static async Task AssertIdentityAsync(NpgsqlConnection connection, string session, string current, CancellationToken token)
    {
        await using NpgsqlCommand query = new("""
            SELECT session_user::pg_catalog.text, current_user::pg_catalog.text, rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls
            FROM pg_catalog.pg_roles WHERE rolname OPERATOR(pg_catalog.=) current_user
            """, connection);
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token));
        Assert.Equal(session, reader.GetString(0));
        Assert.Equal(current, reader.GetString(1));
        for (int column = 2; column <= 6; column++) Assert.False(reader.GetBoolean(column));
        Assert.False(await reader.ReadAsync(token));
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return await command.ExecuteNonQueryAsync(token);
    }
}
