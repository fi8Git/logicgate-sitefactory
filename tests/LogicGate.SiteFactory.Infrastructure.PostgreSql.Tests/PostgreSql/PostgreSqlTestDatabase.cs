using System.Buffers;
using System.Globalization;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

/// <summary>Owns only one randomly named database on an explicitly provisioned temporary cluster.</summary>
internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");
    private readonly PostgreSqlTestConfiguration configuration;
    private bool disposed;

    private PostgreSqlTestDatabase(PostgreSqlTestConfiguration configuration)
    {
        this.configuration = configuration;
        Name = "sf_test_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    }

    public string Name { get; }

    internal static bool IsOwnedName(string name) => name.Length == 40 && name.StartsWith("sf_test_", StringComparison.Ordinal)
        && !name.AsSpan(8).ContainsAnyExcept(HexDigits);

    public static async Task<PostgreSqlTestDatabase> CreateAsync(PostgreSqlTestConfiguration configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PostgreSqlTestDatabase database = new(configuration);
        try
        {
            await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await admin.OpenAsync(cancellationToken);
            await VerifyClusterAsync(admin, cancellationToken);
            // Name is minted internally, never supplied by an environment variable, test name or caller.
            await using NpgsqlCommand create = new($"CREATE DATABASE \"{database.Name}\" TEMPLATE template0", admin);
            await create.ExecuteNonQueryAsync(cancellationToken);
            return database;
        }
        catch (NpgsqlException)
        {
            // No provider message/inner exception, endpoint, query or connection material in test output.
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-004 Could not create an isolated PostgreSQL test database.");
        }
    }

    private static async Task VerifyClusterAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new("""
            SELECT pg_catalog.current_setting('server_version_num')::pg_catalog.int4 OPERATOR(pg_catalog./) 10000,
                   pg_catalog.current_database(), current_user,
                   (SELECT ssl FROM pg_catalog.pg_stat_ssl WHERE pid OPERATOR(pg_catalog.=) pg_catalog.pg_backend_pid()),
                   EXISTS (SELECT 1 FROM pg_catalog.pg_database
                           WHERE datname OPERATOR(pg_catalog.<>) 'postgres' AND datname OPERATOR(pg_catalog.<>) 'template0'
                             AND datname OPERATOR(pg_catalog.<>) 'template1' AND datname OPERATOR(pg_catalog.<>) 'sitefactory_test_admin'
                             AND datname OPERATOR(pg_catalog.!~) '^sf_test_[0-9a-f]{32}$')
            """, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)
            || reader.GetInt32(0) != PostgreSqlTestConfiguration.MajorVersion
            || reader.GetString(1) != PostgreSqlTestConfiguration.AdministrativeDatabase
            || reader.GetString(2) != PostgreSqlTestConfiguration.AdministrativeDatabase
            || !reader.GetBoolean(3) || reader.GetBoolean(4))
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-005 PostgreSQL 18, TLS and an exclusively temporary test cluster are required.");
        }
    }

    public async Task ExecuteAsync(Func<NpgsqlConnection, CancellationToken, Task> test, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            await using NpgsqlConnection connection = configuration.Connection(Name);
            await connection.OpenAsync(cancellationToken);
            await test(connection, cancellationToken);
        }
        catch (NpgsqlException)
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-006 PostgreSQL test operation failed (provider details withheld).");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        if (!IsOwnedName(Name))
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-007 Refusing cleanup of an unowned database.");

        // Cleanup must still run after cancellation of the test. Independently bounded, never global pool clearing.
        using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
        try
        {
            await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await admin.OpenAsync(cleanup.Token);
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(cleanup.Token);
            disposed = true;
        }
        catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException)
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-008 Temporary database cleanup failed; destroy the dedicated test cluster.");
        }
    }
}
