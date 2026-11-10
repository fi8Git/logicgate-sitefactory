using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

// Test-only, opt-in measurements. Never record arguments, output, SQL, paths or exceptions.
internal static class PostgreSqlTestMetrics
{
    internal const string DirectoryVariable = "SITEFACTORY_TEST_POSTGRES_METRICS_DIRECTORY";
    private static readonly AsyncLocal<string?> CurrentTest = new();
    private static readonly object Gate = new();

    internal enum Phase { Test, Apply, Bootstrap }

    internal static IDisposable Test([CallerMemberName] string name = "")
    {
        if (!IsAllowedTestName(name))
            throw new ArgumentException("Only fixed G04, P01 or P02 scenario names can be measured.", nameof(name));
        CurrentTest.Value = name;
        return Measure(Phase.Test);
    }

    internal static bool IsAllowedTestName(string name) =>
        PostgreSqlTestGate.ExpectedTests.Concat(PostgreSqlTestGate.ExpectedP01Tests).Concat(PostgreSqlTestGate.ExpectedP02Tests)
            .Any(test => test.EndsWith("." + name, StringComparison.Ordinal));

    internal static IDisposable Measure(Phase phase)
    {
        if (!Enum.IsDefined(phase)) throw new ArgumentException("Only fixed G04 phases can be measured.", nameof(phase));
        return new Measurement(CurrentTest.Value, Environment.GetEnvironmentVariable(DirectoryVariable), phase);
    }

    internal static IDisposable Process(string[] arguments)
    {
        Phase? phase = arguments.FirstOrDefault() switch
        {
            "apply" => Phase.Apply,
            "bootstrap" => Phase.Bootstrap,
            _ => null,
        };
        return new Measurement(CurrentTest.Value, phase.HasValue ? Environment.GetEnvironmentVariable(DirectoryVariable) : null, phase ?? Phase.Test);
    }

    private sealed class Measurement : IDisposable
    {
        private readonly string? file;
        private readonly Phase phase;
        private readonly long start = Stopwatch.GetTimestamp();
        private bool disposed;

        internal Measurement(string? name, string? directory, Phase phase)
        {
            this.phase = phase;
            if (name is null || string.IsNullOrWhiteSpace(directory)) return;
            file = Path.Combine(directory, name + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".metrics");
            Write("Start", 0);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Write("End", (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        private void Write(string state, long milliseconds)
        {
            if (file is null) return;
            string row = string.Create(CultureInfo.InvariantCulture,
                $"{phase}|{state}|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}|{milliseconds}\n");
            lock (Gate) File.AppendAllText(file, row);
        }
    }
}
