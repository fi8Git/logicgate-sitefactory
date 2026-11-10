using System.Data;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Runtime.Database;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

/// <summary>Fixed, schema-qualified reads in a bounded, non-pooled, read-only snapshot. No EF migration APIs.</summary>
public sealed class PostgreSqlDatabaseReader(PostgreSqlRuntimeConfiguration configuration) : IRuntimeDatabaseReader
{
    public static TimeSpan MaximumDuration { get; } = TimeSpan.FromSeconds(5);

    public async Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(MaximumDuration);
        DatabaseRoleObservation? role = null;
        try
        {
            await using NpgsqlConnection connection = configuration.CreateProbeConnection();
            await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token).ConfigureAwait(false);
            await using (NpgsqlCommand readOnly = new("SET TRANSACTION READ ONLY", connection, transaction))
                await readOnly.ExecuteNonQueryAsync(deadline.Token).ConfigureAwait(false);

            role = await ReadRoleAsync(connection, transaction, deadline.Token).ConfigureAwait(false);
            List<DatabaseIdentityObservation> identities = [];
            List<DatabaseSchemaObservation> schemas = [];
            List<string> migrations = [];
            string[] tables = await ReadTableNamesAsync(connection, transaction, deadline.Token).ConfigureAwait(false);
            if (tables.Contains("database_identity", StringComparer.Ordinal))
            {
                await using NpgsqlCommand command = new("""
                    SELECT database_instance_id, client_slug, environment
                    FROM sitefactory_runtime.database_identity ORDER BY database_instance_id LIMIT 2
                    """, connection, transaction);
                await using NpgsqlDataReader rows = await command.ExecuteReaderAsync(deadline.Token).ConfigureAwait(false);
                while (await rows.ReadAsync(deadline.Token).ConfigureAwait(false))
                {
                    if (rows.IsDBNull(0) || rows.IsDBNull(1) || rows.IsDBNull(2)) return new(DatabaseReadOutcome.InvalidDocument, role);
                    identities.Add(new(rows.GetGuid(0), rows.GetString(1), rows.GetString(2)));
                }
            }
            if (tables.Contains("schema_state", StringComparer.Ordinal))
            {
                await using NpgsqlCommand command = new("""
                    SELECT schema_family, current_version, last_migration_id, migration_state
                    FROM sitefactory_runtime.schema_state ORDER BY schema_family COLLATE pg_catalog."C" LIMIT 2
                    """, connection, transaction);
                await using NpgsqlDataReader rows = await command.ExecuteReaderAsync(deadline.Token).ConfigureAwait(false);
                while (await rows.ReadAsync(deadline.Token).ConfigureAwait(false))
                {
                    if (rows.IsDBNull(0) || rows.IsDBNull(1) || rows.IsDBNull(3)) return new(DatabaseReadOutcome.InvalidDocument, role);
                    schemas.Add(new(rows.GetString(0), rows.GetString(1), rows.IsDBNull(2) ? null : rows.GetString(2), rows.GetString(3)));
                }
            }
            if (tables.Contains("__EFMigrationsHistory", StringComparer.Ordinal))
            {
                await using NpgsqlCommand command = new("""
                    SELECT "MigrationId" FROM sitefactory_runtime."__EFMigrationsHistory"
                    ORDER BY ("MigrationId" OPERATOR(pg_catalog.=) $1) DESC, "MigrationId" COLLATE pg_catalog."C" LIMIT 2
                    """, connection, transaction);
                // Keep the expected migration visible even if several unknown IDs sort before it.
                command.Parameters.AddWithValue(DatabaseCompatibilityProbe.InitialMigration);
                await using NpgsqlDataReader rows = await command.ExecuteReaderAsync(deadline.Token).ConfigureAwait(false);
                while (await rows.ReadAsync(deadline.Token).ConfigureAwait(false))
                {
                    if (rows.IsDBNull(0)) return new(DatabaseReadOutcome.InvalidDocument, role);
                    migrations.Add(rows.GetString(0));
                }
            }
            deadline.Token.ThrowIfCancellationRequested();
            return new(DatabaseReadOutcome.Read, role, identities, schemas, migrations);
            // Read-only transaction rolls back on disposal; the physical session always closes.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return new(DatabaseReadOutcome.Unavailable);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            return new(DatabaseReadOutcome.PermissionDenied, role);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable
            or PostgresErrorCodes.UndefinedColumn or PostgresErrorCodes.DatatypeMismatch)
        {
            return new(DatabaseReadOutcome.InvalidDocument, role);
        }
        catch (InvalidCastException)
        {
            // Materialization boundary only: a column no longer has the declared CLR-compatible SQL type.
            return new(DatabaseReadOutcome.InvalidDocument, role);
        }
        catch (NpgsqlException exception) when (exception is not PostgresException || exception.SqlState is
            PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.InvalidPassword
            or PostgresErrorCodes.InvalidAuthorizationSpecification or PostgresErrorCodes.InvalidCatalogName
            or PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.TooManyConnections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(DatabaseReadOutcome.Unavailable);
        }
        // No catch-all: unexpected SQL/programming errors are not relabeled as user configuration failures.
    }

    private static async Task<string[]> ReadTableNamesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        List<string> tables = [];
        await using NpgsqlCommand command = new("""
            SELECT c.relname::pg_catalog.text, c.relkind::pg_catalog.text FROM pg_catalog.pg_class c
            JOIN pg_catalog.pg_namespace n ON n.oid OPERATOR(pg_catalog.=) c.relnamespace
            WHERE n.nspname OPERATOR(pg_catalog.=) 'sitefactory_runtime' AND (c.relname OPERATOR(pg_catalog.=) 'database_identity'
                OR c.relname OPERATOR(pg_catalog.=) 'schema_state' OR c.relname OPERATOR(pg_catalog.=) '__EFMigrationsHistory')
            ORDER BY c.relname COLLATE pg_catalog."C"
            """, connection, transaction);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            // Never invoke a view/foreign table standing in for the expected local metadata tables.
            if (reader.GetString(1) != "r") throw new InvalidCastException("Unexpected metadata relation type.");
            tables.Add(reader.GetString(0));
        }
        return tables.ToArray();
    }

    private static async Task<DatabaseRoleObservation> ReadRoleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        await using NpgsqlCommand command = new("""
            SELECT session_user::pg_catalog.text, current_user::pg_catalog.text, r.rolcanlogin, r.rolinherit,
                pg_catalog.current_setting('search_path'), pg_catalog.current_setting('transaction_read_only'),
                r.rolsuper, r.rolcreatedb, r.rolcreaterole, r.rolreplication, r.rolbypassrls,
                EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members WHERE member OPERATOR(pg_catalog.=) r.oid),
                EXISTS (SELECT 1 FROM pg_catalog.pg_database WHERE datname OPERATOR(pg_catalog.=) pg_catalog.current_database() AND datdba OPERATOR(pg_catalog.=) r.oid)
                    OR EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspowner OPERATOR(pg_catalog.=) r.oid)
                    OR EXISTS (SELECT 1 FROM pg_catalog.pg_class WHERE relowner OPERATOR(pg_catalog.=) r.oid)
                    OR EXISTS (SELECT 1 FROM pg_catalog.pg_proc WHERE proowner OPERATOR(pg_catalog.=) r.oid),
                pg_catalog.has_database_privilege(r.oid, pg_catalog.current_database(), 'CREATE'),
                pg_catalog.has_database_privilege(r.oid, pg_catalog.current_database(), 'TEMP'),
                EXISTS (SELECT 1 FROM pg_catalog.pg_namespace n WHERE NOT pg_catalog.starts_with(n.nspname::pg_catalog.text, 'pg_')
                    AND n.nspname OPERATOR(pg_catalog.<>) 'information_schema' AND pg_catalog.has_schema_privilege(r.oid, n.oid, 'CREATE')),
                EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid OPERATOR(pg_catalog.=) c.relnamespace
                    WHERE n.nspname OPERATOR(pg_catalog.=) 'sitefactory_runtime' AND c.relkind OPERATOR(pg_catalog.=) 'r'
                    AND (pg_catalog.has_table_privilege(r.oid, c.oid, 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER,MAINTAIN')
                        OR pg_catalog.has_any_column_privilege(r.oid, c.oid, 'INSERT,UPDATE,REFERENCES'))),
                pg_catalog.has_database_privilege(r.oid, pg_catalog.current_database(), 'CONNECT WITH GRANT OPTION')
                    OR EXISTS (SELECT 1 FROM pg_catalog.pg_namespace n WHERE (n.nspname OPERATOR(pg_catalog.=) 'public'
                        OR n.nspname OPERATOR(pg_catalog.=) 'sitefactory_runtime')
                        AND pg_catalog.has_schema_privilege(r.oid, n.oid, 'USAGE WITH GRANT OPTION'))
                    OR EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid OPERATOR(pg_catalog.=) c.relnamespace
                        WHERE n.nspname OPERATOR(pg_catalog.=) 'sitefactory_runtime' AND c.relkind OPERATOR(pg_catalog.=) 'r'
                        AND pg_catalog.has_any_column_privilege(r.oid, c.oid, 'SELECT WITH GRANT OPTION'))
            FROM pg_catalog.pg_roles r WHERE r.rolname OPERATOR(pg_catalog.=) current_user
            """, connection, transaction);
        await using NpgsqlDataReader row = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await row.ReadAsync(token).ConfigureAwait(false) || row.GetString(5) != "on")
            throw new InvalidOperationException("The PostgreSQL probe requires a read-only role snapshot.");
        DatabaseRolePrivileges privileges = DatabaseRolePrivileges.None;
        for (int column = 6; column < 18; column++)
            if (row.GetBoolean(column)) privileges |= (DatabaseRolePrivileges)(1 << (column - 6));
        return new(row.GetString(0), row.GetString(1), row.GetBoolean(2), row.GetBoolean(3), row.GetString(4), privileges);
    }
}
