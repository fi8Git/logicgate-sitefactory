using System.Diagnostics;
using System.Net;
using System.Text;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

internal sealed class PostgreSqlTestConfiguration
{
    public const string FileVariable = "SITEFACTORY_TEST_POSTGRES_ADMIN_FILE";
    public const string RequiredVariable = "SITEFACTORY_TEST_POSTGRES_REQUIRED";
    public const string AdministrativeDatabase = "sitefactory_test_admin";
    public const int MajorVersion = 18;
    private const int MaximumBytes = 16384;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string connectionString;

    private PostgreSqlTestConfiguration(string connectionString) => this.connectionString = connectionString;

    public override string ToString() => "PostgreSQL integration test configuration (redacted)";

    public static async Task<PostgreSqlTestConfiguration> ReadAsync(string? file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(file))
            throw Error("SF-TEST-PG-001 Dedicated temporary administrative connection file required.");

        string content;
        try
        {
            await using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] bytes = new byte[MaximumBytes + 1];
            int count = await stream.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
            if (count > MaximumBytes)
                throw Error("SF-TEST-PG-002 Invalid administrative test configuration.");
            content = new UTF8Encoding(false, true).GetString(bytes, 0, count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Error("SF-TEST-PG-002 Administrative test configuration is unreadable.");
        }

        return Parse(content);
    }

    internal static PostgreSqlTestConfiguration Parse(string content)
    {
        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new(content);
        }
        catch (ArgumentException)
        {
            throw Error("SF-TEST-PG-002 Invalid administrative test configuration.");
        }

        // No DNS, default port, developer database, inherited credential or remote server.
        string[] allowed = ["Host", "Port", "Database", "Username", "Password", "SSL Mode", "Root Certificate"];
        if (settings.Keys.Cast<string>().Any(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase))
            || !IPAddress.TryParse(settings.Host, out IPAddress? address) || !IPAddress.IsLoopback(address)
            || !settings.ShouldSerialize("Port") || settings.Port is < 1 or > 65535
            || settings.Database != AdministrativeDatabase || settings.Username != AdministrativeDatabase
            || string.IsNullOrWhiteSpace(settings.Password)
            || settings.SslMode != SslMode.VerifyFull || string.IsNullOrWhiteSpace(settings.RootCertificate))
        {
            throw Error("SF-TEST-PG-003 Only an explicit loopback, dedicated test administrator and verified TLS are accepted.");
        }

        settings.Pooling = false;
        settings.Timeout = 5;
        settings.CommandTimeout = 10;
        settings.ApplicationName = "sitefactory-integration-tests";
        settings.IncludeErrorDetail = false;
        settings.LogParameters = false;
        settings.PersistSecurityInfo = false;
        return new(settings.ConnectionString);
    }

    internal NpgsqlConnection Connection(string database) => new(new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = database,
    }.ConnectionString);

    internal static InvalidOperationException Error(string message) => new(message);
}
