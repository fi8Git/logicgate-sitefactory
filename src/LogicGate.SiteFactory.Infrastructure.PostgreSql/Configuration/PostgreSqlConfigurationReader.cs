using System.Text;
using LogicGate.SiteFactory.Runtime.Contracts;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;

/// <summary>Only the current component's external file is read. No environment credential fallback or network I/O.</summary>
public static class PostgreSqlConfigurationReader
{
    private const int MaximumBytes = 16384;

    public static string ConnectionFileKey(PostgreSqlComponent component) => component switch
    {
        PostgreSqlComponent.Web => "SITEFACTORY_WEB_CONNECTION_FILE",
        PostgreSqlComponent.Worker => "SITEFACTORY_WORKER_CONNECTION_FILE",
        _ => throw new ArgumentOutOfRangeException(nameof(component)),
    };

    public static async Task<PostgreSqlRuntimeConfiguration> ReadAsync(PostgreSqlComponent component,
        string? connectionFile, string environment, CancellationToken cancellationToken)
    {
        // The Migrator supplies its reference explicitly; runtime hosts do not know its configuration key.
        if (!Enum.IsDefined(component)) throw new ArgumentOutOfRangeException(nameof(component));
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.GetNames<RuntimeEnvironment>().Contains(environment, StringComparer.OrdinalIgnoreCase))
            throw new PostgreSqlConfigurationException("SF-RUNTIME-CONFIG-007", "An explicit supported environment is required.");
        if (string.IsNullOrWhiteSpace(connectionFile))
            throw new PostgreSqlConfigurationException("SF-SECRET-001", "A dedicated connection file is required.");

        string content;
        try
        {
            FileInfo file = new(Path.GetFullPath(connectionFile));
            for (FileSystemInfo? entry = file; entry is not null; entry = entry is FileInfo info ? info.Directory : ((DirectoryInfo)entry).Parent)
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new PostgreSqlConfigurationException("SF-SECRET-004", "The connection file must not follow a symbolic link.");
            if ((file.Attributes & FileAttributes.Directory) != 0)
                throw new PostgreSqlConfigurationException("SF-SECRET-004", "The connection file is unreadable or is not valid UTF-8.");
            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode mode = File.GetUnixFileMode(file.FullName);
                if (mode != UnixFileMode.UserRead && mode != (UnixFileMode.UserRead | UnixFileMode.UserWrite))
                    throw new PostgreSqlConfigurationException("SF-SECRET-005", "The connection file requires owner-only read permissions.");
            }
            await using FileStream stream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] bytes = new byte[MaximumBytes + 1];
            int count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            if (count > MaximumBytes) throw InvalidConnection();
            content = new UTF8Encoding(false, true).GetString(bytes, 0, count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Do not retain provider/file exceptions: their messages may contain a path or secret.
            throw new PostgreSqlConfigurationException("SF-SECRET-004", "The connection file is unreadable or is not valid UTF-8.");
        }

        // Remove at most one terminal line ending; never trim spaces or normalize password characters.
        if (content.EndsWith("\r\n", StringComparison.Ordinal)) content = content[..^2];
        else if (content.EndsWith('\n')) content = content[..^1];
        if (content.Length == 0) throw new PostgreSqlConfigurationException("SF-SECRET-002", "The connection file is empty.");

        NpgsqlConnectionStringBuilder settings;
        try
        {
            settings = new(content);
        }
        catch (ArgumentException)
        {
            throw InvalidConnection();
        }
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Database)
            || string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password)
            || settings.Port is < 1 or > 65535)
            throw InvalidConnection();
        if (settings.PersistSecurityInfo || settings.IncludeErrorDetail || settings.LogParameters
            || settings.Passfile is not null || settings.SslMode == SslMode.Disable)
            throw new PostgreSqlConfigurationException("SF-RUNTIME-CONFIG-009", "Unsafe connection options are not supported.");
        if (environment.Equals(nameof(RuntimeEnvironment.Production), StringComparison.OrdinalIgnoreCase)
            && (settings.SslMode != SslMode.VerifyFull || settings.Username.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                || IsPlaceholder(settings.Password) || IsPlaceholder(settings.Database) || IsPlaceholder(settings.Username)
                || settings.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
            throw new PostgreSqlConfigurationException("SF-RUNTIME-CONFIG-009", "Production requires verified TLS and non-placeholder dedicated credentials.");

        cancellationToken.ThrowIfCancellationRequested();
        return new(component, settings.ConnectionString);
    }

    private static PostgreSqlConfigurationException InvalidConnection() => new(
        "SF-RUNTIME-CONFIG-004", "A valid connection with explicit Host, Database, Username and Password is required.");

    private static bool IsPlaceholder(string value) => new[] { "changeme", "password", "replace-me", "example-secret", "TODO" }
        .Contains(value.Trim(), StringComparer.OrdinalIgnoreCase) || (value.Trim().StartsWith('<') && value.Trim().EndsWith('>'));
}
