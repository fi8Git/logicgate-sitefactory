using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Migrator;
using LogicGate.SiteFactory.Runtime.Database;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "G05")]
[Collection(nameof(PostgreSqlMigrationTests))]
public sealed class PostgreSqlCompatibilityTests
{
    private static readonly Guid Instance = new("11111111-1111-4111-8111-111111111111");
    private static readonly string[] DangerousRoleFlags = ["SUPERUSER", "CREATEDB", "CREATEROLE", "REPLICATION", "BYPASSRLS", "INHERIT"];
    private static readonly (string Column, string Value, string Original, string Code)[] MetadataCases =
    [
        ("schema_family", "other", "platform", "SF-SCHEMA-STARTUP-009"),
        ("current_version", "2", "1", "SF-SCHEMA-STARTUP-009"),
        ("migration_state", "migration-planned", "stable", "SF-SCHEMA-STARTUP-005"),
        ("migration_state", "migration-running", "stable", "SF-SCHEMA-STARTUP-005"),
        ("migration_state", "migration-failed", "stable", "SF-SCHEMA-STARTUP-006"),
        ("migration_state", "restore-pending", "stable", "SF-SCHEMA-STARTUP-009"),
        ("last_migration_id", "different", "20260923093309_CreateRuntimeMetadata", "SF-SCHEMA-STARTUP-008"),
    ];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see tests/PostgreSql/README.md.")]
    public async Task ExtractedReaderAndProbeDistinguishCompatibilityFailuresWithoutChangingPostgreSql()
    {
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-probe-e2e-").FullName;
        try
        {
            DirectProbe probe = new(Instance);
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string migrator = Path.Combine(sandbox, "migration-connection");
            string web = Path.Combine(sandbox, "web-connection");
            string worker = Path.Combine(sandbox, "worker-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, migrator, Token);
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Web, web, Token);
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Worker, worker, Token);
            Assert.Equal(0, await SetupAsync(migrator, bootstrap: false));
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-003", "SF-SCHEMA-STARTUP-001"]);
            Assert.Equal(0, await SetupAsync(migrator, bootstrap: true));
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["PASS"]);
            await CheckAsync(fixture, probe, worker, fixture.Baseline.Worker, ["PASS"], component: "Worker");

            await AdminAsync(fixture, """
                CREATE TABLE sitefactory_runtime.sf_test_saved_state AS TABLE sitefactory_runtime.schema_state;
                DELETE FROM sitefactory_runtime.schema_state;
                """);
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-SCHEMA-STARTUP-001"]);
            await AdminAsync(fixture, """
                INSERT INTO sitefactory_runtime.schema_state SELECT * FROM sitefactory_runtime.sf_test_saved_state;
                DROP TABLE sitefactory_runtime.sf_test_saved_state;
                """);
            await VerifyNonTableMetadataAsync(fixture, probe, web);

            await VerifyTransitiveMembershipAsync(fixture, probe, web, worker);

            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-004"], client: "different-client");
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-005"], environment: "Development");
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-006"],
                instance: new("33333333-3333-4333-8333-333333333333"));
            // Same privileges do not make Worker the expected Web login. No name is inferred from credentials.
            await CheckAsync(fixture, probe, worker, fixture.Baseline.Web, ["SF-DB-STARTUP-007"]);
            await CheckAsync(fixture, probe, migrator, fixture.Baseline.Web, ["SF-DB-STARTUP-007", "SF-DB-STARTUP-008"]);

            foreach ((string column, string value, string original, string code) in MetadataCases)
            {
                await SetStateAsync(fixture, column, value);
                await CheckAsync(fixture, probe, web, fixture.Baseline.Web, [code]);
                await SetStateAsync(fixture, column, original);
            }
            await AdminAsync(fixture, """
                CREATE TABLE sitefactory_runtime.sf_test_saved_history AS TABLE sitefactory_runtime."__EFMigrationsHistory";
                DELETE FROM sitefactory_runtime."__EFMigrationsHistory";
                """);
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-SCHEMA-STARTUP-004"]);
            await AdminAsync(fixture, """
                INSERT INTO sitefactory_runtime."__EFMigrationsHistory" SELECT * FROM sitefactory_runtime.sf_test_saved_history;
                DROP TABLE sitefactory_runtime.sf_test_saved_history;
                INSERT INTO sitefactory_runtime."__EFMigrationsHistory" VALUES ('000-unknown', '10.0.12'), ('001-unknown', '10.0.12');
                """);
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-SCHEMA-STARTUP-007"]);
            RuntimeDatabaseObservation bounded = await ReadObservationAsync(web, Token);
            Assert.Equal(2, bounded.Migrations.Count);
            Assert.Contains("20260923093309_CreateRuntimeMetadata", bounded.Migrations);
            await AdminAsync(fixture, "DELETE FROM sitefactory_runtime.\"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('000-unknown', '001-unknown')");

            await AdminAsync(fixture, """
                ALTER TABLE sitefactory_runtime.database_identity DROP CONSTRAINT ck_database_identity_instance_id;
                ALTER TABLE sitefactory_runtime.database_identity ALTER COLUMN database_instance_id TYPE text USING database_instance_id::text;
                """);
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-SCHEMA-STARTUP-009"]);
            await AdminAsync(fixture, """
                ALTER TABLE sitefactory_runtime.database_identity ALTER COLUMN database_instance_id TYPE uuid USING database_instance_id::uuid;
                ALTER TABLE sitefactory_runtime.database_identity ADD CONSTRAINT ck_database_identity_instance_id
                    CHECK (database_instance_id <> '00000000-0000-0000-0000-000000000000'::uuid);
                """);

            await AdminAsync(fixture, $"REVOKE SELECT ON sitefactory_runtime.database_identity FROM \"{fixture.Baseline.Web}\"");
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-009"]);
            await AdminAsync(fixture, $"GRANT SELECT ON sitefactory_runtime.database_identity TO \"{fixture.Baseline.Web}\"");

            foreach (string flag in DangerousRoleFlags)
            {
                await AdminAsync(fixture, $"ALTER ROLE \"{fixture.Baseline.Web}\" {flag}");
                await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-008"]);
                await AdminAsync(fixture, $"ALTER ROLE \"{fixture.Baseline.Web}\" NO{flag}");
            }
            foreach ((string grant, string revoke) in new (string, string)[]
            {
                ($"GRANT \"{fixture.Baseline.Owner}\" TO \"{fixture.Baseline.Web}\" WITH INHERIT FALSE; GRANT \"{fixture.Baseline.Owner}\" TO \"{fixture.Baseline.Web}\" WITH SET TRUE",
                    $"REVOKE \"{fixture.Baseline.Owner}\" FROM \"{fixture.Baseline.Web}\""),
                ($"GRANT CREATE ON SCHEMA sitefactory_runtime TO \"{fixture.Baseline.Web}\"",
                    $"REVOKE CREATE ON SCHEMA sitefactory_runtime FROM \"{fixture.Baseline.Web}\""),
                ($"GRANT CREATE ON SCHEMA public TO PUBLIC", "REVOKE CREATE ON SCHEMA public FROM PUBLIC"),
                ($"GRANT TEMP ON DATABASE \"{fixture.Database.Name}\" TO \"{fixture.Baseline.Web}\"",
                    $"REVOKE TEMP ON DATABASE \"{fixture.Database.Name}\" FROM \"{fixture.Baseline.Web}\""),
                ($"GRANT UPDATE(client_slug) ON sitefactory_runtime.database_identity TO \"{fixture.Baseline.Web}\"",
                    $"REVOKE UPDATE(client_slug) ON sitefactory_runtime.database_identity FROM \"{fixture.Baseline.Web}\""),
                ($"GRANT SELECT ON sitefactory_runtime.database_identity TO \"{fixture.Baseline.Web}\" WITH GRANT OPTION",
                    $"REVOKE GRANT OPTION FOR SELECT ON sitefactory_runtime.database_identity FROM \"{fixture.Baseline.Web}\""),
                ($"ALTER TABLE sitefactory_runtime.database_identity OWNER TO \"{fixture.Baseline.Web}\"",
                    $"ALTER TABLE sitefactory_runtime.database_identity OWNER TO \"{fixture.Baseline.Owner}\"; GRANT SELECT ON sitefactory_runtime.database_identity TO \"{fixture.Baseline.Web}\""),
            })
            {
                await AdminAsync(fixture, grant);
                await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-008"]);
                await AdminAsync(fixture, revoke);
            }
            await AdminAsync(fixture, $"ALTER ROLE \"{fixture.Baseline.Web}\" IN DATABASE \"{fixture.Database.Name}\" SET search_path = public");
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-DB-STARTUP-007"]);
            await AdminAsync(fixture, $"ALTER ROLE \"{fixture.Baseline.Web}\" IN DATABASE \"{fixture.Database.Name}\" SET search_path = pg_catalog, sitefactory_runtime");

            // A blocked read must time out without DML or unbounded host waiting.
            string[] beforeLock = await PostgreSqlBootstrapTests.SnapshotMetadataAsync(fixture);
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
                await using NpgsqlCommand locked = new("LOCK TABLE sitefactory_runtime.schema_state IN ACCESS EXCLUSIVE MODE", connection, transaction);
                await locked.ExecuteNonQueryAsync(token);
                Stopwatch elapsed = Stopwatch.StartNew();
                string[] result = await probe.RunAsync(web, fixture.Baseline.Web, Token);
                AssertCodes(result, ["SF-DB-STARTUP-001"]);
                Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), "Probe timeout exceeded its bounded budget.");
            }, Token);
            Assert.Equal(beforeLock, await PostgreSqlBootstrapTests.SnapshotMetadataAsync(fixture));

            // Keep a loopback port allocated but never speak PostgreSQL: connect/handshake cannot wait forever.
            using TcpListener silent = new(IPAddress.Loopback, 0);
            silent.Start();
            NpgsqlConnectionStringBuilder unavailable = new(await File.ReadAllTextAsync(web, Token))
            {
                Port = ((IPEndPoint)silent.LocalEndpoint).Port,
                Timeout = 999,
                CommandTimeout = 0,
            };
            string unreachable = Path.Combine(sandbox, "unreachable-connection");
            await PostgreSqlRoleFixture.WritePrivateConnectionAsync(unavailable.ConnectionString, unreachable, Token);
            Stopwatch unavailableTime = Stopwatch.StartNew();
            await CheckAsync(fixture, probe, unreachable, fixture.Baseline.Web, ["SF-DB-STARTUP-001"]);
            Assert.True(unavailableTime.Elapsed < TimeSpan.FromSeconds(10), "Unreachable PostgreSQL exceeded its bounded budget.");

            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.RunAsync(unreachable, fixture.Baseline.Web, cancellation.Token));
            await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["PASS"]);

        }
        finally
        {
            DeleteSandbox(sandbox, "sitefactory-probe-e2e-");
        }
    }

    private static async Task CheckAsync(PostgreSqlRoleFixture fixture, DirectProbe probe, string file, string login,
        string[] codes, string client = "example-client", string environment = "Test", string component = "Web", Guid? instance = null, bool readableMetadata = true)
    {
        string[] before = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, readableMetadata, Token);
        string[] actual = await probe.RunAsync(file, login, Token, client, environment, component, instance ?? Instance);
        NpgsqlConnectionStringBuilder secret = new(await File.ReadAllTextAsync(file, Token));
        // Check before an assertion that could include unexpected diagnostic content in test output.
        Assert.False(actual.Any(value => value.Contains(secret.Password!, StringComparison.Ordinal)
            || value.Contains(secret.ConnectionString, StringComparison.Ordinal)
            || value.Contains(secret.Username!, StringComparison.Ordinal) || value.Contains(secret.Host!, StringComparison.Ordinal)
            || value.Contains(file, StringComparison.Ordinal)
            || value.Contains("Exception", StringComparison.Ordinal)), "Probe diagnostics disclosed private connection material.");
        AssertCodes(actual, codes);
        await AssertNoProbeSessionsAsync(fixture);
        Assert.Equal(before, await PostgreSqlCatalogSnapshot.ReadAsync(fixture, readableMetadata, Token));
    }

    private static void AssertCodes(string[] actual, string[] codes) => Assert.Equal(codes, actual.Select(static result => result.Split('|')[0]));

    private static Task AdminAsync(PostgreSqlRoleFixture fixture, string sql) => fixture.Database.ExecuteAsync(async (connection, token) =>
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }, Token);

    private static Task SetStateAsync(PostgreSqlRoleFixture fixture, string column, string value) => fixture.Database.ExecuteAsync(async (connection, token) =>
    {
        await using NpgsqlCommand command = new("UPDATE sitefactory_runtime.schema_state SET " + column + " = $1", connection);
        command.Parameters.AddWithValue(value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
    }, Token);


    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see tests/PostgreSql/README.md.")]
    public async Task ExtractedReaderRejectsCreateOnNonSystemPgSchemasForWebAndWorker()
    {
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-schema-probe-").FullName;
        try
        {
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string migrator = Path.Combine(sandbox, "migration-connection");
            string web = Path.Combine(sandbox, "web-connection");
            string worker = Path.Combine(sandbox, "worker-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, migrator, Token);
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Web, web, Token);
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Worker, worker, Token);
            await SetupAsync(migrator, bootstrap: false);
            await SetupAsync(migrator, bootstrap: true);
            DirectProbe probe = new(Instance);
            foreach (string schema in PostgreSqlSchemaPrivilegeCases.ApplicationSchemas)
                await PostgreSqlSchemaPrivilegeCases.VerifyAsync(fixture, schema, (login, compatible) =>
                    CheckAsync(fixture, probe, login == PostgreSqlTestLogin.Web ? web : worker, fixture.Name(login),
                        compatible ? ["PASS"] : ["SF-DB-STARTUP-008"], component: login.ToString()));
        }
        finally
        {
            DeleteSandbox(sandbox, "sitefactory-schema-probe-");
        }
    }

    private static async Task VerifyNonTableMetadataAsync(PostgreSqlRoleFixture fixture, DirectProbe probe, string web)
    {
        await AdminAsync(fixture, "ALTER TABLE sitefactory_runtime.database_identity RENAME TO sf_test_saved_identity");
        string[] physicalRows = await SavedMetadataAsync();
        await AdminAsync(fixture, $"""
            CREATE VIEW sitefactory_runtime.database_identity AS SELECT * FROM sitefactory_runtime.sf_test_saved_identity;
            GRANT SELECT ON sitefactory_runtime.database_identity TO "{fixture.Baseline.Web}";
            """);
        Assert.Equal(DatabaseReadOutcome.InvalidDocument, (await ReadObservationAsync(web, Token)).Outcome);
        await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-SCHEMA-STARTUP-009"], readableMetadata: false);
        Assert.Equal(physicalRows, await SavedMetadataAsync());
        await AdminAsync(fixture, $"""
            DROP VIEW sitefactory_runtime.database_identity;
            CREATE FOREIGN DATA WRAPPER sf_test_no_handler;
            CREATE SERVER sf_test_no_handler FOREIGN DATA WRAPPER sf_test_no_handler;
            CREATE FOREIGN TABLE sitefactory_runtime.database_identity(database_instance_id uuid, client_slug text, environment text)
                SERVER sf_test_no_handler;
            GRANT SELECT ON sitefactory_runtime.database_identity TO "{fixture.Baseline.Web}";
            """);
        // This foreign table has no handler: trying to materialize it would fail with a
        // provider exception. The reader must reject its catalog type without invoking it.
        Assert.Equal(DatabaseReadOutcome.InvalidDocument, (await ReadObservationAsync(web, Token)).Outcome);
        await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["SF-SCHEMA-STARTUP-009"], readableMetadata: false);
        Assert.Equal(physicalRows, await SavedMetadataAsync());
        await AdminAsync(fixture, """
            DROP FOREIGN TABLE sitefactory_runtime.database_identity;
            DROP SERVER sf_test_no_handler;
            DROP FOREIGN DATA WRAPPER sf_test_no_handler;
            ALTER TABLE sitefactory_runtime.sf_test_saved_identity RENAME TO database_identity;
            """);
        await CheckAsync(fixture, probe, web, fixture.Baseline.Web, ["PASS"]);

        async Task<string[]> SavedMetadataAsync()
        {
            List<string> rows = [];
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                // Inspect the real underlying table, never execute a substituted view/foreign table.
                await using NpgsqlCommand command = new("""
                    SELECT 'identity|' || xmin::text || '|' || row_to_json(t)::text FROM sitefactory_runtime.sf_test_saved_identity t
                    UNION ALL SELECT 'schema|' || xmin::text || '|' || row_to_json(t)::text FROM sitefactory_runtime.schema_state t
                    UNION ALL SELECT 'migration|' || xmin::text || '|' || row_to_json(t)::text FROM sitefactory_runtime."__EFMigrationsHistory" t
                    ORDER BY 1
                    """, connection);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) rows.Add(reader.GetString(0));
            }, Token);
            return rows.ToArray();
        }
    }

    private static async Task VerifyTransitiveMembershipAsync(PostgreSqlRoleFixture fixture, DirectProbe probe, string web, string worker)
    {
        string intermediate = fixture.Baseline.Migrator[..^"migrator".Length] + "intermediate";
        string privileged = fixture.Baseline.Migrator[..^"migrator".Length] + "privileged";
        await AdminAsync(fixture, $"""
            CREATE ROLE "{intermediate}" NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
            CREATE ROLE "{privileged}" NOLOGIN NOINHERIT NOSUPERUSER CREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
            GRANT "{privileged}" TO "{intermediate}" WITH SET TRUE, INHERIT FALSE, ADMIN FALSE;
            """);
        try
        {
            foreach ((string file, string login, string component) in new[]
            {
                (web, fixture.Baseline.Web, "Web"), (worker, fixture.Baseline.Worker, "Worker"),
            })
            {
                await AdminAsync(fixture, $"GRANT \"{intermediate}\" TO \"{login}\" WITH SET TRUE, INHERIT FALSE, ADMIN FALSE");
                await fixture.ExecuteAsync(component == "Web" ? PostgreSqlTestLogin.Web : PostgreSqlTestLogin.Worker,
                    async (connection, token) =>
                    {
                        await using NpgsqlCommand witness = new($"""
                            SET ROLE "{privileged}";
                            SELECT rolcreatedb FROM pg_catalog.pg_roles WHERE rolname = current_user;
                            """, connection);
                        Assert.True((bool)(await witness.ExecuteScalarAsync(token))!);
                        await using NpgsqlCommand reset = new("RESET ROLE", connection);
                        await reset.ExecuteNonQueryAsync(token);
                    }, Token);
                RuntimeDatabaseObservation observation = await ReadObservationAsync(file, Token, component);
                Assert.True(observation.Role!.Privileges.HasFlag(DatabaseRolePrivileges.Membership));
                Assert.False(observation.Role.Privileges.HasFlag(DatabaseRolePrivileges.CreateDatabase));
                await CheckAsync(fixture, probe, file, login, ["SF-DB-STARTUP-008"], component: component);
                await AdminAsync(fixture, $"REVOKE \"{intermediate}\" FROM \"{login}\"");
            }
        }
        finally
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                await using NpgsqlCommand command = new($"DROP ROLE \"{intermediate}\", \"{privileged}\"", connection);
                await command.ExecuteNonQueryAsync(token);
            }, cleanup.Token);
        }
    }

    internal static async Task<int> SetupAsync(string file, bool bootstrap)
    {
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        string[] arguments = bootstrap
            ? ["bootstrap", "--environment", "Test", "--client-slug", "example-client", "--database-instance-id", Instance.ToString(),
                "--operation-id", "22222222-2222-4222-8222-222222222222", "--confirm-bootstrap"]
            : ["apply", "--environment", "Test", "--confirm-migration"];
        int exit = await MigrationCommand.RunAsync(arguments, file, output, error, Token);
        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        return exit;
    }

    internal static async Task<RuntimeDatabaseObservation> ReadObservationAsync(string file, CancellationToken token, string component = "Web")
    {
        PostgreSqlRuntimeConfiguration configuration = await PostgreSqlConfigurationReader.ReadAsync(
            Enum.Parse<PostgreSqlComponent>(component), file, "Test", token);
        return await new PostgreSqlDatabaseReader(configuration).ReadAsync(token);
    }

    internal static Task AssertNoProbeSessionsAsync(PostgreSqlRoleFixture fixture) => fixture.Database.ExecuteAsync(async (connection, token) =>
    {
        await using NpgsqlCommand command = new("""
            SELECT pg_catalog.count(*) FROM pg_catalog.pg_stat_activity
            WHERE datname = $1 AND application_name = 'sitefactory-compatibility-probe'
            """, connection);
        command.Parameters.AddWithValue(fixture.Database.Name);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(token))!);
    }, Token);

    internal static void DeleteSandbox(string sandbox, string prefix)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(root, Path.GetFullPath(sandbox), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        Assert.StartsWith(prefix, Path.GetFileName(sandbox), StringComparison.Ordinal);
        Assert.False((File.GetAttributes(sandbox) & FileAttributes.ReparsePoint) != 0);
        Directory.Delete(sandbox, recursive: true);
        Assert.False(Directory.Exists(sandbox));
    }

    private sealed class DirectProbe(Guid instanceId)
    {
        internal Task<string[]> RunAsync(string file, string login, CancellationToken token, string client = "example-client",
            string environment = "Test", string component = "Web", Guid? instance = null) =>
            DatabaseCompatibilityIntegrationBridge.ProbeRealAsync(file, client, environment, login, component, instance ?? instanceId, token);
    }
}
