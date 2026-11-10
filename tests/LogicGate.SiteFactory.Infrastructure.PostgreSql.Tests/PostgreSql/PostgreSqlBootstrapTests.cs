using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Collection(nameof(PostgreSqlMigrationTests))]
public sealed class PostgreSqlBootstrapTests
{
    private const string InstanceId = "11111111-1111-4111-8111-111111111111";
    private const string OperationId = "22222222-2222-4222-8222-222222222222";
    private const string OtherId = "33333333-3333-4333-8333-333333333333";
    private const string MigrationId = "20260923093309_CreateRuntimeMetadata";
    private static readonly PostgreSqlTestLogin[] RuntimeLogins = [PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker];
    private static readonly string[] MetadataTables = ["database_identity", "schema_state", "\"__EFMigrationsHistory\""];
    private static readonly string[] RuntimeWrites =
    [
        "INSERT INTO sitefactory_runtime.database_identity SELECT * FROM sitefactory_runtime.database_identity",
        "UPDATE sitefactory_runtime.database_identity SET client_slug = 'other-client'",
        "DELETE FROM sitefactory_runtime.database_identity",
        "INSERT INTO sitefactory_runtime.schema_state SELECT * FROM sitefactory_runtime.schema_state",
        "UPDATE sitefactory_runtime.schema_state SET current_version = '999'",
        "DELETE FROM sitefactory_runtime.schema_state",
    ];
    private static readonly (string Table, string Column, string Value, string Original)[] Corruptions =
    [
        ("database_identity", "provisioning_version", "999", "1"),
        ("database_identity", "database_owner_role_id", "wrong-owner", ""),
        ("schema_state", "current_version", "999", "1"),
        ("schema_state", "schema_family", "unknown", "platform"),
        ("schema_state", "last_migration_id", "unknown", MigrationId),
        ("schema_state", "migration_state", "migration-failed", "stable"),
    ];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see tests/PostgreSql/README.md.")]
    public async Task ExtractedBootstrapIsTransactionalIdempotentAndRefusesMismatchesAndRuntimeWrites()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-bootstrap-e2e-").FullName;
        try
        {
            string project = Directory.CreateDirectory(Path.Combine(sandbox, "working directory")).FullName;
            string[] files = SnapshotFiles(project);
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string migrator = Path.Combine(sandbox, "migrator-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, migrator, Token);
            AssertFailure(await BootstrapAsync(project, migrator), "SF-BOOTSTRAP-HISTORY-001 Apply the exact initial migration before bootstrap.");
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                Assert.Equal(0L, await ScalarAsync(connection,
                    "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'sitefactory_runtime'", token));
            }, Token);
            MigratorProcessResult applied = await PostgreSqlMigrationTests.ApplyAsync(project, migrator);
            Assert.Equal(0, applied.ExitCode);
            Assert.Empty(applied.StandardError);
            await AssertEmptyMetadataAsync(fixture);

            foreach (PostgreSqlTestLogin login in RuntimeLogins)
            {
                string file = Path.Combine(sandbox, login + "-connection");
                await fixture.WriteConnectionFileAsync(login, file, Token);
                AssertFailure(await BootstrapAsync(project, file),
                    "SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required.");
                await AssertEmptyMetadataAsync(fixture);
            }

            // Real server failure AFTER identity insert. The nontransactional sequence proves the trigger
            // observed that identity before rejecting SchemaState; production sources are not patched.
            await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection, $$"""
                CREATE SEQUENCE sitefactory_runtime.sf_test_bootstrap_probe;
                ALTER SEQUENCE sitefactory_runtime.sf_test_bootstrap_probe OWNER TO "{{fixture.Baseline.Owner}}";
                CREATE FUNCTION sitefactory_runtime.sf_test_reject_state() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF (SELECT count(*) FROM sitefactory_runtime.database_identity) = 1 THEN
                        PERFORM pg_catalog.nextval('sitefactory_runtime.sf_test_bootstrap_probe');
                    END IF;
                    RAISE EXCEPTION 'SF test-only injected failure';
                END $body$;
                CREATE TRIGGER sf_test_reject_state BEFORE INSERT ON sitefactory_runtime.schema_state
                    FOR EACH ROW EXECUTE FUNCTION sitefactory_runtime.sf_test_reject_state();
                """, token), Token);
            AssertFailure(await BootstrapAsync(project, migrator),
                "SF-BOOTSTRAP-FAILED-001 Bootstrap failed; inspect database state before retrying.");
            await AssertEmptyMetadataAsync(fixture);
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                Assert.Equal(true, await ScalarAsync(connection, "SELECT is_called FROM sitefactory_runtime.sf_test_bootstrap_probe", token));
                await ExecuteAsync(connection, """
                    DROP TRIGGER sf_test_reject_state ON sitefactory_runtime.schema_state;
                    DROP FUNCTION sitefactory_runtime.sf_test_reject_state();
                    DROP SEQUENCE sitefactory_runtime.sf_test_bootstrap_probe;
                    """, token);
            }, Token);

            DateTimeOffset before = TimeProvider.System.GetUtcNow();
            AssertSuccess(await BootstrapAsync(project, migrator));
            await VerifyInitialMetadataAsync(fixture, before, TimeProvider.System.GetUtcNow());
            string[] first = await SnapshotMetadataAsync(fixture);
            AssertSuccess(await BootstrapAsync(project, migrator));
            Assert.True(first.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed."); // Includes xmin, original dates and operation ID.
            AssertSuccess(await BootstrapAsync(project, migrator, operation: OtherId));
            Assert.True(first.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");

            AssertFailure(await BootstrapAsync(project, migrator, client: "other-client"),
                "SF-BOOTSTRAP-CLIENT-001 Existing database client does not match the requested identity.");
            Assert.True(first.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");
            AssertFailure(await BootstrapAsync(project, migrator, environment: "Development"),
                "SF-BOOTSTRAP-ENVIRONMENT-001 Existing database environment does not match the requested identity.");
            Assert.True(first.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");
            AssertFailure(await BootstrapAsync(project, migrator, instance: OtherId),
                "SF-BOOTSTRAP-INSTANCE-001 Existing database instance does not match the requested identity.");
            Assert.True(first.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");

            foreach (PostgreSqlTestLogin login in RuntimeLogins)
            {
                AssertFailure(await BootstrapAsync(project, Path.Combine(sandbox, login + "-connection")),
                    "SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required.");
                await fixture.ExecuteAsync(login, async (connection, token) =>
                {
                    Assert.Equal(1L, await ScalarAsync(connection, "SELECT count(*) FROM sitefactory_runtime.database_identity", token));
                    foreach (string sql in RuntimeWrites)
                    {
                        await using NpgsqlCommand command = new(sql, connection);
                        PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(token));
                        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
                    }
                }, Token);
            }
            Assert.True(first.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");

            foreach ((string table, string column, string value, string original) in Corruptions)
            {
                await SetMetadataAsync(fixture, table, column, value);
                string[] corrupt = await SnapshotMetadataAsync(fixture);
                AssertFailure(await BootstrapAsync(project, migrator),
                    "SF-BOOTSTRAP-STATE-001 Existing runtime metadata is incomplete or incompatible; no repair was performed.");
                Assert.True(corrupt.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");
                await SetMetadataAsync(fixture, table, column, original.Length == 0 ? fixture.Baseline.Owner : original);
            }
            // Restore each isolated test document from a test-only copy, not by bootstrap/repair.
            foreach (string table in new[] { "database_identity", "schema_state" })
            {
                await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection,
                    "CREATE TABLE sitefactory_runtime.sf_test_saved AS TABLE sitefactory_runtime." + table
                    + "; DELETE FROM sitefactory_runtime." + table, token), Token);
                string[] partial = await SnapshotMetadataAsync(fixture);
                AssertFailure(await BootstrapAsync(project, migrator),
                    "SF-BOOTSTRAP-STATE-001 Existing runtime metadata is incomplete or incompatible; no repair was performed.");
                Assert.True(partial.AsEnumerable().SequenceEqual(await SnapshotMetadataAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");
                await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection,
                    "INSERT INTO sitefactory_runtime." + table + " SELECT * FROM sitefactory_runtime.sf_test_saved; DROP TABLE sitefactory_runtime.sf_test_saved", token), Token);
            }
            Assert.Equal(files, SnapshotFiles(project));
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-bootstrap-e2e-", Path.GetFileName(sandbox), StringComparison.Ordinal);
            Directory.Delete(sandbox, recursive: true);
        }
    }

    internal static Task<MigratorProcessResult> BootstrapAsync(string project, string file, string client = "example-client",
        string environment = "Test", string instance = InstanceId, string operation = OperationId) =>
        MigratorProcessRunner.RunAsync(project, ["bootstrap", "--environment", environment, "--client-slug", client,
            "--database-instance-id", instance, "--operation-id", operation, "--confirm-bootstrap"], file, Token);

    private static void AssertSuccess(MigratorProcessResult result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Equal("Runtime metadata bootstrap completed." + Environment.NewLine, Encoding.UTF8.GetString(result.StandardOutput));
    }

    private static void AssertFailure(MigratorProcessResult result, string diagnostic)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(diagnostic + Environment.NewLine, Encoding.UTF8.GetString(result.StandardError));
    }

    private static Task AssertEmptyMetadataAsync(PostgreSqlRoleFixture fixture) => fixture.Database.ExecuteAsync(async (connection, token) =>
    {
        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM sitefactory_runtime.database_identity", token));
        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM sitefactory_runtime.schema_state", token));
    }, Token);

    private static Task VerifyInitialMetadataAsync(PostgreSqlRoleFixture fixture, DateTimeOffset before, DateTimeOffset after) =>
        fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            await using NpgsqlCommand command = new("""
                SELECT i.database_instance_id, i.client_slug, i.environment, i.created_at_utc, i.provisioning_version,
                    i.database_owner_role_id, i.singleton, s.schema_family, s.current_version, s.last_migration_id,
                    s.migration_state, s.updated_at_utc, s.updated_by_operation_id
                FROM sitefactory_runtime.database_identity i CROSS JOIN sitefactory_runtime.schema_state s
                """, connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            Assert.True(await reader.ReadAsync(token));
            Assert.Equal(Guid.Parse(InstanceId), reader.GetGuid(0));
            Assert.Equal("example-client", reader.GetString(1));
            Assert.Equal("Test", reader.GetString(2));
            DateTimeOffset created = reader.GetFieldValue<DateTimeOffset>(3);
            Assert.Equal(TimeSpan.Zero, created.Offset);
            Assert.InRange(created, before, after);
            Assert.Equal("1", reader.GetString(4));
            Assert.Equal(fixture.Baseline.Owner, reader.GetString(5));
            Assert.True(reader.GetBoolean(6));
            Assert.Equal("platform", reader.GetString(7));
            Assert.Equal("1", reader.GetString(8));
            Assert.Equal(MigrationId, reader.GetString(9));
            Assert.Equal("stable", reader.GetString(10));
            Assert.Equal(created, reader.GetFieldValue<DateTimeOffset>(11));
            Assert.Equal(Guid.Parse(OperationId), reader.GetGuid(12));
            Assert.False(await reader.ReadAsync(token));
        }, Token);

    internal static async Task<string[]> SnapshotMetadataAsync(PostgreSqlRoleFixture fixture)
    {
        List<string> result = [];
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            foreach (string table in MetadataTables)
            {
                await using NpgsqlCommand command = new("SELECT xmin::text || '|' || row_to_json(t)::text FROM sitefactory_runtime." + table + " t ORDER BY 1", connection);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) result.Add(table + "|" + reader.GetString(0));
            }
        }, Token);
        return result.ToArray();
    }

    private static Task SetMetadataAsync(PostgreSqlRoleFixture fixture, string table, string column, string value) =>
        fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            // Table/column are exclusively fixed test constants; values are parameters.
            await using NpgsqlCommand command = new("UPDATE sitefactory_runtime." + table + " SET " + column + " = $1", connection);
            command.Parameters.AddWithValue(value);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
        }, Token);

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
}
