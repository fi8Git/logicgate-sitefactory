using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Cli.Tests;
using LogicGate.SiteFactory.Migrator;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Collection(nameof(PostgreSqlMigrationTests))]
public sealed class PostgreSqlMigrationTests
{
    private const string MigrationId = "20260923093309_CreateRuntimeMetadata";
    private static readonly string[] RuntimeTables = ["database_identity", "schema_state"];
    private static readonly string[] ExpectedColumns =
    [
        "__EFMigrationsHistory|MigrationId|character varying|150|NO|",
        "__EFMigrationsHistory|ProductVersion|character varying|32|NO|",
        "database_identity|database_instance_id|uuid||NO|",
        "database_identity|client_slug|character varying|80|NO|",
        "database_identity|environment|character varying|32|NO|",
        "database_identity|created_at_utc|timestamp with time zone||NO|",
        "database_identity|provisioning_version|character varying|64|NO|",
        "database_identity|database_owner_role_id|character varying|100|NO|",
        "database_identity|singleton|boolean||NO|true",
        "schema_state|schema_family|character varying|100|NO|",
        "schema_state|current_version|character varying|64|NO|",
        "schema_state|last_migration_id|character varying|150|YES|",
        "schema_state|migration_state|character varying|32|NO|",
        "schema_state|updated_at_utc|timestamp with time zone||NO|",
        "schema_state|updated_by_operation_id|uuid||NO|",
    ];
    private static readonly string[] ExpectedRelations =
    [
        "PK___EFMigrationsHistory|i", "__EFMigrationsHistory|r", "database_identity|r", "pk_database_identity|i",
        "pk_schema_state|i", "schema_state|r", "ux_database_identity_singleton|i",
    ];
    private static readonly string[] ForbiddenRuntimeCommands =
    [
        "CREATE TABLE sitefactory_runtime.forbidden (id integer)",
        "ALTER TABLE sitefactory_runtime.database_identity ADD COLUMN forbidden integer",
        "DROP TABLE sitefactory_runtime.schema_state",
        "DELETE FROM sitefactory_runtime.\"__EFMigrationsHistory\"",
    ];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see tests/PostgreSql/README.md.")]
    public async Task ExtractedMigratorAppliesOnceWithItsOwnLoginAndRejectsRuntimeCredentials()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-migration-e2e-").FullName;
        try
        {
            string project = Directory.CreateDirectory(Path.Combine(sandbox, "working directory")).FullName;
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            // SF-043 bootstrap provides the empty runtime schema and ACLs, not any EF table or migration.
            await AssertEmptyAsync(fixture);
            foreach (PostgreSqlTestLogin login in new[] { PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker })
            {
                string file = Path.Combine(sandbox, login + "-connection");
                await fixture.WriteConnectionFileAsync(login, file, Token);
                AssertDenied(await ApplyAsync(project, file));
                await AssertEmptyAsync(fixture);
            }

            // Supplying the bootstrap credential to the generated executable must not authorize migration.
            string administrativeFile = Path.Combine(sandbox, "administrative-connection");
            using (NpgsqlConnection admin = configuration.Connection(fixture.Database.Name))
                await PostgreSqlRoleFixture.WritePrivateConnectionAsync(admin.ConnectionString, administrativeFile, Token);
            AssertDenied(await ApplyAsync(project, administrativeFile));
            await AssertEmptyAsync(fixture);

            string migratorFile = Path.Combine(sandbox, "migrator-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, migratorFile, Token);
            // Malformed provisioning is rejected before EF creates even its history table; no automatic repair.
            foreach ((string enable, string reset) in new (string, string)[]
            {
                ($"ALTER ROLE \"{fixture.Baseline.Migrator}\" INHERIT", $"ALTER ROLE \"{fixture.Baseline.Migrator}\" NOINHERIT"),
                ($"ALTER ROLE \"{fixture.Baseline.Owner}\" LOGIN", $"ALTER ROLE \"{fixture.Baseline.Owner}\" NOLOGIN"),
                ($"GRANT \"{fixture.Baseline.Owner}\" TO \"{fixture.Baseline.Migrator}\" WITH SET FALSE",
                    $"GRANT \"{fixture.Baseline.Owner}\" TO \"{fixture.Baseline.Migrator}\" WITH SET TRUE"),
            })
            {
                await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection, enable, token), Token);
                try
                {
                    AssertDenied(await ApplyAsync(project, migratorFile));
                    await AssertEmptyAsync(fixture);
                }
                finally
                {
                    await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection, reset, token), Token);
                }
            }

            string[] filesBefore = SnapshotFiles(project);
            MigratorProcessResult first = await ApplyAsync(project, migratorFile);
            AssertSuccess(first);
            string[] firstSchema = await InspectSchemaAsync(fixture);
            MigratorProcessResult second = await ApplyAsync(project, migratorFile);
            AssertSuccess(second);
            Assert.Equal(first.StandardOutput, second.StandardOutput);
            Assert.True(firstSchema.AsEnumerable().SequenceEqual(await InspectSchemaAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Schema changed.");
            Assert.Equal(filesBefore, SnapshotFiles(project));

            foreach (PostgreSqlTestLogin login in new[] { PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker })
            {
                // A populated history must not turn an unauthorized migration invocation into success either.
                AssertDenied(await ApplyAsync(project, Path.Combine(sandbox, login + "-connection")));
                await fixture.ExecuteAsync(login, async (connection, token) =>
                {
                    Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM sitefactory_runtime.database_identity", token));
                    Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM sitefactory_runtime.schema_state", token));
                    foreach (string sql in ForbiddenRuntimeCommands)
                    {
                        await using NpgsqlCommand command = new(sql, connection);
                        PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(token));
                        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
                    }
                }, Token);
            }
            Assert.Equal(firstSchema, await InspectSchemaAsync(fixture));
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                // All generated Migrator invocations exited and closed their non-pooled sessions.
                await using NpgsqlCommand sessions = new("SELECT count(*) FROM pg_catalog.pg_stat_activity WHERE usename = $1", connection);
                sessions.Parameters.AddWithValue(fixture.Baseline.Migrator);
                Assert.Equal(0L, await sessions.ExecuteScalarAsync(token));
            }, Token);
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-migration-e2e-", Path.GetFileName(sandbox), StringComparison.Ordinal);
            Directory.Delete(sandbox, recursive: true);
        }
    }

    internal static Task<MigratorProcessResult> ApplyAsync(string project, string connectionFile) =>
        MigratorProcessRunner.RunAsync(project, ["apply", "--environment", "Test", "--confirm-migration"], connectionFile, Token);

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see docs/extraction/SF-TASK-062-G01-allocation.md.")]
    public async Task ExtractedMigratorPhysicallyClosesElevatedSessionsInsideTheLiveTestHost()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-migrator-sessions-").FullName;
        try
        {
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string file = Path.Combine(sandbox, "migrator-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, file, Token);
            string[] bootstrap = ["bootstrap", "--environment", "Test", "--client-slug", "example-client",
                "--database-instance-id", "11111111-1111-4111-8111-111111111111",
                "--operation-id", "22222222-2222-4222-8222-222222222222", "--confirm-bootstrap"];

            // The host remains alive: process termination cannot hide an elevated pooled session.
            MigratorProcessResult missingHistory = await RunInProcessAsync(file, bootstrap, Token);
            Assert.Equal(1, missingHistory.ExitCode);
            Assert.Equal("SF-BOOTSTRAP-HISTORY-001 Apply the exact initial migration before bootstrap." + Environment.NewLine,
                Encoding.UTF8.GetString(missingHistory.StandardError));
            await AssertNoSessionsAsync(fixture);
            AssertSuccess(await RunInProcessAsync(file, ["apply", "--environment", "Test", "--confirm-migration"], Token));
            await AssertNoSessionsAsync(fixture);
            string[] before = await PostgreSqlBootstrapTests.SnapshotMetadataAsync(fixture);

            await using (NpgsqlConnection blocking = configuration.Connection(fixture.Database.Name))
            {
                await blocking.OpenAsync(Token);
                await using NpgsqlTransaction transaction = await blocking.BeginTransactionAsync(Token);
                await ExecuteAsync(blocking, "LOCK TABLE sitefactory_runtime.schema_state IN ACCESS EXCLUSIVE MODE", Token);
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
                Task<MigratorProcessResult> pending = RunInProcessAsync(file, bootstrap, cancellation.Token);
                try
                {
                    await WaitForSessionAsync(fixture, blocked: true);
                    await cancellation.CancelAsync();
                    MigratorProcessResult cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(10), Token);
                    Assert.Equal(130, cancelled.ExitCode);
                    Assert.Equal("SF-BOOTSTRAP-CANCELLED-001 Bootstrap cancelled; inspect database state before retrying." + Environment.NewLine,
                        Encoding.UTF8.GetString(cancelled.StandardError));
                }
                finally
                {
                    await cancellation.CancelAsync();
                    await pending.WaitAsync(TimeSpan.FromSeconds(10), Token);
                }
                await transaction.RollbackAsync(Token);
            }
            await AssertNoSessionsAsync(fixture);
            Assert.True(before.AsEnumerable().SequenceEqual(await PostgreSqlBootstrapTests.SnapshotMetadataAsync(fixture), StringComparer.Ordinal),
                "SF-TEST-PG-PROOF Cancelled bootstrap changed metadata.");
            MigratorProcessResult initialized = await RunInProcessAsync(file, bootstrap, Token);
            Assert.Equal(0, initialized.ExitCode);
            await AssertNoSessionsAsync(fixture);

            await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection,
                $"REVOKE \"{fixture.Baseline.Owner}\" FROM \"{fixture.Baseline.Migrator}\"", token), Token);
            AssertDenied(await RunInProcessAsync(file, bootstrap, Token));
            await AssertNoSessionsAsync(fixture);
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-migrator-sessions-", Path.GetFileName(sandbox), StringComparison.Ordinal);
            Directory.Delete(sandbox, recursive: true);
        }
    }

    private static async Task<MigratorProcessResult> RunInProcessAsync(string file, string[] args, CancellationToken token)
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Process(args);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        int exit = await MigrationCommand.RunAsync(args, file, output, error, token);
        return MigratorProcessRunner.ValidateResult(new CliBytesResult(exit,
            Encoding.UTF8.GetBytes(output.ToString()), Encoding.UTF8.GetBytes(error.ToString())));
    }

    private static Task AssertNoSessionsAsync(PostgreSqlRoleFixture fixture) => WaitForSessionAsync(fixture, blocked: false);

    private static async Task WaitForSessionAsync(PostgreSqlRoleFixture fixture, bool blocked)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(2))
        {
            long count = -1;
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                await using NpgsqlCommand query = new("""
                    SELECT pg_catalog.count(*) FROM pg_catalog.pg_stat_activity
                    WHERE datname OPERATOR(pg_catalog.=) $1 AND usename OPERATOR(pg_catalog.=) $2
                      AND (NOT $3 OR wait_event_type OPERATOR(pg_catalog.=) 'Lock')
                    """, connection);
                query.Parameters.AddWithValue(fixture.Database.Name);
                query.Parameters.AddWithValue(fixture.Baseline.Migrator);
                query.Parameters.AddWithValue(blocked);
                count = (long)(await query.ExecuteScalarAsync(token))!;
            }, Token);
            if (blocked ? count == 1 : count == 0) return;
            await Task.Delay(20, Token);
        }
        Assert.Fail(blocked ? "SF-TEST-PG-SESSION Expected lock wait was not observed." : "SF-TEST-PG-SESSION Residual elevated session.");
    }

    private static void AssertSuccess(MigratorProcessResult result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Equal("Runtime metadata migration completed." + Environment.NewLine, Encoding.UTF8.GetString(result.StandardOutput));
    }

    private static void AssertDenied(MigratorProcessResult result)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Equal("SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required." + Environment.NewLine,
            Encoding.UTF8.GetString(result.StandardError)); // Exact output: no SQL, connection, stack trace or secret.
    }

    private static Task AssertEmptyAsync(PostgreSqlRoleFixture fixture) => fixture.Database.ExecuteAsync(async (connection, token) =>
    {
        Assert.Empty(await RowsAsync(connection, """
            SELECT table_schema || '.' || table_name FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
            """, token));
    }, Token);

    private static async Task<string[]> InspectSchemaAsync(PostgreSqlRoleFixture fixture)
    {
        List<string> snapshot = [];
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            Assert.Equal(["public", "sitefactory_runtime"], await RowsAsync(connection, """
                SELECT nspname FROM pg_catalog.pg_namespace
                WHERE nspname <> 'information_schema' AND NOT pg_catalog.starts_with(nspname::text, 'pg_') ORDER BY nspname COLLATE "C"
                """, token));
            Assert.Equal(["sitefactory_runtime.__EFMigrationsHistory", "sitefactory_runtime.database_identity", "sitefactory_runtime.schema_state"],
                await RowsAsync(connection, """
                    SELECT table_schema || '.' || table_name FROM information_schema.tables
                    WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema COLLATE "C", table_name COLLATE "C"
                    """, token));
            Assert.Equal([MigrationId + "|10.0.12"], await RowsAsync(connection,
                "SELECT \"MigrationId\" || '|' || \"ProductVersion\" FROM sitefactory_runtime.\"__EFMigrationsHistory\"", token));
            foreach (string table in RuntimeTables)
                Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM sitefactory_runtime." + table, token));

            // Exact application relations, including technical indexes; no business tables, views or sequences.
            string[] relations = await RowsAsync(connection, """
                SELECT c.relname::text || '|' || c.relkind::text || '|' || pg_catalog.pg_get_userbyid(c.relowner)::text
                FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname NOT IN ('information_schema', 'pg_catalog') AND NOT pg_catalog.starts_with(n.nspname::text, 'pg_')
                ORDER BY c.relname COLLATE "C"
                """, token);
            Assert.Equal(ExpectedRelations.Select(name => name + "|" + fixture.Baseline.Owner), relations);
            Assert.Equal(0L, await ScalarAsync(connection, """
                SELECT count(*) FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname NOT IN ('information_schema', 'pg_catalog') AND NOT pg_catalog.starts_with(n.nspname::text, 'pg_')
                """, token));
            Assert.Equal(14L, await ScalarAsync(connection, """
                SELECT count(*) FROM pg_catalog.pg_constraint WHERE connamespace = 'sitefactory_runtime'::regnamespace AND contype <> 'n'
                """, token));
            // PostgreSQL 18 also catalogs the fourteen required columns as first-class NOT NULL constraints.
            Assert.Equal(14L, await ScalarAsync(connection, """
                SELECT count(*) FROM pg_catalog.pg_constraint WHERE connamespace = 'sitefactory_runtime'::regnamespace AND contype = 'n'
                """, token));

            snapshot.AddRange(relations);
            snapshot.AddRange(await RowsAsync(connection, """
                SELECT c.oid::text || '|' || c.relfilenode::text || '|' || c.relname::text || '|' || coalesce(c.relacl::text, '')
                FROM pg_catalog.pg_class c WHERE c.relnamespace = 'sitefactory_runtime'::regnamespace ORDER BY c.relname COLLATE "C"
                """, token));
            snapshot.AddRange(await RowsAsync(connection, """
                SELECT conname || '|' || pg_catalog.pg_get_constraintdef(oid) FROM pg_catalog.pg_constraint
                WHERE connamespace = 'sitefactory_runtime'::regnamespace ORDER BY conname COLLATE "C"
                """, token));
            string[] columns = await RowsAsync(connection, """
                SELECT table_name || '|' || column_name || '|' || data_type || '|' || coalesce(character_maximum_length::text, '')
                       || '|' || is_nullable || '|' || coalesce(column_default, '')
                FROM information_schema.columns WHERE table_schema = 'sitefactory_runtime'
                ORDER BY table_name COLLATE "C", ordinal_position
                """, token);
            Assert.Equal(ExpectedColumns, columns);
            snapshot.AddRange(columns);
            snapshot.AddRange(await RowsAsync(connection,
                "SELECT xmin::text || '|' || \"MigrationId\" || '|' || \"ProductVersion\" FROM sitefactory_runtime.\"__EFMigrationsHistory\"", token));
        }, Token);
        return snapshot.ToArray();
    }

    private static string[] SnapshotFiles(string project) => Directory.GetFiles(project, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(project, path) + "|" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return await command.ExecuteScalarAsync(token);
    }

    private static async Task<string[]> RowsAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        List<string> rows = [];
        await using NpgsqlCommand command = new(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(reader.GetString(0));
        return rows.ToArray();
    }
}
