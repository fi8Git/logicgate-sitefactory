namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

// Versioned integration-only ACL profile. It is not a production name generator or client provisioning API.
internal sealed class PostgreSqlRoleBaseline
{
    public PostgreSqlRoleBaseline(string database)
    {
        if (!PostgreSqlTestDatabase.IsOwnedName(database))
            throw new ArgumentException("An internally allocated test database is required.", nameof(database));
        Database = database;
        string prefix = "sf_" + database[8..] + "_test_";
        Owner = prefix + "owner";
        Migrator = prefix + "migrator";
        Web = prefix + "web";
        Worker = prefix + "worker";
    }

    public string Database { get; }
    public string Owner { get; }
    public string Migrator { get; }
    public string Web { get; }
    public string Worker { get; }
    public string[] Names => [Owner, Migrator, Web, Worker];

    // All interpolated identifiers come from the validated, fixed ASCII names above. No credentials in this SQL.
    public string Sql => $"""
        CREATE ROLE "{Owner}" NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT CONNECTION LIMIT 0;
        CREATE ROLE "{Migrator}" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT CONNECTION LIMIT 2;
        CREATE ROLE "{Web}" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT CONNECTION LIMIT 4;
        CREATE ROLE "{Worker}" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT CONNECTION LIMIT 4;

        GRANT "{Owner}" TO "{Migrator}" WITH INHERIT FALSE;
        GRANT "{Owner}" TO "{Migrator}" WITH SET TRUE;
        GRANT "{Owner}" TO "{Migrator}" WITH ADMIN FALSE;

        ALTER DATABASE "{Database}" OWNER TO "{Owner}";
        REVOKE ALL ON DATABASE "{Database}" FROM PUBLIC;
        GRANT CONNECT ON DATABASE "{Database}" TO "{Migrator}", "{Web}", "{Worker}";
        ALTER SCHEMA public OWNER TO "{Owner}";
        REVOKE ALL ON SCHEMA public FROM PUBLIC;
        CREATE SCHEMA sitefactory_runtime AUTHORIZATION "{Owner}";
        REVOKE ALL ON SCHEMA sitefactory_runtime FROM PUBLIC;
        GRANT USAGE ON SCHEMA sitefactory_runtime TO "{Web}", "{Worker}";

        ALTER ROLE "{Owner}" IN DATABASE "{Database}" SET search_path = pg_catalog, sitefactory_runtime;
        ALTER ROLE "{Migrator}" IN DATABASE "{Database}" SET search_path = pg_catalog, sitefactory_runtime;
        ALTER ROLE "{Web}" IN DATABASE "{Database}" SET search_path = pg_catalog, sitefactory_runtime;
        ALTER ROLE "{Worker}" IN DATABASE "{Database}" SET search_path = pg_catalog, sitefactory_runtime;
        ALTER ROLE "{Migrator}" SET statement_timeout = '10s';
        ALTER ROLE "{Migrator}" SET lock_timeout = '3s';
        ALTER ROLE "{Migrator}" SET idle_in_transaction_session_timeout = '10s';
        ALTER ROLE "{Web}" SET statement_timeout = '10s';
        ALTER ROLE "{Web}" SET lock_timeout = '3s';
        ALTER ROLE "{Web}" SET idle_in_transaction_session_timeout = '10s';
        ALTER ROLE "{Worker}" SET statement_timeout = '10s';
        ALTER ROLE "{Worker}" SET lock_timeout = '3s';
        ALTER ROLE "{Worker}" SET idle_in_transaction_session_timeout = '10s';

        ALTER DEFAULT PRIVILEGES FOR ROLE "{Owner}" REVOKE ALL ON TABLES FROM PUBLIC;
        ALTER DEFAULT PRIVILEGES FOR ROLE "{Owner}" REVOKE ALL ON SEQUENCES FROM PUBLIC;
        -- These revocations must be global: a per-schema REVOKE cannot remove the built-in PUBLIC defaults.
        ALTER DEFAULT PRIVILEGES FOR ROLE "{Owner}" REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
        ALTER DEFAULT PRIVILEGES FOR ROLE "{Owner}" REVOKE USAGE ON TYPES FROM PUBLIC;
        ALTER DEFAULT PRIVILEGES FOR ROLE "{Owner}" REVOKE ALL ON SCHEMAS FROM PUBLIC;
        ALTER DEFAULT PRIVILEGES FOR ROLE "{Owner}" IN SCHEMA sitefactory_runtime
            GRANT SELECT ON TABLES TO "{Web}", "{Worker}";
        """;
}
