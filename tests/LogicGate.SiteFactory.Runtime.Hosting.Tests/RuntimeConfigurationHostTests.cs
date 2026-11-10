using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Hosting.Tests;

public sealed class RuntimeConfigurationHostTests
{
    [Theory]
    [InlineData("", "Development", "http://localhost", "ClientSlug")]
    [InlineData("client-test", "Invalid", "https://www.example.test", "Environment")]
    [InlineData("client-test", "Production", "http://www.example.test", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://user:do-not-log@example.test", "PublicBaseUrl")]
    public async Task BothHostsRefuseInvalidConfigurationBeforeTheyStart(string slug, string environment, string url, string property)
    {
        using TestHosts hosts = new();
        string[] args = ["--SiteFactory:Runtime:ClientSlug", slug, "--SiteFactory:Runtime:PublicBaseUrl", url];
        IHost webHost = hosts.Create(RuntimeComponent.Web, environment, args);
        IHost workerHost = hosts.Create(RuntimeComponent.Worker, environment, args);
        CancellationToken token = TestContext.Current.CancellationToken;
        RuntimeStartupException web = await Assert.ThrowsAsync<RuntimeStartupException>(() => webHost.StartAsync(token));
        RuntimeStartupException worker = await Assert.ThrowsAsync<RuntimeStartupException>(() => workerHost.StartAsync(token));
        foreach (IHost host in new[] { webHost, workerHost })
        {
            Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
            RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
            RuntimeStartupReport report = Assert.IsType<RuntimeStartupReport>(startup.Report);
            Assert.False(report.IsReady);
            Assert.False(startup.ConfigurationIsValidated);
            Assert.False(startup.IsReady);
            Assert.Contains(report.Results, result => result.PropertyPath == property && result.Severity == StartupValidationSeverity.BlockingFailure);
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeConfiguration>());
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<RuntimeIdentity>());
            Assert.All(report.Results, static result => Assert.DoesNotContain("do-not-log", result.ToString(), StringComparison.Ordinal));
        }
        Assert.Equal(web.Message, worker.Message);
        Assert.Contains(" " + property + " ", web.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log", web.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log", worker.ToString(), StringComparison.Ordinal);
        Assert.All(hosts.LogMessages, static message => Assert.DoesNotContain("do-not-log", message, StringComparison.Ordinal));
        Assert.Empty(Directory.GetFileSystemEntries(hosts.DirectoryPath));
    }

    [Fact]
    public async Task BothHostsStartWithRealJsonConfigurationAndCorrectIdentity()
    {
        using TestHosts hosts = new();
        // Preparation only: neither runtime profile may create or rewrite this configuration.
        const string json = """{"SiteFactory":{"Runtime":{"ClientSlug":"client-test","PublicBaseUrl":"https://www.example.test"}}}""";
        CancellationToken token = TestContext.Current.CancellationToken;
        await hosts.WriteConfigurationAsync(json, token);
        string settings = Path.Combine(hosts.DirectoryPath, "appsettings.json");
        IHost web = hosts.Create(RuntimeComponent.Web, "Production");
        IHost worker = hosts.Create(RuntimeComponent.Worker, "Production");
        foreach ((IHost host, RuntimeComponent component) in new[] { (web, RuntimeComponent.Web), (worker, RuntimeComponent.Worker) })
        {
            RuntimeStartupService startup = host.Services.GetRequiredService<RuntimeStartupService>();
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            Assert.Null(startup.Report);
            await host.StartAsync(token);
            try
            {
                Assert.True(Assert.IsType<RuntimeStartupReport>(startup.Report).IsReady);
                Assert.True(startup.ConfigurationIsValidated);
                Assert.True(startup.IsReady);
                RuntimeConfiguration runtime = host.Services.GetRequiredService<RuntimeConfiguration>();
                Assert.Same(runtime, host.Services.GetRequiredService<RuntimeConfiguration>());
                Assert.Same(runtime.Identity, host.Services.GetRequiredService<RuntimeIdentity>());
                Assert.Equal(component, runtime.Identity.Component);
                Assert.Equal("client-test", runtime.Identity.ClientSlug);
                Assert.Equal(RuntimeEnvironment.Production, runtime.Identity.Environment);
                Assert.Equal(new Uri("https://www.example.test"), runtime.PublicBaseUrl);
                Assert.Equal(443, runtime.PublicBaseUrl.Port);
                Assert.True(lifetime.ApplicationStarted.IsCancellationRequested);
                Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
                Assert.False(lifetime.ApplicationStopped.IsCancellationRequested);
            }
            finally
            {
                await host.StopAsync(token);
            }
            Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
            Assert.False(startup.IsReady);
        }
        Assert.Equal(json, await File.ReadAllTextAsync(settings, token));
        Assert.Equal([settings], Directory.GetFileSystemEntries(hosts.DirectoryPath));
    }
}
