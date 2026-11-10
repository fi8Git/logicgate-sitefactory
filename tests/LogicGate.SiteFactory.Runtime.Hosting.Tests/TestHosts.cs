using System.Collections.Concurrent;
using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Hosting.Tests;

internal sealed class TestHosts : IDisposable
{
    private const string DirectoryPrefix = "sitefactory-runtime-host-test-";
    private readonly List<IHost> hosts = [];
    private readonly CaptureLoggerProvider logs = new();

    public string DirectoryPath { get; } = Directory.CreateTempSubdirectory(DirectoryPrefix).FullName;
    public IReadOnlyList<string> LogMessages => logs.Messages;

    public Task WriteConfigurationAsync(string json, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(Path.Combine(DirectoryPath, "appsettings.json"), json, cancellationToken);

    public IHost Create(RuntimeComponent component, string environment, params string[] runtimeArguments)
    {
        string[] args = ["--contentRoot", DirectoryPath, "--environment", environment, .. runtimeArguments];
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ContentRootPath = DirectoryPath,
            EnvironmentName = environment,
        });
        // Explicit sources only: no ambient environment, user secrets, reload or working-directory dependency.
        builder.Configuration.SetBasePath(DirectoryPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddCommandLine(args);
        builder.Logging.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace);
        builder.Services.AddRuntimeStartupValidation(builder.Configuration, builder.Environment.EnvironmentName, component);
        IHost host = builder.Build();
        hosts.Add(host);
        return host;
    }

    public void Dispose()
    {
        try
        {
            foreach (IHost host in hosts.AsEnumerable().Reverse()) host.Dispose();
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar,
                DirectoryPath, StringComparison.Ordinal);
            Assert.StartsWith(DirectoryPrefix, Path.GetFileName(DirectoryPath), StringComparison.Ordinal);
            Directory.Delete(DirectoryPath, recursive: true);
            Assert.False(Directory.Exists(DirectoryPath));
        }
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();
        public IReadOnlyList<string> Messages => messages.ToArray();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception) + exception?.ToString());
    }
}
