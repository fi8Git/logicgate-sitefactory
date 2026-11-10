using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using LogicGate.SiteFactory.Cli.Tests;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

// Environment opt-in is process-global: do not run metrics mutations beside other test collections.
[CollectionDefinition("P01 diagnostics environment", DisableParallelization = true)]
public sealed class PostgreSqlHarnessDiagnosticsEnvironment;

// Offline only. The copied script and same-assembly BCL supervisor need no private CLI or template.
[Collection("P01 diagnostics environment")]
public sealed class PostgreSqlHarnessDiagnosticsTests
{
    [Fact]
    public async Task TimeoutReportsOnlyAllowlistedResultsAndStillFailsWhenReturnValueIsAssigned()
    {
        await AssertOwnedTimeoutAsync(allowFailure: true, cancel: false);
        await AssertOwnedTimeoutAsync(allowFailure: false, cancel: false);
        await AssertOwnedTimeoutAsync(allowFailure: true, cancel: true);
    }

    [Fact]
    public async Task SummaryNeverEchoesRawOutputAndDoesNotTurnMissingOrConflictingResultsIntoSuccess()
    {
        DiagnosticResult result = await RunAsync($$"""
            $outputText = @'
              Passed Tests.Sample.Completed [1 ms]
              Failed Tests.Sample.FailedCase [SENSITIVE_TEST_MARKER]
              Skipped Tests.Sample.SkippedCase [1 ms]
              Passed Tests.Sample.Conflicting [1 ms]
              Failed Tests.Sample.Conflicting [1 ms]
              Passed Tests.Sample.SENSITIVE_TEST_MARKER [1 ms]
              Passed Tests.Sample.Pending(arguments: "SENSITIVE_TEST_MARKER") [1 ms]
              Passed Tests.Sample.PendingSuffix [1 ms]
              Passed Tests.Sample.Pending [1 ms] SENSITIVE_TEST_MARKER
              Passed Tests.Sample.Pending [truncated
            {{SensitiveConnection()}}
            System.Exception: SENSITIVE_TEST_MARKER
            ::error::SENSITIVE_TEST_MARKER
            '@
            Write-TestProgress $outputText @('Completed', 'FailedCase', 'SkippedCase', 'Conflicting', 'Pending')
            Write-TestProgress '' @('Pending')
            """);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Equal(Progress("Completed Passed", "FailedCase Failed", "SkippedCase Skipped", "Conflicting ConflictingResults", "Pending NotObserved")
            + Progress("Pending NotObserved"), result.StandardOutput);
        Assert.DoesNotContain("SENSITIVE_TEST_MARKER", result.StandardOutput + result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonzeroExitIsPreservedAndDoesNotLeakCapturedOutput()
    {
        DiagnosticResult result = await RunAsync("""
            $arguments = @('-NoProfile', '-NonInteractive', '-Command', "[Console]::WriteLine('SENSITIVE_TEST_MARKER'); [Console]::Error.WriteLine('SENSITIVE_TEST_MARKER'); exit 7")
            $result = Invoke-TestProcess 'pwsh' $arguments -AllowFailure
            if ($result.Code -ne 7) { throw 'Exit code lost.' }
            try {
                $result = Invoke-TestProcess 'pwsh' $arguments
                throw 'Nonzero exit ignored.'
            }
            catch {
                if ($_.Exception.Message -cne 'SF-TEST-PG-PROCESS pwsh failed with exit 7.') { throw 'Unexpected test failure.' }
            }
            exit 0
            """);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task SuccessfulProcessKeepsItsResultWithoutPrintingCapturedStreams()
    {
        DiagnosticResult result = await RunAsync("""
            $result = Invoke-TestProcess 'pwsh' @('-NoProfile', '-NonInteractive', '-Command', "[Console]::WriteLine('captured-output'); [Console]::Error.WriteLine('captured-error')") -ExpectedTests @('Pending')
            if ($result.Code -ne 0 -or $result.Output.Trim() -cne 'captured-output' -or $result.Error.Trim() -cne 'captured-error') {
                throw 'Process result changed.'
            }
            """);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void MetricsEmissionIsOptInBoundedRedactedAndIdempotentForG04P01AndP02Only()
    {
        string directory = Directory.CreateTempSubdirectory("sitefactory-p01-diagnostics-").FullName;
        string? previous = Environment.GetEnvironmentVariable(PostgreSqlTestMetrics.DirectoryVariable);
        string[] g04 = PostgreSqlTestGate.ExpectedTests.Select(MethodName).ToArray();
        string[] p01 = PostgreSqlTestGate.ExpectedP01Tests.Select(MethodName).ToArray();
        string[] p02 = PostgreSqlTestGate.ExpectedP02Tests.Select(MethodName).ToArray();
        try
        {
            Environment.SetEnvironmentVariable(PostgreSqlTestMetrics.DirectoryVariable, null);
            IDisposable disabled = PostgreSqlTestMetrics.Test(p01[0]);
            disabled.Dispose();
            disabled.Dispose();
            Assert.Empty(Directory.GetFiles(directory));
            Environment.SetEnvironmentVariable(PostgreSqlTestMetrics.DirectoryVariable, directory);
            Assert.Equal(["Test", "Apply", "Bootstrap"], Enum.GetNames<PostgreSqlTestMetrics.Phase>());
            foreach (string name in g04.Concat(p01).Concat(p02))
            {
                IDisposable measurement = PostgreSqlTestMetrics.Test(name);
                if (name == g04[0] || name == p02.Single(static name => name.StartsWith("GeneratedMetadataPermissions", StringComparison.Ordinal)))
                {
                    foreach (string action in new[] { "apply", "bootstrap" })
                    {
                        IDisposable phase = PostgreSqlTestMetrics.Process([action, "SENSITIVE_TEST_MARKER"]);
                        phase.Dispose();
                        phase.Dispose();
                    }
                }
                measurement.Dispose();
                measurement.Dispose();
            }
            string[] p03 = ["RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent",
                "RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites", "RealWebReadinessHasNoFalseFailuresUnderConcurrentTraffic"];
            foreach (string name in PostgreSqlTestGate.ExpectedG05Tests.Select(MethodName).Concat(p03).Append("SENSITIVE_TEST_MARKER"))
                Assert.Throws<ArgumentException>(() => PostgreSqlTestMetrics.Test(name));
            string[] files = Directory.GetFiles(directory);
            Assert.Equal(g04.Concat(p01).Concat(p02).Select(name => name + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".metrics")
                .Order(StringComparer.Ordinal), files.Select(Path.GetFileName).Order(StringComparer.Ordinal));
            foreach (string file in files)
            {
                string name = Path.GetFileName(file).Split('.')[0];
                string[] rows = File.ReadAllLines(file);
                string[] phases = name == g04[0] || name.StartsWith("GeneratedMetadataPermissions", StringComparison.Ordinal)
                    ? ["Test", "Apply", "Bootstrap"] : ["Test"];
                Assert.Equal(phases.Length * 2, rows.Length);
                foreach (string phase in phases)
                {
                    string start = Assert.Single(rows, row => row.StartsWith(phase + "|Start|", StringComparison.Ordinal));
                    string end = Assert.Single(rows, row => row.StartsWith(phase + "|End|", StringComparison.Ordinal));
                    Assert.All(new[] { start, end }, row =>
                    {
                        Assert.Matches(@"\A(Test|Apply|Bootstrap)\|(Start|End)\|[0-9]{1,16}\|[0-9]{1,8}\z", row);
                        string[] fields = row.Split('|');
                        Assert.InRange(long.Parse(fields[2], CultureInfo.InvariantCulture), 0, 9_999_999_999_999_999L);
                        Assert.InRange(long.Parse(fields[3], CultureInfo.InvariantCulture), 0, 99_999_999L);
                        Assert.DoesNotContain("SENSITIVE_TEST_MARKER", row, StringComparison.Ordinal);
                    });
                    Assert.Equal("0", start.Split('|')[3]);
                }
                if (p01.Contains(name, StringComparer.Ordinal))
                    Assert.All(rows, row => Assert.StartsWith("Test|", row, StringComparison.Ordinal));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(PostgreSqlTestMetrics.DirectoryVariable, previous);
            DeleteOwnedDirectory(directory);
        }
    }

    private static async Task AssertOwnedTimeoutAsync(bool allowFailure, bool cancel)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using CancellationTokenSource handshake = CancellationTokenSource.CreateLinkedTokenSource(token);
        handshake.CancelAfter(TimeSpan.FromSeconds(10));
        string directory = Directory.CreateTempSubdirectory("sitefactory-p01-diagnostics-").FullName;
        string parentPipe = "sf-p01-parent-" + Guid.NewGuid().ToString("N");
        string childPipe = "sf-p01-child-" + Guid.NewGuid().ToString("N");
        string heldFile = Path.Combine(directory, "held-file");
        string processFile = Path.Combine(directory, "pwsh-pid");
        using NamedPipeServerStream parent = new(parentPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using NamedPipeServerStream child = new(childPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        string script = $$"""
            $child = @'
            [IO.File]::WriteAllText({{Quote(processFile)}}, $PID.ToString([Globalization.CultureInfo]::InvariantCulture))
            [Console]::WriteLine('  Passed Tests.Sample.Completed [1 ms]')
            [Console]::WriteLine({{Quote(SensitiveConnection())}})
            [Console]::Error.WriteLine('SENSITIVE_TEST_MARKER')
            [Console]::Out.Flush()
            $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            foreach ($argument in @({{Quote(typeof(G04TestEntryPoint).Assembly.Location)}}, {{Quote(G04TestEntryPoint.FixtureMode)}}, 'parent', '{{parentPipe}}', '{{childPipe}}', {{Quote(heldFile)}})) {
                $start.ArgumentList.Add($argument)
            }
            $descendant = [Diagnostics.Process]::Start($start)
            try { Start-Sleep -Seconds 60 } finally { $descendant.Dispose() }
            '@
            $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($child))
            try {
                $result = Invoke-TestProcess 'pwsh' @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded, '-OutputFormat', 'Text') -TimeoutSeconds 5 {{(allowFailure ? "-AllowFailure" : "")}} -ExpectedTests @('Completed', 'Pending')
                exit 0
            }
            catch {
                if ($_.Exception.Message -cne 'SF-TEST-PG-PROCESS Process timeout.') { throw 'Unexpected test failure.' }
                [Console]::Error.WriteLine('SF-TEST-PG-PROCESS Process timeout.')
                exit 1
            }
            """;
        Task<DiagnosticResult> run = RunInDirectoryAsync(script, directory, cancellation.Token);
        try
        {
            await parent.WaitForConnectionAsync(handshake.Token);
            using StreamReader parentReader = new(parent, leaveOpen: true);
            int parentId = int.Parse((await parentReader.ReadLineAsync(handshake.Token))!, CultureInfo.InvariantCulture);
            await child.WaitForConnectionAsync(handshake.Token);
            using StreamReader childReader = new(child, leaveOpen: true);
            int childId = int.Parse((await childReader.ReadLineAsync(handshake.Token))!, CultureInfo.InvariantCulture);
            int pwshId = int.Parse(await File.ReadAllTextAsync(processFile, handshake.Token), CultureInfo.InvariantCulture);
            Assert.True(File.Exists(heldFile));
            Assert.All(new[] { pwshId, parentId, childId }, id => Assert.True(IsAlive(id)));
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(15), token));
            }
            else
            {
                DiagnosticResult result = await run.WaitAsync(TimeSpan.FromSeconds(15), token);
                Assert.Equal(1, result.ExitCode);
                Assert.Equal("SF-TEST-PG-PROCESS Process timeout.\n", result.StandardError);
                Assert.Equal(Progress("Completed Passed", "Pending NotObserved"), result.StandardOutput);
                Assert.DoesNotContain("SENSITIVE_TEST_MARKER", result.StandardOutput + result.StandardError, StringComparison.Ordinal);
            }
            Assert.All(new[] { pwshId, parentId, childId }, id => Assert.False(IsAlive(id)));
            File.Delete(heldFile);
            Assert.False(File.Exists(heldFile));
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (OperationCanceledException) { }
            DeleteOwnedDirectory(directory);
        }
    }

    private static string Progress(params string[] results) =>
        "SF-TEST-PG-PROGRESS Observed console results (not a completed TRX proof):\n"
        + string.Concat(results.Select(result => "SF-TEST-PG-PROGRESS " + result + "\n"))
        + "SF-TEST-PG-PROGRESS NotObserved does not distinguish queued, running or unreported tests.\n";

    private static async Task<DiagnosticResult> RunAsync(string test)
    {
        string directory = Directory.CreateTempSubdirectory("sitefactory-p01-diagnostics-").FullName;
        try { return await RunInDirectoryAsync(test, directory, TestContext.Current.CancellationToken); }
        finally { DeleteOwnedDirectory(directory); }
    }

    private static async Task<DiagnosticResult> RunInDirectoryAsync(string test, string directory, CancellationToken token)
    {
        string script = Path.Combine(AppContext.BaseDirectory, "PostgreSql", "PostgreSqlHarnessDiagnostics.ps1");
        Assert.True(File.Exists(script));
        ProcessStartInfo start = MigratorProcessRunner.CreateIsolatedStartInfo(directory);
        start.FileName = "pwsh";
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(". " + Quote(script) + "\n" + test)), "-OutputFormat", "Text" })
            start.ArgumentList.Add(argument);
        CliBytesResult result = await MigratorProcessRunner.RunOwnedAsync(start, TimeSpan.FromSeconds(30), token);
        return new(result.ExitCode, Encoding.UTF8.GetString(result.StandardOutput).Replace("\r\n", "\n", StringComparison.Ordinal),
            Encoding.UTF8.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static string SensitiveConnection() => new DbConnectionStringBuilder
    {
        ["Host"] = "SENSITIVE_TEST_MARKER",
        ["Password"] = "SENSITIVE_TEST_MARKER",
    }.ConnectionString;

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string MethodName(string value) => value[(value.LastIndexOf('.') + 1)..];

    private static bool IsAlive(int id)
    {
        try { using Process process = Process.GetProcessById(id); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static void DeleteOwnedDirectory(string directory)
    {
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(directory));
        Assert.StartsWith("sitefactory-p01-diagnostics-", Path.GetFileName(directory), StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
        Assert.False(Directory.Exists(directory));
    }

    private sealed record DiagnosticResult(int ExitCode, string StandardOutput, string StandardError);
}
