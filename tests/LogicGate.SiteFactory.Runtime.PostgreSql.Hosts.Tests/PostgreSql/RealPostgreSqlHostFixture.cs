using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;
using LogicGate.SiteFactory.Migrator;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql;

// Setup owns privileged mutations; runtime hosts only receive their dedicated private file reference.
internal sealed class RealPostgreSqlHostFixture : IAsyncDisposable
{
    // Configure the closed test-only event sink before the first Npgsql use in any P03 Fact.
    static RealPostgreSqlHostFixture() => P03Observations.InitializeProviderLogging();

    private const string Prefix = "sitefactory-p03-secrets-";
    private readonly PostgreSqlTestConfiguration configuration;
    private readonly Dictionary<PostgreSqlTestLogin, string> files = [];
    private RealPostgreSqlHostFixture(PostgreSqlTestConfiguration configuration, PostgreSqlRoleFixture roles)
    {
        this.configuration = configuration;
        Roles = roles;
        DirectoryPath = Directory.CreateTempSubdirectory(Prefix).FullName;
    }

    internal PostgreSqlRoleFixture Roles { get; }
    internal string DirectoryPath { get; }
    internal string FileFor(PostgreSqlTestLogin login) => files[login];

    internal static async Task<RealPostgreSqlHostFixture> CreateAsync(CancellationToken token)
    {
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(token);
        PostgreSqlRoleFixture roles = await PostgreSqlRoleFixture.CreateAsync(configuration, token);
        RealPostgreSqlHostFixture? fixture = null;
        try
        {
            fixture = new(configuration, roles);
            fixture.ProtectDirectory();
            foreach (PostgreSqlTestLogin login in Enum.GetValues<PostgreSqlTestLogin>())
            {
                string path = Path.Combine(fixture.DirectoryPath, login + "-connection");
                await roles.WriteConnectionFileAsync(login, path, token);
                fixture.files.Add(login, path);
            }
            await fixture.RunMigratorAsync(["apply", "--environment", "Test", "--confirm-migration"], "Runtime metadata migration completed.", token);
            await fixture.RunMigratorAsync(["bootstrap", "--environment", "Test", "--client-slug", "example-client",
                "--database-instance-id", "11111111-1111-4111-8111-111111111111", "--operation-id", "22222222-2222-4222-8222-222222222222",
                "--confirm-bootstrap"], "Runtime metadata bootstrap completed.", token);
            return fixture;
        }
        catch
        {
            if (fixture is not null) await fixture.DisposeAsync();
            else await roles.DisposeAsync();
            throw;
        }
    }

    private void ProtectDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            SecurityIdentifier owner = identity.User ?? throw new InvalidOperationException("P03 requires an explicit test user.");
            DirectorySecurity security = new();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(DirectoryPath).SetAccessControl(security);
        }
        else File.SetUnixFileMode(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private async Task RunMigratorAsync(string[] arguments, string expected, CancellationToken token)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await MigrationCommand.RunAsync(arguments, FileFor(PostgreSqlTestLogin.Migrator), output, error, token);
        Assert.Equal(0, exit);
        Assert.Empty(error.ToString());
        Assert.Equal(expected + Environment.NewLine, output.ToString());
        await AssertRedactedAsync(FileFor(PostgreSqlTestLogin.Migrator), output + error.ToString(), token);
    }

    internal Task AdminAsync(string sql, CancellationToken token) => Roles.Database.ExecuteAsync(async (connection, cancellationToken) =>
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }, token);

    internal Task<string[]> SnapshotAsync(CancellationToken token) => PostgreSqlHostSnapshot.ReadAsync(Roles, token);

    internal async Task WithoutMutationAsync(Func<Task> action, CancellationToken token)
    {
        string[] before = await SnapshotAsync(token);
        try { await action(); }
        finally
        {
            using CancellationTokenSource verification = new(TimeSpan.FromSeconds(15));
            PostgreSqlHostSnapshot.AssertUnchanged(before, await SnapshotAsync(verification.Token));
        }
    }

    internal async Task MissingMetadataAsync(Func<string, Task> check, CancellationToken token)
    {
        string[] codes = ["SF-DB-STARTUP-003", "SF-SCHEMA-STARTUP-001", "SF-SCHEMA-STARTUP-004"];
        for (int index = 0; index < PostgreSqlHostSnapshot.MetadataTables.Length; index++)
        {
            string table = PostgreSqlHostSnapshot.MetadataTables[index];
            await AdminAsync("CREATE TABLE sitefactory_runtime.sf_test_saved AS TABLE sitefactory_runtime." + table
                + "; DELETE FROM sitefactory_runtime." + table, token);
            try { await check(codes[index]); }
            finally
            {
                using CancellationTokenSource restoration = new(TimeSpan.FromSeconds(15));
                await AdminAsync("INSERT INTO sitefactory_runtime." + table
                    + " SELECT * FROM sitefactory_runtime.sf_test_saved; DROP TABLE sitefactory_runtime.sf_test_saved", restoration.Token);
            }
        }
    }

    internal async Task MutationAsync(string setup, string restore, Func<Task> check, CancellationToken token)
    {
        await AdminAsync(setup, token);
        try { await check(); }
        finally
        {
            using CancellationTokenSource restoration = new(TimeSpan.FromSeconds(15));
            await AdminAsync(restore, restoration.Token);
        }
    }

    internal async Task<string> ChangedConnectionAsync(PostgreSqlTestLogin login, string name,
        Action<NpgsqlConnectionStringBuilder> change, CancellationToken token)
    {
        NpgsqlConnectionStringBuilder settings = new(await File.ReadAllTextAsync(FileFor(login), token));
        change(settings);
        string file = Path.Combine(DirectoryPath, name + "-connection");
        await PostgreSqlRoleFixture.WritePrivateConnectionAsync(settings.ConnectionString, file, token);
        return file;
    }

    internal static string InvalidPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "';--";

    internal static async Task AssertRedactedAsync(string file, string text, CancellationToken token)
    {
        NpgsqlConnectionStringBuilder credential = new(await File.ReadAllTextAsync(file, token));
        foreach (string forbidden in new[] { credential.Password!, credential.ConnectionString, credential.Username!, file, Path.GetDirectoryName(file)! })
            Assert.True(!text.Contains(forbidden, StringComparison.Ordinal), "Runtime output disclosed private connection material.");
        // Kestrel's own loopback URL is legitimate in host logs; public HTTP payloads are asserted exactly elsewhere.
    }

    internal async Task AssertAllRedactedAsync(string text, CancellationToken token)
    {
        foreach (string file in Directory.GetFiles(DirectoryPath, "*-connection")) await AssertRedactedAsync(file, text, token);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            try
            {
                using CancellationTokenSource verification = new(TimeSpan.FromSeconds(15));
                await AssertSessionsClosedAsync(verification.Token);
            }
            finally { await Roles.DisposeAsync(); }
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
            await using NpgsqlConnection admin = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await admin.OpenAsync(cleanup.Token);
            await using NpgsqlCommand command = new("SELECT (SELECT pg_catalog.count(*) FROM pg_catalog.pg_database WHERE datname = $1) + "
                + "(SELECT pg_catalog.count(*) FROM pg_catalog.pg_roles WHERE rolname = ANY($2))", admin);
            command.Parameters.AddWithValue(Roles.Database.Name);
            command.Parameters.AddWithValue(Roles.Baseline.Names);
            Assert.Equal(0L, await command.ExecuteScalarAsync(cleanup.Token));
        }
        finally
        {
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, DirectoryPath, comparison);
            Assert.StartsWith(Prefix, Path.GetFileName(DirectoryPath), StringComparison.Ordinal);
            Assert.False((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(DirectoryPath, recursive: true);
            Assert.False(Directory.Exists(DirectoryPath));
        }
    }

    internal Task AssertSessionsClosedAsync(CancellationToken token) => Roles.Database.ExecuteAsync(async (connection, cancellationToken) =>
    {
        await using NpgsqlCommand command = new("SELECT pg_catalog.count(*) FROM pg_catalog.pg_stat_activity WHERE usename = ANY($1)", connection);
        command.Parameters.AddWithValue(Roles.Baseline.Names);
        Assert.Equal(0L, await command.ExecuteScalarAsync(cancellationToken));
    }, token);
}
