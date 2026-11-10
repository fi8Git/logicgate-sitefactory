using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P01")]
public sealed class PostgreSqlRoleTests
{
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    private const string Requirement = "Requires the dedicated ephemeral PostgreSQL 18 P01 proof.";
    private static readonly string[] DatabaseWritePrivileges = ["CREATE", "TEMP"];
    private static readonly string[] SchemaNames = ["public", "sitefactory_runtime"];
    private static readonly string[] FutureTableNames = ["future_table", "future_view"];
    private static readonly string[] TableWritePrivileges = ["INSERT", "UPDATE", "DELETE", "TRUNCATE", "REFERENCES", "TRIGGER", "MAINTAIN"];
    private static readonly string[] SequencePrivileges = ["SELECT", "USAGE", "UPDATE"];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task<PostgreSqlTestConfiguration> ConfigurationAsync() => PostgreSqlTestGate.ReadAsync(Token);

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task RoleAttributesAndMembershipAreLeastPrivilege()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(await ConfigurationAsync(), Token);
        PostgreSqlRoleBaseline roles = fixture.Baseline;
        Assert.Equal(4, roles.Names.Distinct(StringComparer.Ordinal).Count());
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            foreach (string name in roles.Names)
            {
                await using NpgsqlCommand query = new("""
                    SELECT rolcanlogin, rolsuper, rolcreatedb, rolcreaterole, rolreplication,
                           rolbypassrls, rolinherit, rolconnlimit, rolpassword IS NULL
                    FROM pg_authid WHERE rolname = $1
                    """, connection);
                query.Parameters.AddWithValue(name);
                await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(token);
                Assert.True(await reader.ReadAsync(token));
                Assert.Equal(name != roles.Owner, reader.GetBoolean(0));
                for (int column = 1; column <= 6; column++)
                    Assert.False(reader.GetBoolean(column));
                Assert.Equal(name == roles.Owner ? 0 : name == roles.Migrator ? 2 : 4, reader.GetInt32(7));
                Assert.Equal(name == roles.Owner, reader.GetBoolean(8));
                Assert.False(await reader.ReadAsync(token));
            }
            await using NpgsqlCommand memberships = new("""
                SELECT parent.rolname, child.rolname, m.admin_option, m.inherit_option, m.set_option
                FROM pg_auth_members m
                JOIN pg_roles parent ON parent.oid = m.roleid
                JOIN pg_roles child ON child.oid = m.member
                WHERE parent.rolname = ANY($1) OR child.rolname = ANY($1)
                """, connection);
            memberships.Parameters.AddWithValue(roles.Names);
            await using NpgsqlDataReader membership = await memberships.ExecuteReaderAsync(token);
            Assert.True(await membership.ReadAsync(token));
            Assert.Equal(roles.Owner, membership.GetString(0));
            Assert.Equal(roles.Migrator, membership.GetString(1));
            Assert.False(membership.GetBoolean(2));
            Assert.False(membership.GetBoolean(3));
            Assert.True(membership.GetBoolean(4));
            Assert.False(await membership.ReadAsync(token));
        }, Token);

        foreach (PostgreSqlTestLogin login in Enum.GetValues<PostgreSqlTestLogin>())
        {
            await fixture.ExecuteAsync(login, async (connection, token) =>
            {
                Assert.Equal(fixture.Name(login), await ScalarAsync(connection, "SELECT session_user::text", token));
                Assert.Equal(fixture.Name(login), await ScalarAsync(connection, "SELECT current_user::text", token));
                Assert.Equal("pg_catalog, sitefactory_runtime", await ScalarAsync(connection, "SHOW search_path", token));
                Assert.Equal("10s", await ScalarAsync(connection, "SHOW statement_timeout", token));
                Assert.Equal("3s", await ScalarAsync(connection, "SHOW lock_timeout", token));
                Assert.Equal("10s", await ScalarAsync(connection, "SHOW idle_in_transaction_session_timeout", token));
                // Query the public catalog as the actual login too, not only the administrative view.
                Assert.Equal(false, await ScalarAsync(connection, """
                    SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls
                    FROM pg_roles WHERE rolname = current_user
                    """, token));
            }, Token);
        }
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task MigratorRequiresExplicitOwnerAndResetsItsIdentity()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(await ConfigurationAsync(), Token);
        await fixture.ExecuteAsync(PostgreSqlTestLogin.Migrator, async (connection, token) =>
        {
            const string create = "CREATE TABLE sitefactory_runtime.ddl_probe (id integer PRIMARY KEY)";
            await DeniedAsync(connection, create, token);
            await ExecuteAsync(connection, $"SET ROLE \"{fixture.Baseline.Owner}\"", token);
            Assert.Equal(fixture.Baseline.Migrator, await ScalarAsync(connection, "SELECT session_user::text", token));
            Assert.Equal(fixture.Baseline.Owner, await ScalarAsync(connection, "SELECT current_user::text", token));
            await ExecuteAsync(connection, create, token);
            await ExecuteAsync(connection, "ALTER TABLE sitefactory_runtime.ddl_probe ADD COLUMN value text", token);
            Assert.Equal(fixture.Baseline.Owner, await ScalarAsync(connection,
                "SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid = 'sitefactory_runtime.ddl_probe'::regclass", token));
            await ExecuteAsync(connection, "DROP TABLE sitefactory_runtime.ddl_probe; RESET ROLE", token);
            Assert.Equal(fixture.Baseline.Migrator, await ScalarAsync(connection, "SELECT current_user::text", token));
            await DeniedAsync(connection, create, token);
        }, Token);
        await fixture.ExecuteAsync(PostgreSqlTestLogin.Migrator, async (connection, token) =>
        {
            Assert.Equal(fixture.Baseline.Migrator, await ScalarAsync(connection, "SELECT current_user::text", token));
            await DeniedAsync(connection, $"GRANT \"{fixture.Baseline.Owner}\" TO \"{fixture.Baseline.Web}\"", token);
        }, Token);
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task WebCanReadButCannotChangeRuntimeSchema()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        await AssertRuntimePrivilegesAsync(PostgreSqlTestLogin.Web);
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task WorkerCanReadButCannotChangeRuntimeSchema()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        await AssertRuntimePrivilegesAsync(PostgreSqlTestLogin.Worker);
    }

    private static async Task AssertRuntimePrivilegesAsync(PostgreSqlTestLogin login)
    {
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(await ConfigurationAsync(), Token);
        await CreateRuntimeProbesAsync(fixture);
        string[] before = await SnapshotRuntimeAsync(fixture);
        await fixture.ExecuteAsync(login, async (connection, token) =>
        {
            Assert.Equal(1, await ScalarAsync(connection, "SELECT probe FROM sitefactory_runtime.database_identity", token));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT probe FROM sitefactory_runtime.schema_state", token));
            string[] forbidden =
            [
                "CREATE TABLE sitefactory_runtime.forbidden (id integer)",
                "DROP TABLE sitefactory_runtime.database_identity",
                "ALTER TABLE sitefactory_runtime.database_identity ADD COLUMN forbidden integer",
                "INSERT INTO sitefactory_runtime.database_identity VALUES (2)",
                "UPDATE sitefactory_runtime.database_identity SET probe = 2",
                "DELETE FROM sitefactory_runtime.database_identity",
                "TRUNCATE sitefactory_runtime.database_identity",
                "CREATE TABLE public.forbidden (id integer)",
                "CREATE SCHEMA forbidden",
                "CREATE TEMP TABLE forbidden (id integer)",
                "DROP SCHEMA sitefactory_runtime CASCADE",
                $"SET ROLE \"{fixture.Baseline.Owner}\"",
                $"SET ROLE \"{fixture.Baseline.Migrator}\"",
                $"SET ROLE \"{(login == PostgreSqlTestLogin.Web ? fixture.Baseline.Worker : fixture.Baseline.Web)}\"",
                $"GRANT \"{fixture.Baseline.Owner}\" TO \"{fixture.Name(login)}\"",
                $"ALTER DEFAULT PRIVILEGES FOR ROLE \"{fixture.Baseline.Owner}\" IN SCHEMA sitefactory_runtime GRANT INSERT ON TABLES TO \"{fixture.Name(login)}\"",
            ];
            foreach (string sql in forbidden)
                await DeniedAsync(connection, sql, token);
            Assert.Equal(1, await ScalarAsync(connection, "SELECT probe FROM sitefactory_runtime.database_identity", token));
            Assert.Equal(1L, await ScalarAsync(connection, """
                SELECT count(*) FROM information_schema.columns
                WHERE table_schema = 'sitefactory_runtime' AND table_name = 'database_identity'
                """, token));
        }, Token);
        Assert.Equal(before, await SnapshotRuntimeAsync(fixture));
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task PublicPrivilegesAndDatabaseIsolationAreExplicit()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await ConfigurationAsync();
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
        await using PostgreSqlRoleFixture other = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            Assert.Equal(0L, await ScalarAsync(connection, """
                SELECT count(*) FROM pg_database d,
                LATERAL aclexplode(coalesce(d.datacl, acldefault('d', d.datdba))) a
                WHERE d.datname = current_database() AND a.grantee = 0
                """, token));
            Assert.Equal(0L, await ScalarAsync(connection, """
                SELECT count(*) FROM pg_namespace n,
                LATERAL aclexplode(coalesce(n.nspacl, acldefault('n', n.nspowner))) a
                WHERE n.nspname IN ('public', 'sitefactory_runtime') AND a.grantee = 0
                """, token));
            Assert.Equal(fixture.Baseline.Owner, await ScalarAsync(connection,
                "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = current_database()", token));
            Assert.Equal(2L, await ScalarAsync(connection, $"""
                SELECT count(*) FROM pg_namespace WHERE nspname IN ('public', 'sitefactory_runtime')
                AND pg_get_userbyid(nspowner) = '{fixture.Baseline.Owner}'
                """, token));
        }, Token);
        foreach (PostgreSqlTestLogin login in Enum.GetValues<PostgreSqlTestLogin>())
        {
            await fixture.ExecuteAsync(login, async (connection, token) =>
            {
                Assert.Equal(true, await ScalarAsync(connection, "SELECT has_database_privilege(current_database(), 'CONNECT')", token));
                foreach (string privilege in DatabaseWritePrivileges)
                    Assert.Equal(false, await ScalarAsync(connection, $"SELECT has_database_privilege(current_database(), '{privilege}')", token));
                foreach (string schema in SchemaNames)
                {
                    Assert.Equal(false, await ScalarAsync(connection, $"SELECT has_schema_privilege('{schema}', 'CREATE')", token));
                    Assert.Equal(schema == "sitefactory_runtime" && login != PostgreSqlTestLogin.Migrator,
                        await ScalarAsync(connection, $"SELECT has_schema_privilege('{schema}', 'USAGE')", token));
                }
                foreach (string database in new[] { "postgres", "template0", "template1", PostgreSqlTestConfiguration.AdministrativeDatabase, other.Database.Name })
                    Assert.Equal(false, await ScalarAsync(connection, $"SELECT has_database_privilege('{database}', 'CONNECT')", token));
            }, Token);
            foreach (string database in new[] { "postgres", "template0", "template1", PostgreSqlTestConfiguration.AdministrativeDatabase, other.Database.Name })
                await AssertConnectionDeniedAsync(fixture, login, database);
            await AssertConnectionDeniedAsync(other, login, fixture.Database.Name);
        }
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task FutureOwnerObjectsReceiveOnlyExpectedDefaultPrivileges()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(await ConfigurationAsync(), Token);
        await AsOwnerAsync(fixture, """
            CREATE TABLE sitefactory_runtime.future_table (id integer PRIMARY KEY);
            INSERT INTO sitefactory_runtime.future_table VALUES (1);
            CREATE VIEW sitefactory_runtime.future_view AS SELECT id FROM sitefactory_runtime.future_table;
            CREATE SEQUENCE sitefactory_runtime.future_sequence;
            CREATE FUNCTION sitefactory_runtime.future_function() RETURNS integer LANGUAGE sql AS 'SELECT 1';
            CREATE TYPE sitefactory_runtime.future_type AS ENUM ('probe');
            CREATE SCHEMA future_schema;
            CREATE TABLE future_schema.private_table (id integer);
            CREATE SCHEMA type_usage_probe;
            """);
        foreach (PostgreSqlTestLogin login in new[] { PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker })
        {
            await fixture.ExecuteAsync(login, async (connection, token) =>
            {
                foreach (string table in FutureTableNames)
                {
                    Assert.Equal(1, await ScalarAsync(connection, $"SELECT id FROM sitefactory_runtime.{table}", token));
                    foreach (string privilege in TableWritePrivileges)
                        Assert.Equal(false, await ScalarAsync(connection, $"SELECT has_table_privilege('sitefactory_runtime.{table}', '{privilege}')", token));
                }
                foreach (string privilege in SequencePrivileges)
                    Assert.Equal(false, await ScalarAsync(connection, $"SELECT has_sequence_privilege('sitefactory_runtime.future_sequence', '{privilege}')", token));
                Assert.Equal(false, await ScalarAsync(connection, "SELECT has_function_privilege('sitefactory_runtime.future_function()', 'EXECUTE')", token));
                Assert.Equal(false, await ScalarAsync(connection, "SELECT has_type_privilege('sitefactory_runtime.future_type', 'USAGE')", token));
                Assert.Equal(false, await ScalarAsync(connection, "SELECT has_schema_privilege('future_schema', 'USAGE')", token));
                await DeniedAsync(connection, "SELECT nextval('sitefactory_runtime.future_sequence')", token);
                await DeniedAsync(connection, "SELECT sitefactory_runtime.future_function()", token);
                await DeniedAsync(connection, "SELECT * FROM future_schema.private_table", token);
                await DeniedAsync(connection, "INSERT INTO sitefactory_runtime.future_table VALUES (2)", token);
                foreach (string table in FutureTableNames)
                {
                    await DeniedAsync(connection, $"INSERT INTO sitefactory_runtime.{table} VALUES (2)", token);
                    await DeniedAsync(connection, $"UPDATE sitefactory_runtime.{table} SET id = 2", token);
                    await DeniedAsync(connection, $"DELETE FROM sitefactory_runtime.{table}", token);
                }
                // TRUNCATE applies to tables, not views (which fail for wrong object type).
                await DeniedAsync(connection, "TRUNCATE sitefactory_runtime.future_table", token);
                await DeniedAsync(connection, "CREATE TABLE future_schema.forbidden (id integer)", token);
            }, Token);
            // Type USAGE controls creation of dependent objects, not a simple enum cast.
            // Isolate that check from schema CREATE: a temporary, test-only schema grant
            // permits the integer control, while the enum-dependent table must still fail.
            await AsOwnerAsync(fixture, $"GRANT USAGE, CREATE ON SCHEMA type_usage_probe TO \"{fixture.Name(login)}\"");
            try
            {
                await fixture.ExecuteAsync(login, async (connection, token) =>
                {
                    await ExecuteAsync(connection, "CREATE TABLE type_usage_probe.control (value integer); DROP TABLE type_usage_probe.control", token);
                    await DeniedAsync(connection, "CREATE TABLE type_usage_probe.forbidden (value sitefactory_runtime.future_type)", token);
                }, Token);
            }
            finally
            {
                await AsOwnerAsync(fixture, $"REVOKE ALL ON SCHEMA type_usage_probe FROM \"{fixture.Name(login)}\"");
            }
        }
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            Assert.Equal(0L, await ScalarAsync(connection, """
                SELECT count(*) FROM (
                    SELECT relacl AS acl, relowner AS owner, 'r'::"char" AS kind FROM pg_class
                    WHERE relnamespace = 'sitefactory_runtime'::regnamespace AND relkind IN ('r', 'v')
                    UNION ALL
                    SELECT relacl, relowner, 's'::"char" FROM pg_class
                    WHERE relnamespace = 'sitefactory_runtime'::regnamespace AND relkind = 'S'
                    UNION ALL
                    SELECT proacl, proowner, 'f'::"char" FROM pg_proc WHERE pronamespace = 'sitefactory_runtime'::regnamespace
                    UNION ALL
                    SELECT typacl, typowner, 'T'::"char" FROM pg_type
                    WHERE typnamespace = 'sitefactory_runtime'::regnamespace AND typname = 'future_type'
                    UNION ALL
                    SELECT nspacl, nspowner, 'n'::"char" FROM pg_namespace WHERE nspname = 'future_schema'
                ) objects, LATERAL aclexplode(coalesce(objects.acl, acldefault(objects.kind, objects.owner))) a
                WHERE a.grantee = 0
                """, token));
        }, Token);
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task ControlledSearchPathIgnoresPublicHomonyms()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(await ConfigurationAsync(), Token);
        await CreateRuntimeProbesAsync(fixture);
        // Only the administrative test setup can inject these adversarial objects.
        await fixture.Database.ExecuteAsync((connection, token) => ExecuteAsync(connection, """
            CREATE TABLE public.database_identity (probe integer);
            INSERT INTO public.database_identity VALUES (999);
            CREATE FUNCTION public.length(text) RETURNS integer LANGUAGE sql AS 'SELECT 999';
            """, token), Token);
        foreach (PostgreSqlTestLogin login in new[] { PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker })
        {
            await fixture.ExecuteAsync(login, async (connection, token) =>
            {
                Assert.Equal("pg_catalog, sitefactory_runtime", await ScalarAsync(connection, "SHOW search_path", token));
                Assert.Equal(1, await ScalarAsync(connection, "SELECT probe FROM database_identity", token));
                Assert.Equal(3, await ScalarAsync(connection, "SELECT length('abc'::text)", token));
                await DeniedAsync(connection, "SELECT probe FROM public.database_identity", token);
                await DeniedAsync(connection, "CREATE TEMP TABLE database_identity (probe integer)", token);
            }, Token);
        }
    }

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = Requirement)]
    public async Task RoleCleanupSurvivesFailureAndCancellation()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await ConfigurationAsync();
        string? databaseName = null;
        string[] names = [];
        await Assert.ThrowsAsync<DeliberateTestException>(async () =>
        {
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            databaseName = fixture.Database.Name;
            names = fixture.Baseline.Names;
            await CreateRuntimeProbesAsync(fixture);
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ExecuteAsync(
                PostgreSqlTestLogin.Web, (_, _) => Task.CompletedTask, cancelled.Token));
            throw new DeliberateTestException();
        });
        Assert.NotNull(databaseName);
        Assert.Equal(4, names.Length);
        try
        {
            await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await admin.OpenAsync(Token);
            await using NpgsqlCommand query = new("SELECT count(*) FROM pg_roles WHERE rolname = ANY($1)", admin);
            query.Parameters.AddWithValue(names);
            Assert.Equal(0L, await query.ExecuteScalarAsync(Token));
            query.Parameters.Clear();
            query.CommandText = "SELECT count(*) FROM pg_database WHERE datname = $1";
            query.Parameters.AddWithValue(databaseName);
            Assert.Equal(0L, await query.ExecuteScalarAsync(Token));
            query.Parameters.Clear();
            query.CommandText = "SELECT pg_catalog.count(*) FROM pg_catalog.pg_stat_activity WHERE datname = $1 OR usename = ANY($2)";
            query.Parameters.AddWithValue(databaseName);
            query.Parameters.AddWithValue(names);
            Assert.Equal(0L, await query.ExecuteScalarAsync(Token));
        }
        catch (NpgsqlException)
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-012 Could not verify temporary role cleanup.");
        }
    }

    // ACL probes, not a replacement for the generated EF model or a proof of its migration.
    private static async Task<string[]> SnapshotRuntimeAsync(PostgreSqlRoleFixture fixture)
    {
        List<string> rows = [];
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            await using NpgsqlCommand command = new("""
                WITH schemas AS (
                    SELECT oid FROM pg_catalog.pg_namespace
                    WHERE nspname <> 'information_schema' AND NOT pg_catalog.starts_with(nspname::text, 'pg_')
                )
                SELECT 'schema|' || pg_catalog.jsonb_build_array(nspname, nspowner, nspacl)::text
                FROM pg_catalog.pg_namespace WHERE oid IN (SELECT oid FROM schemas)
                UNION ALL
                SELECT 'relation|' || pg_catalog.jsonb_build_array(relnamespace, relname, relkind, relowner, relacl)::text
                FROM pg_catalog.pg_class WHERE relnamespace IN (SELECT oid FROM schemas)
                UNION ALL
                SELECT 'column|' || pg_catalog.jsonb_build_array(a.attrelid, a.attnum, a.attname, a.atttypid,
                    a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attisdropped)::text
                FROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
                WHERE c.relnamespace IN (SELECT oid FROM schemas) AND a.attnum > 0
                UNION ALL
                SELECT 'constraint|' || pg_catalog.jsonb_build_array(conrelid, conname, contype, pg_catalog.pg_get_constraintdef(oid))::text
                FROM pg_catalog.pg_constraint WHERE connamespace IN (SELECT oid FROM schemas)
                UNION ALL
                SELECT 'membership|' || pg_catalog.jsonb_build_array(m.roleid, m.member, m.grantor,
                    m.admin_option, m.inherit_option, m.set_option)::text
                FROM pg_catalog.pg_auth_members m
                JOIN pg_catalog.pg_roles parent ON parent.oid = m.roleid
                JOIN pg_catalog.pg_roles child ON child.oid = m.member
                WHERE parent.rolname = ANY($1) OR child.rolname = ANY($1)
                UNION ALL
                SELECT 'default-acl|' || pg_catalog.jsonb_build_array(defaclrole, defaclnamespace, defaclobjtype, defaclacl)::text
                FROM pg_catalog.pg_default_acl WHERE defaclrole IN (SELECT oid FROM pg_catalog.pg_roles WHERE rolname = ANY($1))
                UNION ALL
                SELECT 'database|' || pg_catalog.jsonb_build_array(datname, datdba, datacl)::text
                FROM pg_catalog.pg_database WHERE datname = pg_catalog.current_database()
                UNION ALL
                SELECT 'identity-row|' || pg_catalog.to_jsonb(d)::text FROM sitefactory_runtime.database_identity d
                UNION ALL
                SELECT 'schema-row|' || pg_catalog.to_jsonb(s)::text FROM sitefactory_runtime.schema_state s
                """, connection);
            command.Parameters.AddWithValue(fixture.Baseline.Names);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(reader.GetString(0));
        }, Token);
        return rows.Order(StringComparer.Ordinal).ToArray();
    }

    private static async Task AssertConnectionDeniedAsync(PostgreSqlRoleFixture fixture, PostgreSqlTestLogin login, string database)
    {
        DirectoryInfo temporary = Directory.CreateTempSubdirectory("sitefactory-p01-connection-");
        try
        {
            string file = Path.Combine(temporary.FullName, "connection.txt");
            await fixture.WriteConnectionFileAsync(login, file, Token);
            NpgsqlConnectionStringBuilder settings = new(await File.ReadAllTextAsync(file, Token))
            {
                Database = database,
                Pooling = false,
                NoResetOnClose = false,
            };
            await using NpgsqlConnection connection = new(settings.ConnectionString);
            PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync(Token));
            // template0 additionally forbids all connections, independently of its CONNECT ACL.
            Assert.Equal(database == "template0" ? PostgresErrorCodes.ObjectNotInPrerequisiteState : PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
        finally { temporary.Delete(recursive: true); }
    }

    private static Task CreateRuntimeProbesAsync(PostgreSqlRoleFixture fixture) => AsOwnerAsync(fixture, """
        CREATE TABLE sitefactory_runtime.database_identity (probe integer NOT NULL);
        CREATE TABLE sitefactory_runtime.schema_state (probe integer NOT NULL);
        INSERT INTO sitefactory_runtime.database_identity VALUES (1);
        INSERT INTO sitefactory_runtime.schema_state VALUES (1);
        """);

    private static Task AsOwnerAsync(PostgreSqlRoleFixture fixture, string sql) => fixture.ExecuteAsync(
        PostgreSqlTestLogin.Migrator, async (connection, token) =>
        {
            await ExecuteAsync(connection, $"SET ROLE \"{fixture.Baseline.Owner}\"", token);
            try { await ExecuteAsync(connection, sql, token); }
            finally { await ExecuteAsync(connection, "RESET ROLE", token); }
        }, Token);

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

    private static async Task DeniedAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        // Catch here, before the fixture sanitizes unexpected provider failures. A syntax error is not proof.
        await using NpgsqlCommand command = new(sql, connection);
        PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(token));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    private sealed class DeliberateTestException : Exception;
}
