using System.Diagnostics;
using System.Text;
using LogicGate.SiteFactory.Cli.Tests;
using LogicGate.SiteFactory.Migrator;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql
{
    internal sealed record MigratorProcessResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);

    // Only the current build, file references and a closed environment. Never a generated PME host.
    internal static class MigratorProcessRunner
    {
        internal static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);
        internal const string ConnectionVariable = "SITEFACTORY_MIGRATOR_CONNECTION_FILE";
        private static readonly string[] AllowedEnvironment =
            ["PATH", "PATHEXT", "SystemRoot", "SystemDrive", "TEMP", "TMP", "DOTNET_ROOT", "DOTNET_ROOT_X64"];
        private static readonly string[] Diagnostics =
        [
            "SF-BOOTSTRAP-USAGE-001 Explicit bootstrap with a valid identity in Development or Test is required.",
            "SF-MIGRATION-USAGE-001 Explicit apply in Development or Test is required.",
            "SF-MIGRATION-CONFIG-001 The dedicated migration connection file is invalid or unreadable.",
            "SF-MIGRATION-MODEL-001 The packaged migration does not match the expected model.",
            "SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required.",
            "SF-MIGRATION-HISTORY-001 The database contains an unsupported migration history.",
            "SF-BOOTSTRAP-CANCELLED-001 Bootstrap cancelled; inspect database state before retrying.",
            "SF-MIGRATION-CANCELLED-001 Migration cancelled; inspect database state before retrying.",
            "SF-BOOTSTRAP-FAILED-001 Bootstrap failed; inspect database state before retrying.",
            "SF-MIGRATION-FAILED-001 Migration failed; inspect database state before retrying.",
            "SF-BOOTSTRAP-HISTORY-001 Apply the exact initial migration before bootstrap.",
            "SF-BOOTSTRAP-STATE-001 Existing runtime metadata is incomplete or incompatible; no repair was performed.",
            "SF-BOOTSTRAP-CLIENT-001 Existing database client does not match the requested identity.",
            "SF-BOOTSTRAP-ENVIRONMENT-001 Existing database environment does not match the requested identity.",
            "SF-BOOTSTRAP-INSTANCE-001 Existing database instance does not match the requested identity.",
        ];

        internal static async Task<MigratorProcessResult> RunAsync(string directory, string[] args, string? file,
            CancellationToken cancellationToken)
        {
            using IDisposable measurement = PostgreSqlTestMetrics.Process(args);
            CliBytesResult result = await RunOwnedAsync(CreateStartInfo(directory, args, file), ProcessTimeout, cancellationToken);
            return ValidateResult(result);
        }

        internal static ProcessStartInfo CreateStartInfo(string directory, string[] args, string? file)
        {
            string assembly = typeof(MigrationCommand).Assembly.Location;
            if (!File.Exists(Path.ChangeExtension(assembly, ".runtimeconfig.json"))
                || !File.Exists(Path.ChangeExtension(assembly, ".deps.json")))
                throw new InvalidOperationException("SF-TEST-PG-PROCESS Current Migrator build dependencies are missing.");
            ProcessStartInfo command = CreateIsolatedStartInfo(directory);
            command.ArgumentList.Add(assembly);
            foreach (string argument in args) command.ArgumentList.Add(argument);
            if (file is not null) command.Environment[ConnectionVariable] = file;
            return command;
        }

        internal static ProcessStartInfo CreateIsolatedStartInfo(string directory)
        {
            ProcessStartInfo command = new("dotnet")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            command.Environment.Clear();
            foreach (string variable in AllowedEnvironment)
                if (Environment.GetEnvironmentVariable(variable) is string value) command.Environment[variable] = value;
            command.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            command.Environment["DOTNET_NOLOGO"] = "1";
            return command;
        }

        internal static async Task<CliBytesResult> RunOwnedAsync(ProcessStartInfo command, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            try
            {
                return await OwnedProcessRunner.RunAsync(command, timeout, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("SF-TEST-PG-PROCESS Owned process exceeded its deadline.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (exception is IOException or InvalidOperationException
                or System.ComponentModel.Win32Exception or TimeoutException)
            {
                // No raw process output, arguments or paths even on startup/cleanup failure.
                throw new InvalidOperationException("SF-TEST-PG-PROCESS Owned process launch or cleanup failed.");
            }
        }

        internal static MigratorProcessResult ValidateResult(CliBytesResult result)
        {
            string output = Encoding.UTF8.GetString(result.StandardOutput);
            string error = Encoding.UTF8.GetString(result.StandardError);
            bool success = result.ExitCode == 0 && error.Length == 0
                && (output == "Runtime metadata migration completed." + Environment.NewLine
                    || output == "Runtime metadata bootstrap completed." + Environment.NewLine
                    || output == "LogicGate.SiteFactory.Migrator apply --environment <Development|Test> --confirm-migration" + Environment.NewLine
                        + "LogicGate.SiteFactory.Migrator bootstrap --environment <Development|Test> --client-slug <slug> --database-instance-id <uuid> --operation-id <uuid> --confirm-bootstrap" + Environment.NewLine);
            bool failure = result.ExitCode is 1 or 2 or 130 && output.Length == 0
                && Diagnostics.Any(diagnostic => error == diagnostic + Environment.NewLine);
            if (!success && !failure)
                throw new InvalidOperationException("SF-TEST-PG-PROCESS Unexpected command output withheld.");
            return new(result.ExitCode, result.StandardOutput, result.StandardError);
        }
    }
}

namespace LogicGate.SiteFactory.Cli.Tests
{
    // Local adapters required by the linked BCL supervisor; no reference to the CLI project.
    internal sealed record CliBytesResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);

    internal static class G04TestEntryPoint
    {
        internal const string FixtureMode = "--sitefactory-g04-process-fixture";

        public static int Main(string[] args)
        {
            if (args is [global::LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlHarnessProcess.Mode])
                return global::LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlHarnessProcess.DispatchAsync().GetAwaiter().GetResult();
            if (args is [global::LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlHarnessProcess.ConfigurationMode])
                return global::LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlHarnessProcess.ValidateConfigurationAsync().GetAwaiter().GetResult();
            if (args is [OwnedProcessRunner.SupervisorMode, var id])
                return OwnedProcessRunner.SuperviseAsync(id).GetAwaiter().GetResult();
            if (args is [FixtureMode, var mode, var pipe, ..])
                return RunFixtureAsync(mode, pipe, args).GetAwaiter().GetResult();
            return global::LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.AutoGenerated.XunitAutoGeneratedEntryPoint.Main(args);
        }

        private static async Task<int> RunFixtureAsync(string mode, string pipe, string[] args)
        {
            using System.IO.Pipes.NamedPipeClientStream control = new(".", pipe,
                System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
            await control.ConnectAsync(10000);
            using StreamWriter writer = new(control, leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(control, leaveOpen: true);
            using FileStream? heldFile = mode == "child"
                ? new(args[3], FileMode.CreateNew, FileAccess.Write, FileShare.None) : null;
            await writer.WriteLineAsync(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (mode == "parent")
            {
                ProcessStartInfo child = new("dotnet") { UseShellExecute = false };
                foreach (string argument in new[] { typeof(G04TestEntryPoint).Assembly.Location,
                    FixtureMode, "child", args[3], args[4] }) child.ArgumentList.Add(argument);
                using Process descendant = Process.Start(child) ?? throw new InvalidOperationException("Fixture did not start.");
                await reader.ReadLineAsync();
                return 0;
            }
            await reader.ReadLineAsync();
            return 0;
        }
    }
}
