using System.Diagnostics;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;

public enum PostgreSqlComponent
{
    Web,
    Worker,
    Migrator,
}

/// <summary>Infrastructure-only startup snapshot. Public representation never exposes connection material.</summary>
public sealed class PostgreSqlRuntimeConfiguration
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string connection;

    internal PostgreSqlRuntimeConfiguration(PostgreSqlComponent component, string connection)
    {
        Component = component;
        this.connection = connection;
    }

    public PostgreSqlComponent Component { get; }
    public override string ToString() => $"PostgreSQL {Component}: configured";

    // Each Infrastructure consumer owns its closed connection; preserve all validated runtime settings.
    internal NpgsqlConnection CreateConnection() => new(connection);

    // Construction stays offline; consumers of a future probe must explicitly open this connection.
    internal NpgsqlConnection CreateProbeConnection() => new(new NpgsqlConnectionStringBuilder(connection)
    {
        Pooling = false,
        Multiplexing = false,
        NoResetOnClose = false,
        Timeout = 3,
        CommandTimeout = 3,
        CancellationTimeout = 1000,
        ApplicationName = "sitefactory-compatibility-probe",
    }.ConnectionString);
}

public sealed class PostgreSqlConfigurationException : Exception
{
    internal PostgreSqlConfigurationException(string code, string summary) : base(code + " PostgreSql " + summary) => Code = code;
    public string Code { get; }
    public override string ToString() => Message;
}
