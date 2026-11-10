using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using LogicGate.SiteFactory.Cli.Tests;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

public sealed class MigratorProcessRunnerTests
{
    [Fact]
    public async Task CurrentMigratorBuildRunsWithoutPmeGenerationOrDatabaseConnection()
    {
        MigratorProcessResult result = await MigratorProcessRunner.RunAsync(AppContext.BaseDirectory,
            ["--help"], null, TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.StartsWith("LogicGate.SiteFactory.Migrator apply", Encoding.UTF8.GetString(result.StandardOutput), StringComparison.Ordinal);
    }

    [Fact]
    public void CommandEnvironmentContainsOnlyTheAllowlistAndASecretFileReference()
    {
        ProcessStartInfo command = MigratorProcessRunner.CreateStartInfo(AppContext.BaseDirectory, ["--help"], "connection-file");
        string[] allowed = ["PATH", "PATHEXT", "SystemRoot", "SystemDrive", "TEMP", "TMP", "DOTNET_ROOT", "DOTNET_ROOT_X64",
            "DOTNET_CLI_UI_LANGUAGE", "DOTNET_NOLOGO", MigratorProcessRunner.ConnectionVariable];
        Assert.All(command.Environment.Keys, key => Assert.Contains(key, allowed, StringComparer.OrdinalIgnoreCase));
        Assert.Equal("connection-file", command.Environment[MigratorProcessRunner.ConnectionVariable]);
        Assert.DoesNotContain("connection-file", command.ArgumentList);
        Assert.DoesNotContain(command.Environment.Keys, static key => key.Contains("WEB", StringComparison.OrdinalIgnoreCase)
            || key.Contains("WORKER", StringComparison.OrdinalIgnoreCase) || key.Contains("ADMIN", StringComparison.OrdinalIgnoreCase)
            || key == "DOTNET_STARTUP_HOOKS");
    }

    [Theory]
    [InlineData(0, "DO_NOT_DISCLOSE", "")]
    [InlineData(1, "", "DO_NOT_DISCLOSE")]
    [InlineData(42, "", "")]
    public void UnexpectedRawProcessOutputIsWithheld(int exitCode, string output, string error)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => MigratorProcessRunner.ValidateResult(
            new(exitCode, Encoding.UTF8.GetBytes(output), Encoding.UTF8.GetBytes(error))));
        Assert.Equal("SF-TEST-PG-PROCESS Unexpected command output withheld.", exception.Message);
        Assert.DoesNotContain("DO_NOT_DISCLOSE", exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task PreCancelledCommandDoesNotLaunchAProcess()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MigratorProcessRunner.RunOwnedAsync(
            MigratorProcessRunner.CreateIsolatedStartInfo(AppContext.BaseDirectory), TimeSpan.FromSeconds(10), cancellation.Token));
    }

    [Theory]
    [InlineData("parent-exit")]
    [InlineData("timeout")]
    [InlineData("cancellation")]
    public async Task OwnedFamilyIsCleanedIncludingSurvivingDescendant(string outcome)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using CancellationTokenSource handshake = CancellationTokenSource.CreateLinkedTokenSource(token);
        handshake.CancelAfter(TimeSpan.FromSeconds(10));
        string directory = Directory.CreateTempSubdirectory("sitefactory-g04-owned-").FullName;
        string parentPipe = "sf-g04-parent-" + Guid.NewGuid().ToString("N");
        string childPipe = "sf-g04-child-" + Guid.NewGuid().ToString("N");
        string heldFile = Path.Combine(directory, "held-file");
        using NamedPipeServerStream parent = new(parentPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using NamedPipeServerStream child = new(childPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        ProcessStartInfo command = MigratorProcessRunner.CreateIsolatedStartInfo(directory);
        foreach (string argument in new[] { typeof(G04TestEntryPoint).Assembly.Location, G04TestEntryPoint.FixtureMode,
            "parent", parentPipe, childPipe, heldFile }) command.ArgumentList.Add(argument);
        Task<CliBytesResult> run = MigratorProcessRunner.RunOwnedAsync(command,
            TimeSpan.FromSeconds(outcome == "timeout" ? 6 : 20), cancellation.Token);
        try
        {
            await parent.WaitForConnectionAsync(handshake.Token);
            using StreamReader parentReader = new(parent, leaveOpen: true);
            int parentId = int.Parse((await parentReader.ReadLineAsync(handshake.Token))!, CultureInfo.InvariantCulture);
            await child.WaitForConnectionAsync(handshake.Token);
            using StreamReader childReader = new(child, leaveOpen: true);
            int childId = int.Parse((await childReader.ReadLineAsync(handshake.Token))!, CultureInfo.InvariantCulture);
            Assert.True(File.Exists(heldFile));
            if (outcome == "parent-exit")
            {
                using StreamWriter control = new(parent, leaveOpen: true) { AutoFlush = true };
                await control.WriteLineAsync("exit");
                CliBytesResult result = await run.WaitAsync(TimeSpan.FromSeconds(15), token);
                Assert.Equal(0, result.ExitCode);
            }
            else if (outcome == "cancellation")
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(15), token));
            }
            else
            {
                await Assert.ThrowsAsync<TimeoutException>(() => run.WaitAsync(TimeSpan.FromSeconds(15), token));
            }
            Assert.False(IsAlive(parentId));
            Assert.False(IsAlive(childId));
            File.Delete(heldFile);
            Assert.False(File.Exists(heldFile));
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(15), token); }
            catch (Exception exception) when (exception is OperationCanceledException or TimeoutException) { }
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(directory));
            Assert.StartsWith("sitefactory-g04-owned-", Path.GetFileName(directory), StringComparison.Ordinal);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool IsAlive(int id)
    {
        try { using Process process = Process.GetProcessById(id); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
}
