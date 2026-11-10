using System.Diagnostics;
using System.Text;

namespace LogicGate.SiteFactory.Cli.Tests;

internal static class CliProcessRunner
{
    public static async Task<CliResult> RunAsync(string workingDirectory, string[] args, CancellationToken cancellationToken)
    {
        CliBytesResult result = await RunBytesAsync(workingDirectory, args, cancellationToken);
        return new(result.ExitCode, NormalizeLines(Encoding.UTF8.GetString(result.StandardOutput)),
            NormalizeLines(Encoding.UTF8.GetString(result.StandardError)));
    }

    // No decoding or newline normalization: plan tests must verify the actual console bytes.
    public static Task<CliBytesResult> RunBytesAsync(
        string workingDirectory, string[] args, CancellationToken cancellationToken, string? cliAssemblyPath = null)
        => RunDotnetAsync(workingDirectory, [cliAssemblyPath ?? typeof(SiteFactoryCli).Assembly.Location, .. args],
            TimeSpan.FromSeconds(30), cancellationToken);

    internal static async Task<CliBytesResult> RunDotnetAsync(
        string workingDirectory, string[] args, TimeSpan timeoutDuration, CancellationToken cancellationToken,
        IReadOnlyList<string>? removeEnvironmentVariables = null, IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (string argument in args)
            startInfo.ArgumentList.Add(argument);
        if (removeEnvironmentVariables is not null)
        {
            foreach (string key in startInfo.Environment.Keys.Where(key =>
                removeEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray())
                startInfo.Environment.Remove(key);
        }
        if (environmentVariables is not null)
        {
            foreach ((string key, string value) in environmentVariables)
                startInfo.Environment[key] = value;
        }

        return await OwnedProcessRunner.RunAsync(startInfo, timeoutDuration, cancellationToken);
    }

    // Normalize only platform line endings; preserve all other output for exact assertions.
    private static string NormalizeLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed record CliBytesResult(int ExitCode, byte[] StandardOutput, byte[] StandardError);
