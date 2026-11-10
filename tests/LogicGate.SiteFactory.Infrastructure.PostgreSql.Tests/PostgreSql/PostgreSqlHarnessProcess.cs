using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Cli.Tests;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

// Private transport between PowerShell and the existing test-only job/subreaper.
// Captured streams travel over redirected pipes, never through public diagnostics.
internal static class PostgreSqlHarnessProcess
{
    internal const string Mode = "--sitefactory-postgresql-harness";
    internal const string ConfigurationMode = "--sitefactory-postgresql-harness-configuration";
    private static readonly string[] EnvironmentNames = ["PATH", "PATHEXT", "SystemRoot", "SystemDrive", "TEMP", "TMP",
        "DOTNET_ROOT", "DOTNET_ROOT_X64", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "NUGET_PACKAGES",
        "DOCKER_HOST", "DOCKER_CONTEXT", "DOCKER_CONFIG"];

    internal sealed record Request(string Directory, string Executable, string[] Arguments, int TimeoutSeconds,
        Dictionary<string, string?> Environment)
    {
        public override string ToString() => "PostgreSQL harness process request (redacted)";
    }

    internal sealed record Result(string Status, int ExitCode, string Output, string Error, double ElapsedMs,
        string Cleanup, string? Code)
    {
        public override string ToString() => "PostgreSQL harness process result (redacted)";
    }

    internal static async Task<Result> RunAsync(Request request, CancellationToken cancellationToken)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        try
        {
            if (request.TimeoutSeconds is < 1 or > 360 || !Path.IsPathFullyQualified(request.Directory)
                || request.Arguments.Length > 128 || request.Environment.Count > 32)
                throw new InvalidOperationException();
            ProcessStartInfo command = MigratorProcessRunner.CreateIsolatedStartInfo(request.Directory);
            command.FileName = request.Executable;
            foreach (string name in EnvironmentNames)
                if (System.Environment.GetEnvironmentVariable(name) is string value) command.Environment[name] = value;
            foreach (string argument in request.Arguments) command.ArgumentList.Add(argument);
            foreach ((string name, string? value) in request.Environment)
            {
                if (value is null) command.Environment.Remove(name);
                else command.Environment[name] = value;
            }
            command.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            command.Environment["VSLANG"] = "1033";
            CliBytesResult result = await OwnedProcessRunner.RunAsync(command, TimeSpan.FromSeconds(request.TimeoutSeconds), cancellationToken);
            // Streams are not an archive; the caller may inspect only bounded captured transport.
            if (result.StandardOutput.Length > 8 * 1024 * 1024 || result.StandardError.Length > 8 * 1024 * 1024)
                return new("Failed", result.ExitCode, "", "", elapsed.Elapsed.TotalMilliseconds, "Completed", "SF-TEST-PG-STREAM");
            return new("Completed", result.ExitCode, Encoding.UTF8.GetString(result.StandardOutput),
                Encoding.UTF8.GetString(result.StandardError), elapsed.Elapsed.TotalMilliseconds, "Completed", null);
        }
        catch (OperationCanceledException)
        {
            return new(cancellationToken.IsCancellationRequested ? "Cancelled" : "Timeout", -1, "", "",
                elapsed.Elapsed.TotalMilliseconds, "Completed", cancellationToken.IsCancellationRequested ? "SF-TEST-PG-CANCELLED" : "SF-TEST-PG-TIMEOUT");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException
            or System.ComponentModel.Win32Exception or ArgumentException)
        {
            // A failed cleanup must never be reported as a completed/green invocation.
            return new("Failed", -1, "", "", elapsed.Elapsed.TotalMilliseconds, "Failed", "SF-TEST-PG-PROCESS");
        }
    }

    internal static async Task<int> DispatchAsync()
    {
        try
        {
            // The caller keeps stdin open. EOF (including caller death) cancels the owned family.
            using StreamReader input = new(Console.OpenStandardInput(), Encoding.UTF8);
            using CancellationTokenSource handshake = new(TimeSpan.FromSeconds(10));
            string? line = await input.ReadLineAsync(handshake.Token);
            if (line is null || line.Length > 1024 * 1024) return 125;
            Request request = JsonSerializer.Deserialize<Request>(line) ?? throw new InvalidOperationException();
            using CancellationTokenSource disconnect = new();
            Task<string?> stop = input.ReadLineAsync();
            Task<Result> run = RunAsync(request, disconnect.Token);
            if (await Task.WhenAny(run, stop) == stop) await disconnect.CancelAsync();
            Result result = await run;
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(result));
            await Console.Out.FlushAsync();
            return 0;
        }
        catch (Exception) { return 125; } // Transport boundary only: never print request/provider details.
    }

    internal static async Task<int> ValidateConfigurationAsync()
    {
        try
        {
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
            PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(deadline.Token);
            await using NpgsqlConnection connection = configuration.Connection(PostgreSqlTestConfiguration.AdministrativeDatabase);
            await connection.OpenAsync(deadline.Token); // The existing parser requires VerifyFull, loopback and a dedicated login.
            await using NpgsqlCommand command = new("SELECT pg_catalog.current_setting('server_version_num') = '180006' AND ssl FROM pg_catalog.pg_stat_ssl WHERE pid = pg_catalog.pg_backend_pid()", connection);
            command.CommandTimeout = 3;
            if (await command.ExecuteScalarAsync(deadline.Token) is not true) return 1;
            await Console.Out.WriteLineAsync("SF-TEST-PG-CONFIG Verified temporary PostgreSQL 18.6 and TLS.");
            return 0;
        }
        catch (Exception) { return 1; } // Disposable configuration preflight, fixed result without exception output.
    }
}
