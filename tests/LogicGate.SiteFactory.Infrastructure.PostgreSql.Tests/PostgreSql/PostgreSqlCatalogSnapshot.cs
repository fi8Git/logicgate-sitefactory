using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

/// <summary>Logical DDL snapshot; deliberately excludes vacuum/statistics and other operational counters.</summary>
internal static class PostgreSqlCatalogSnapshot
{
    private const string Query = """
        WITH schemas AS (
            SELECT oid FROM pg_catalog.pg_namespace
            WHERE nspname <> 'information_schema' AND NOT pg_catalog.starts_with(nspname::text, 'pg_')
        )
        SELECT 'schema|' || jsonb_build_array(nspname, nspowner, nspacl)::text
        FROM pg_catalog.pg_namespace WHERE oid IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'relation|' || jsonb_build_array(relnamespace, relname, relkind, relowner, relacl,
            relrowsecurity, relforcerowsecurity, reloptions)::text
        FROM pg_catalog.pg_class WHERE relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'column|' || jsonb_build_array(a.attrelid, a.attnum, a.attname, a.atttypid,
            a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attisdropped)::text
        FROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
        WHERE c.relnamespace IN (SELECT oid FROM schemas) AND a.attnum > 0
        UNION ALL
        SELECT 'constraint|' || jsonb_build_array(conrelid, conname, contype, convalidated,
            pg_catalog.pg_get_constraintdef(oid))::text
        FROM pg_catalog.pg_constraint WHERE connamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'default|' || jsonb_build_array(d.adrelid, d.adnum, pg_catalog.pg_get_expr(d.adbin, d.adrelid))::text
        FROM pg_catalog.pg_attrdef d JOIN pg_catalog.pg_class c ON c.oid = d.adrelid
        WHERE c.relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'index|' || jsonb_build_array(i.indexrelid, pg_catalog.pg_get_indexdef(i.indexrelid))::text
        FROM pg_catalog.pg_index i JOIN pg_catalog.pg_class c ON c.oid = i.indrelid
        WHERE c.relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'function|' || jsonb_build_array(pronamespace, proname, proowner, prokind, proacl,
            proargtypes::text, prorettype, prosrc, probin, prosecdef, proconfig)::text
        FROM pg_catalog.pg_proc WHERE pronamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'type|' || jsonb_build_array(typnamespace, typname, typowner, typtype, typacl, typbasetype)::text
        FROM pg_catalog.pg_type WHERE typnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'trigger|' || jsonb_build_array(t.tgrelid, t.tgname, t.tgenabled, pg_catalog.pg_get_triggerdef(t.oid))::text
        FROM pg_catalog.pg_trigger t JOIN pg_catalog.pg_class c ON c.oid = t.tgrelid
        WHERE c.relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'rule|' || jsonb_build_array(r.ev_class, r.rulename, pg_catalog.pg_get_ruledef(r.oid))::text
        FROM pg_catalog.pg_rewrite r JOIN pg_catalog.pg_class c ON c.oid = r.ev_class
        WHERE c.relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'policy|' || jsonb_build_array(p.polrelid, p.polname, p.polcmd, p.polroles,
            p.polpermissive, pg_catalog.pg_get_expr(p.polqual, p.polrelid),
            pg_catalog.pg_get_expr(p.polwithcheck, p.polrelid))::text
        FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid = p.polrelid
        WHERE c.relnamespace IN (SELECT oid FROM schemas)
        UNION ALL
        SELECT 'namespace-object|' || jsonb_build_array(d.classid, d.objid, d.objsubid,
            pg_catalog.pg_describe_object(d.classid, d.objid, d.objsubid))::text
        FROM pg_catalog.pg_depend d
        WHERE d.refclassid = 'pg_catalog.pg_namespace'::regclass AND d.refobjid IN (SELECT oid FROM schemas)
        """;

    internal static async Task<string[]> ReadAsync(PostgreSqlRoleFixture fixture, bool migrated, CancellationToken token)
    {
        List<string> result = [];
        await fixture.Database.ExecuteAsync(async (connection, cancellationToken) =>
        {
            await using NpgsqlCommand command = new(Query, connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        }, token);
        if (migrated) result.AddRange(await PostgreSqlBootstrapTests.SnapshotMetadataAsync(fixture));
        return result.Order(StringComparer.Ordinal).ToArray();
    }
}
