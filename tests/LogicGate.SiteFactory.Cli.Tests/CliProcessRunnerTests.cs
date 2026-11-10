using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

public sealed class CliProcessRunnerTests
{
    [Theory]
    [InlineData("exit")]
    [InlineData("cancellation")]
    [InlineData("timeout")]
    public async Task OwnedTreeIsCleanedWithoutTouchingAnUnrelatedProcess(string completion)
    {
        string directory = Directory.CreateTempSubdirectory("sitefactory-process-cleanup-").FullName;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(40));
        await using FixtureConnection parent = new();
        await using FixtureConnection child = new();
        await using FixtureConnection witness = new();
        ProcessStartInfo witnessStart = new("dotnet") { UseShellExecute = false };
        foreach (string argument in new[] { typeof(TestProcessEntryPoint).Assembly.Location,
            TestProcessEntryPoint.FixtureMode, "child", witness.Name, Path.Combine(directory, "witness.txt") })
            witnessStart.ArgumentList.Add(argument);
        using Process witnessProcess = Process.Start(witnessStart)!;
        await witness.ConnectAsync(deadline.Token);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        try
        {
            Task<CliBytesResult> run = CliProcessRunner.RunDotnetAsync(directory,
                [typeof(TestProcessEntryPoint).Assembly.Location, TestProcessEntryPoint.FixtureMode,
                    "parent", parent.Name, child.Name, Path.Combine(directory, "held.txt")],
                TimeSpan.FromSeconds(completion == "timeout" ? 10 : 30), cancellation.Token);
            await parent.ConnectAsync(deadline.Token);
            await child.ConnectAsync(deadline.Token);
            Assert.False(child.Process!.HasExited);
            Assert.False(parent.Process!.HasExited);
            if (completion == "exit")
            {
                // Parent exit is acknowledged independently of stdout EOF: the descendant owns inherited handles.
                await parent.ReleaseAsync();
                await parent.Process.WaitForExitAsync(deadline.Token);
                Assert.Equal(23, (await run).ExitCode);
            }
            else
            {
                if (completion == "cancellation")
                    await cancellation.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
                // Unix Process handles for non-children are observed asynchronously by .NET.
                Assert.True(parent.Process.WaitForExit(3000), "The owned parent survived cleanup.");
            }
            Assert.True(child.Process.WaitForExit(3000), "The owned descendant survived its exited parent.");
            Assert.False(witnessProcess.HasExited);
            await witness.ReleaseAsync();
            await witnessProcess.WaitForExitAsync(deadline.Token);
            Directory.Delete(directory, recursive: true);
            Assert.False(Directory.Exists(directory));
        }
        finally
        {
            await child.DisposeAsync();
            await parent.DisposeAsync();
            await witness.DisposeAsync();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AlreadyCancelledInvocationDoesNotStartAProcess()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CliProcessRunner.RunDotnetAsync(
            Path.GetTempPath(), ["--version"], TimeSpan.FromSeconds(30), cancelled.Token));
    }

    [Fact]
    public async Task CommandOutputExitCodeAndEnvironmentArePreserved()
    {
        CliBytesResult result = await CliProcessRunner.RunDotnetAsync(Path.GetTempPath(),
            [typeof(TestProcessEntryPoint).Assembly.Location, TestProcessEntryPoint.FixtureMode, "output"],
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken,
            environmentVariables: new Dictionary<string, string> { ["SF_TEST_PROCESS_VALUE"] = "value" });
        Assert.Equal(23, result.ExitCode);
        Assert.Equal("été\nvalue\n", System.Text.Encoding.UTF8.GetString(result.StandardOutput));
        Assert.Equal("error\n", System.Text.Encoding.UTF8.GetString(result.StandardError));
    }

    private sealed class FixtureConnection : IAsyncDisposable
    {
        public string Name { get; } = "sf-fixture-" + Guid.NewGuid().ToString("N");
        private readonly NamedPipeServerStream pipe;
        private StreamWriter? writer;
        public Process? Process { get; private set; }

        public FixtureConnection() => pipe = new(Name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        public async Task ConnectAsync(CancellationToken token)
        {
            await pipe.WaitForConnectionAsync(token);
            using StreamReader reader = new(pipe, leaveOpen: true);
            int pid = int.Parse((await reader.ReadLineAsync(token))!, CultureInfo.InvariantCulture);
            Process = System.Diagnostics.Process.GetProcessById(pid);
            // The controlled fixture cannot exit until this connection is released.
            _ = Process.SafeHandle;
            writer = new(pipe, leaveOpen: true) { AutoFlush = true };
        }

        public async Task ReleaseAsync()
        {
            if (writer is not null)
            {
                try { await writer.WriteLineAsync("release"); }
                catch (IOException) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
            if (Process is not null)
            {
                // Release through the owned control channel, never signal a potentially recycled PID.
                Assert.True(Process.WaitForExit(5000), "Fixture cleanup failed.");
                Process.Dispose();
                Process = null;
            }
            writer = null;
            await pipe.DisposeAsync();
        }
    }
}
