using LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql;

// Observations only, on the isolated database and the four roles owned by this test.
// No provider security policy, password, statistics or operational counters are copied here.
internal static class PostgreSqlHostSnapshot
{
    private const string Catalog = """
        WITH schemas AS (
            SELECT oid FROM pg_catalog.pg_namespace
            WHERE nspname <> 'information_schema' AND NOT pg_catalog.starts_with(nspname::text, 'pg_')
        )
        SELECT 'schema|' || pg_catalog.jsonb_build_array(nspname, nspowner, nspacl)::text
        FROM pg_catalog.pg_namespace WHERE oid IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'relation|' || pg_catalog.jsonb_build_array(relnamespace, relname, relkind, relowner, relacl,
            relrowsecurity, relforcerowsecurity, reloptions)::text
        FROM pg_catalog.pg_class WHERE relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'column|' || pg_catalog.jsonb_build_array(a.attrelid, a.attnum, a.attname, a.atttypid,
            a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attisdropped)::text
        FROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
        WHERE c.relnamespace IN (SELECT oid FROM schemas) AND a.attnum > 0
        UNION ALL
        SELECT 'constraint|' || pg_catalog.jsonb_build_array(conrelid, conname, contype, convalidated,
            pg_catalog.pg_get_constraintdef(oid))::text
        FROM pg_catalog.pg_constraint WHERE connamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'database|' || pg_catalog.jsonb_build_array(datname, datdba, datacl)::text
        FROM pg_catalog.pg_database WHERE datname = pg_catalog.current_database()
        UNION ALL
        SELECT 'role|' || pg_catalog.jsonb_build_array(oid, rolname, rolcanlogin, rolinherit, rolconnlimit,
            rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls, rolconfig)::text
        FROM pg_catalog.pg_roles WHERE rolname = ANY($1)
        UNION ALL
        SELECT 'membership|' || pg_catalog.jsonb_build_array(m.roleid, m.member, m.grantor,
            m.admin_option, m.inherit_option, m.set_option)::text
        FROM pg_catalog.pg_auth_members m JOIN pg_catalog.pg_roles r ON r.oid = m.member
        WHERE r.rolname = ANY($1)
        UNION ALL
        SELECT 'default-acl|' || pg_catalog.jsonb_build_array(defaclrole, defaclnamespace,
            defaclobjtype, defaclacl)::text
        FROM pg_catalog.pg_default_acl WHERE defaclrole IN
            (SELECT oid FROM pg_catalog.pg_roles WHERE rolname = ANY($1))
        UNION ALL
        SELECT 'role-settings|' || pg_catalog.jsonb_build_array(setdatabase, setrole, setconfig)::text
        FROM pg_catalog.pg_db_role_setting WHERE setrole IN
            (SELECT oid FROM pg_catalog.pg_roles WHERE rolname = ANY($1))
        """;

    internal static readonly string[] MetadataTables = ["database_identity", "schema_state", "\"__EFMigrationsHistory\""];

    internal static async Task<string[]> ReadAsync(PostgreSqlRoleFixture fixture, CancellationToken token)
    {
        List<string> rows = [];
        await fixture.Database.ExecuteAsync(async (connection, cancellationToken) =>
        {
            await using (NpgsqlCommand command = new(Catalog, connection))
            {
                command.Parameters.AddWithValue(fixture.Baseline.Names);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) rows.Add(reader.GetString(0));
            }
            foreach (string table in MetadataTables)
            {
                // Fixed test table names only. xmin also detects an update to identical values.
                await using NpgsqlCommand command = new("SELECT xmin::text || '|' || pg_catalog.row_to_json(t)::text FROM sitefactory_runtime." + table + " t", connection);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) rows.Add(table + "|" + reader.GetString(0));
            }
        }, token);
        return rows.Order(StringComparer.Ordinal).ToArray();
    }

    internal static void AssertUnchanged(string[] before, string[] after) =>
        Assert.True(before.SequenceEqual(after, StringComparer.Ordinal), "Owned PostgreSQL structure, ACLs, roles or runtime metadata changed during the host/probe phase.");
}
