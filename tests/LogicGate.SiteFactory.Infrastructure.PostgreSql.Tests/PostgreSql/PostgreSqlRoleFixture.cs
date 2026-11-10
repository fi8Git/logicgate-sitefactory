using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

internal enum PostgreSqlTestLogin
{
    Migrator,
    Web,
    Worker,
}

internal sealed class PostgreSqlRoleFixture : IAsyncDisposable
{
    private readonly PostgreSqlTestConfiguration configuration;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Dictionary<PostgreSqlTestLogin, string> passwords = new()
    {
        [PostgreSqlTestLogin.Migrator] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        [PostgreSqlTestLogin.Web] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        [PostgreSqlTestLogin.Worker] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
    };
    private bool rolesCreated;

    private PostgreSqlRoleFixture(PostgreSqlTestConfiguration configuration, PostgreSqlTestDatabase database)
    {
        this.configuration = configuration;
        Database = database;
        Baseline = new(database.Name);
    }

    public PostgreSqlTestDatabase Database { get; }
    public PostgreSqlRoleBaseline Baseline { get; }

    public string Name(PostgreSqlTestLogin login) => login switch
    {
        PostgreSqlTestLogin.Migrator => Baseline.Migrator,
        PostgreSqlTestLogin.Web => Baseline.Web,
        PostgreSqlTestLogin.Worker => Baseline.Worker,
        _ => throw new ArgumentOutOfRangeException(nameof(login)),
    };

    public static async Task<PostgreSqlRoleFixture> CreateAsync(PostgreSqlTestConfiguration configuration, CancellationToken cancellationToken)
    {
        PostgreSqlRoleFixture fixture = new(configuration, await PostgreSqlTestDatabase.CreateAsync(configuration, cancellationToken));
        bool initialized = false;
        try
        {
            await fixture.Database.ExecuteAsync(async (connection, token) =>
            {
                // External clusters must be prepared explicitly by their test operator. Never mutate system ACLs here.
                await using NpgsqlCommand guard = new("""
                    SELECT EXISTS (
                        SELECT 1 FROM pg_catalog.pg_database d,
                        LATERAL pg_catalog.aclexplode(coalesce(d.datacl, pg_catalog.acldefault('d', d.datdba))) a
                        WHERE (d.datname OPERATOR(pg_catalog.=) 'postgres' OR d.datname OPERATOR(pg_catalog.=) 'template0'
                            OR d.datname OPERATOR(pg_catalog.=) 'template1' OR d.datname OPERATOR(pg_catalog.=) 'sitefactory_test_admin')
                        AND a.grantee OPERATOR(pg_catalog.=) 0)
                    """, connection);
                if (await guard.ExecuteScalarAsync(token) is not false)
                    throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-013 Dedicated cluster system database ACLs must be prepared; see tests/PostgreSql/README.md.");
                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
                await using NpgsqlCommand bootstrap = new(fixture.Baseline.Sql, connection, transaction);
                await bootstrap.ExecuteNonQueryAsync(token);
                foreach (PostgreSqlTestLogin login in Enum.GetValues<PostgreSqlTestLogin>())
                {
                    // DDL has no bind slots. Only internal ASCII identifiers and cryptographically random hex
                    // are interpolated here. This ephemeral command is never logged, saved or returned.
                    await using NpgsqlCommand password = new($"ALTER ROLE \"{fixture.Name(login)}\" PASSWORD '{fixture.passwords[login]}'", connection, transaction);
                    await password.ExecuteNonQueryAsync(token);
                }
                await transaction.CommitAsync(token);
                fixture.rolesCreated = true;
            }, cancellationToken);
            initialized = true;
            return fixture;
        }
        finally
        {
            if (!initialized)
                await fixture.DisposeAsync();
        }
    }

    // A real new authenticated session, not SET ROLE from a superuser test connection.
    public async Task ExecuteAsync(PostgreSqlTestLogin login, Func<NpgsqlConnection, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using NpgsqlConnection prototype = configuration.Connection(Database.Name);
        NpgsqlConnectionStringBuilder settings = new(prototype.ConnectionString)
        {
            Username = Name(login),
            Password = passwords[login],
        };
        try
        {
            await using NpgsqlConnection connection = new(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await action(connection, cancellationToken);
        }
        catch (NpgsqlException)
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-010 Role test failed (provider details withheld).");
        }
    }

    // Test subprocess hand-off: only a private file reference leaves this fixture, never a password argument.
    public async Task WriteConnectionFileAsync(PostgreSqlTestLogin login, string path, CancellationToken cancellationToken)
    {
        using NpgsqlConnection prototype = configuration.Connection(Database.Name);
        NpgsqlConnectionStringBuilder settings = new(prototype.ConnectionString)
        {
            Username = Name(login),
            Password = passwords[login],
            // Exercise the runner's own non-pooling safeguard, not just the harness default.
            Pooling = true,
            NoResetOnClose = true,
        };
        await WritePrivateConnectionAsync(settings.ConnectionString, path, cancellationToken);
    }

    internal static async Task WritePrivateConnectionAsync(string connection, string path, CancellationToken cancellationToken)
    {
        FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using FileStream file = new(path, options);
        await file.WriteAsync(Encoding.UTF8.GetBytes(connection), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // Drop only our database first. No DROP OWNED/CASCADE that could touch another database or role.
        await Database.DisposeAsync();
        if (!rolesCreated)
            return;
        using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
        try
        {
            await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await admin.OpenAsync(cleanup.Token);
            await using NpgsqlTransaction transaction = await admin.BeginTransactionAsync(cleanup.Token);
            foreach (string name in new[] { Baseline.Web, Baseline.Worker, Baseline.Migrator, Baseline.Owner })
            {
                await using NpgsqlCommand drop = new($"DROP ROLE \"{name}\"", admin, transaction);
                await drop.ExecuteNonQueryAsync(cleanup.Token);
            }
            await transaction.CommitAsync(cleanup.Token);
            rolesCreated = false;
        }
        catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException)
        {
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-011 Temporary role cleanup failed; destroy the dedicated test cluster.");
        }
    }
}
