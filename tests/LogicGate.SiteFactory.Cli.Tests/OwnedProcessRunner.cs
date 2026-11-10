using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Cli.Tests;

internal static class OwnedProcessRunner
{
    internal const string SupervisorMode = "--sitefactory-owned-process";

    internal static async Task<CliBytesResult> RunAsync(ProcessStartInfo command, TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        string id = "sf-process-" + Guid.NewGuid().ToString("N");
        using SafeFileHandle? job = OperatingSystem.IsWindows() ? OwnedProcessScope.CreateJob(id) : null;
        await using NamedPipeServerStream control = new(id, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { typeof(OwnedProcessRunner).Assembly.Location, SupervisorMode, id })
            start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach ((string key, string? value) in command.Environment)
            start.Environment[key] = value;
        using Process supervisor = new() { StartInfo = start };
        if (!supervisor.Start())
            throw new InvalidOperationException("Test process supervisor did not start.");
        supervisor.StandardInput.Close();
        using MemoryStream stdout = new();
        using MemoryStream stderr = new();
        // Cleanup must survive caller cancellation, but has its own independent, bounded deadline.
        using CancellationTokenSource cleanup = new();
        Task output = supervisor.StandardOutput.BaseStream.CopyToAsync(stdout, cleanup.Token);
        Task error = supervisor.StandardError.BaseStream.CopyToAsync(stderr, cleanup.Token);
        bool enteredScope = false;
        int exitCode;
        try
        {
            await control.WaitForConnectionAsync(timeout.Token);
            using StreamReader reader = new(control, leaveOpen: true);
            using StreamWriter writer = new(control, leaveOpen: true) { AutoFlush = true };
            if (await reader.ReadLineAsync(timeout.Token) != "ready")
                throw new InvalidOperationException("Test process isolation failed before command launch.");
            enteredScope = true;
            // Send arguments privately, never include them in cleanup diagnostics.
            string request = JsonSerializer.Serialize(new Command(command.FileName, command.ArgumentList.ToArray()));
            await writer.WriteLineAsync(request.AsMemory(), timeout.Token);
            string? response = await reader.ReadLineAsync(timeout.Token);
            if (!int.TryParse(response, CultureInfo.InvariantCulture, out exitCode))
                throw new InvalidOperationException("Test process supervisor did not report a command result.");
        }
        finally
        {
            // A child may hold inherited output pipes open: clean the scope BEFORE waiting for EOF.
            await CleanupAsync();
            await Task.WhenAll(output, error).WaitAsync(cleanup.Token);
        }
        return new(exitCode, stdout.ToArray(), stderr.ToArray());

        async Task CleanupAsync()
        {
            cleanup.CancelAfter(OwnedProcessScope.CleanupTimeout);
            await control.DisposeAsync(); // EOF asks the Linux subreaper to kill/reap its adopted children.
            if (job is not null)
                await OwnedProcessScope.TerminateJobAsync(job);
            if (!enteredScope && !supervisor.HasExited)
                supervisor.Kill(); // No command was sent before the isolation handshake.
            try
            {
                await supervisor.WaitForExitAsync(cleanup.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Owned test process cleanup exceeded its deadline (supervisor PID {supervisor.Id}).");
            }
            if (job is null && enteredScope && supervisor.ExitCode != 0)
                throw new InvalidOperationException($"Owned test process cleanup failed (supervisor PID {supervisor.Id}).");
        }
    }

    internal static async Task<int> SuperviseAsync(string id)
    {
        bool isolated = false;
        try
        {
            OwnedProcessScope.Enter(id);
            isolated = true;
            using NamedPipeClientStream control = new(".", id, PipeDirection.InOut, PipeOptions.Asynchronous);
            await control.ConnectAsync(10000);
            using StreamReader reader = new(control, leaveOpen: true);
            using StreamWriter writer = new(control, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync("ready");
            string? request = await reader.ReadLineAsync();
            if (request is null)
                return 0;
            Command command = JsonSerializer.Deserialize<Command>(request)!;
            ProcessStartInfo start = new(command.FileName) { UseShellExecute = false, RedirectStandardInput = true };
            foreach (string argument in command.Arguments)
                start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException();
            process.StandardInput.Close();
            try
            {
                Task exit = process.WaitForExitAsync();
                Task<string?> stop = reader.ReadLineAsync();
                if (await Task.WhenAny(exit, stop) == exit)
                {
                    await exit;
                    await writer.WriteLineAsync(process.ExitCode.ToString(CultureInfo.InvariantCulture));
                }
                await stop;
            }
            catch (IOException)
            {
                // The caller may cancel exactly as the command exits; a disconnected control pipe
                // requests cleanup, not a failed cleanup or a replacement for caller cancellation.
            }
            finally
            {
                if (OperatingSystem.IsLinux())
                    await OwnedProcessScope.ReapLinuxChildrenAsync(process);
                // Windows caller terminates and awaits the whole job, including this supervisor.
            }
            return 0;
        }
        catch (Exception)
        {
            // Protocol exit only. No command arguments, provider errors or user output in diagnostics.
            if (isolated && OperatingSystem.IsLinux())
                OwnedProcessScope.AbortLinuxScope();
            return 125;
        }
    }

    private sealed record Command(string FileName, string[] Arguments);
}
