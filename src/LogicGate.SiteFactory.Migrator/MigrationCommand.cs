using System.Data.Common;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogicGate.SiteFactory.Migrator;

/// <summary>Explicit development/test runner, not the production migration workflow.</summary>
public static class MigrationCommand
{
    public const string InitialMigration = "20260923093309_CreateRuntimeMetadata";
    private const string Usage = "LogicGate.SiteFactory.Migrator apply --environment <Development|Test> --confirm-migration";

    public static async Task<int> RunAsync(string[] args, string? connectionFile,
        TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args is ["--help"])
        {
            await output.WriteLineAsync(Usage).ConfigureAwait(false);
            await output.WriteLineAsync(RuntimeMetadataBootstrap.Usage).ConfigureAwait(false);
            return 0;
        }
        bool bootstrap = args is ["bootstrap", ..];
        MetadataBootstrapRequest? request = bootstrap ? RuntimeMetadataBootstrap.Parse(args) : null;
        if (bootstrap && request is null)
            return await FailAsync(error, 2, "SF-BOOTSTRAP-USAGE-001 Explicit bootstrap with a valid identity in Development or Test is required.").ConfigureAwait(false);
        if (!bootstrap && args is not ["apply", "--environment", "Development" or "Test", "--confirm-migration"])
            return await FailAsync(error, 2, "SF-MIGRATION-USAGE-001 Explicit apply in Development or Test is required.").ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            PostgreSqlRuntimeConfiguration configuration;
            try
            {
                configuration = await PostgreSqlConfigurationReader.ReadAsync(PostgreSqlComponent.Migrator,
                    connectionFile, args[2], cancellationToken).ConfigureAwait(false);
            }
            catch (PostgreSqlConfigurationException)
            {
                return await FailAsync(error, 2, "SF-MIGRATION-CONFIG-001 The dedicated migration connection file is invalid or unreadable.").ConfigureAwait(false);
            }

            await using RuntimeDbContext context = RuntimeDbContextFactory.Create(configuration);
            // Fail closed if the packaged composition changes; never resolve an implicit future latest.
            if (!context.Database.GetMigrations().SequenceEqual([InitialMigration], StringComparer.Ordinal)
                || context.Database.HasPendingModelChanges())
                return await FailAsync(error, 1, "SF-MIGRATION-MODEL-001 The packaged migration does not match the expected model.").ConfigureAwait(false);

            // Require an existing database. Do not let EF provision a missing database.
            DbConnection connection = context.Database.GetDbConnection();
            // A one-shot elevated session must physically close, even if the supplied connection enabled pooling.
            DbConnectionStringBuilder settings = new() { ConnectionString = connection.ConnectionString };
            settings["Pooling"] = false;
            settings["Multiplexing"] = false;
            settings["No Reset On Close"] = false;
            connection.ConnectionString = settings.ConnectionString;
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!await AssumeDatabaseOwnerAsync(connection, cancellationToken).ConfigureAwait(false))
                return await FailAsync(error, 1, "SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required.").ConfigureAwait(false);
            if (request is not null)
            {
                string? diagnostic = await RuntimeMetadataBootstrap.ExecuteAsync(context, request, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                if (diagnostic is not null) return await FailAsync(error, 1, diagnostic).ConfigureAwait(false);
                await output.WriteLineAsync("Runtime metadata bootstrap completed.").ConfigureAwait(false);
                return 0;
            }
            string[] applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            if (applied.Any(migration => migration != InitialMigration))
                return await FailAsync(error, 1, "SF-MIGRATION-HISTORY-001 The database contains an unsupported migration history.").ConfigureAwait(false);

            await context.Database.MigrateAsync(InitialMigration, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync("Runtime metadata migration completed.").ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (bootstrap)
                return await FailAsync(error, 130, "SF-BOOTSTRAP-CANCELLED-001 Bootstrap cancelled; inspect database state before retrying.").ConfigureAwait(false);
            return await FailAsync(error, 130, "SF-MIGRATION-CANCELLED-001 Migration cancelled; inspect database state before retrying.").ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (bootstrap && exception.InnerException is DbException)
        {
            return await FailAsync(error, 1, "SF-BOOTSTRAP-FAILED-001 Bootstrap failed; inspect database state before retrying.").ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Provider errors can contain SQL, credentials and local paths. Never echo them.
            if (bootstrap)
                return await FailAsync(error, 1, "SF-BOOTSTRAP-FAILED-001 Bootstrap failed; inspect database state before retrying.").ConfigureAwait(false);
            return await FailAsync(error, 1, "SF-MIGRATION-FAILED-001 Migration failed; inspect database state before retrying.").ConfigureAwait(false);
        }
    }

    private static async Task<bool> AssumeDatabaseOwnerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        // Read-only preflight. Never infer a production role name or create/repair roles and grants.
        // The selected database's owner is authoritative for this Development/Test runner.
        // Qualify operators too: a connection may put an untrusted schema before pg_catalog.
        await using DbCommand query = connection.CreateCommand();
        query.CommandText = """
            SELECT pg_catalog.quote_ident(owner.rolname)
            FROM pg_catalog.pg_database database
            JOIN pg_catalog.pg_roles owner ON owner.oid OPERATOR(pg_catalog.=) database.datdba
            JOIN pg_catalog.pg_roles login ON login.rolname OPERATOR(pg_catalog.=) session_user
            JOIN pg_catalog.pg_auth_members membership ON membership.roleid OPERATOR(pg_catalog.=) owner.oid
                AND membership.member OPERATOR(pg_catalog.=) login.oid
            WHERE database.datname OPERATOR(pg_catalog.=) pg_catalog.current_database()
              AND session_user OPERATOR(pg_catalog.=) current_user AND login.oid OPERATOR(pg_catalog.<>) owner.oid
              AND login.rolcanlogin AND NOT login.rolinherit
              AND NOT (login.rolsuper OR login.rolcreatedb OR login.rolcreaterole OR login.rolreplication OR login.rolbypassrls)
              AND NOT owner.rolcanlogin AND NOT owner.rolinherit
              AND NOT (owner.rolsuper OR owner.rolcreatedb OR owner.rolcreaterole OR owner.rolreplication OR owner.rolbypassrls)
              AND membership.set_option AND NOT membership.inherit_option AND NOT membership.admin_option
              -- Exact two-role graph: the only outgoing grant is login -> owner, and owner is terminal.
              -- Any other direct/transitive path, including SET-only, inherited or ADMIN-only, is forbidden.
              AND (SELECT pg_catalog.count(*) FROM pg_catalog.pg_auth_members
                   WHERE member OPERATOR(pg_catalog.=) login.oid) OPERATOR(pg_catalog.=) 1
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members WHERE member OPERATOR(pg_catalog.=) owner.oid)
            """;
        if (await query.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not string quotedOwner)
            return false;
        // Identifier quoted by PostgreSQL itself; never interpolate a CLI/configuration value into SQL.
        query.CommandText = "SET ROLE " + quotedOwner;
        await query.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        // The enclosing await using closes this non-pooled connection on success, failure and cancellation.
        return true;
    }

    private static async Task<int> FailAsync(TextWriter error, int exitCode, string diagnostic)
    {
        await error.WriteLineAsync(diagnostic).ConfigureAwait(false);
        return exitCode;
    }
}
