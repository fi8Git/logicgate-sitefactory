using System.Data;
using System.Data.Common;
using System.Text.Json;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

public sealed class PostgreSqlHostConfigurationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ActualHostsBlockReadinessForMissingOrInvalidSecretsWithoutDisclosure(bool web, bool invalid)
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        PostgreSqlComponent component = web ? PostgreSqlComponent.Web : PostgreSqlComponent.Worker;
        secrets.Write(component, "invalid=" + secrets.PasswordFor(component));
        string[] args = secrets.Arguments(HostArguments());
        args = [.. args, "--" + PostgreSqlConfigurationReader.ConnectionFileKey(component), invalid ? secrets.PathFor(component) : ""];
        IHost host = web ? hosts.CreateWeb(args, secrets.ConfigureDatabaseServices) : hosts.CreateWorker(args, secrets.ConfigureDatabaseServices);
        RuntimeStartupException exception = await Assert.ThrowsAsync<RuntimeStartupException>(() => host.StartAsync(Token));
        Assert.False(host.Services.GetRequiredService<RuntimeStartupService>().IsReady);
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
        Assert.Contains(exception.Report.Results, result => result.ErrorCode == (invalid ? "SF-RUNTIME-CONFIG-004" : "SF-SECRET-001"));
        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<PostgreSqlRuntimeConfiguration>());
        Assert.Equal(0, secrets.Reads);
        secrets.AssertRedacted(exception.ToString() + JsonSerializer.Serialize(exception.Report) + string.Join('\n', hosts.LogMessages));
    }

    [Fact]
    public async Task RealHostsUseDistinctSnapshotsAndPublicHealthAndLogsRemainRedacted()
    {
        using OfflineDatabaseFixture secrets = new();
        await using TestHosts hosts = new();
        string[] args = secrets.Arguments(HostArguments());
        WebApplication web = hosts.CreateWeb(args, secrets.ConfigureDatabaseServices);
        IHost worker = hosts.CreateWorker(args, secrets.ConfigureDatabaseServices);
        Assert.NotEqual(secrets.PathFor(PostgreSqlComponent.Web), secrets.PathFor(PostgreSqlComponent.Worker));
        Assert.True(secrets.PasswordFor(PostgreSqlComponent.Web) != secrets.PasswordFor(PostgreSqlComponent.Worker), "Dedicated fictitious credentials must differ.");
        await web.StartAsync(Token);
        try
        {
            await worker.StartAsync(Token);
            try
            {
                PostgreSqlRuntimeConfiguration first = web.Services.GetRequiredService<PostgreSqlRuntimeConfiguration>();
                PostgreSqlRuntimeConfiguration second = worker.Services.GetRequiredService<PostgreSqlRuntimeConfiguration>();
                Assert.NotSame(first, second);
                Assert.Equal(PostgreSqlComponent.Web, first.Component);
                Assert.Equal(PostgreSqlComponent.Worker, second.Component);
                using RuntimeDbContext webContext = RuntimeDbContextFactory.Create(first);
                using RuntimeDbContext workerContext = RuntimeDbContextFactory.Create(second);
                Assert.Equal(ConnectionState.Closed, webContext.Database.GetDbConnection().State);
                Assert.Equal(ConnectionState.Closed, workerContext.Database.GetDbConnection().State);
                DbConnectionStringBuilder webConnection = new() { ConnectionString = webContext.Database.GetConnectionString()! };
                DbConnectionStringBuilder workerConnection = new() { ConnectionString = workerContext.Database.GetConnectionString()! };
                Assert.True((string)webConnection["Password"] == secrets.PasswordFor(PostgreSqlComponent.Web), "The Web snapshot must retain its fictitious credential.");
                Assert.True((string)workerConnection["Password"] == secrets.PasswordFor(PostgreSqlComponent.Worker), "The Worker snapshot must retain its fictitious credential.");
                secrets.Write(PostgreSqlComponent.Web, "invalid");
                Assert.Same(first, web.Services.GetRequiredService<PostgreSqlRuntimeConfiguration>());
                using HttpClient http = hosts.Client(web);
                foreach (string path in new[] { "/health/live", "/health/ready" })
                {
                    using HttpResponseMessage response = await http.GetAsync(path, Token);
                    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                    string body = await response.Content.ReadAsStringAsync(Token);
                    Assert.Equal("{\"status\":\"healthy\"}", body);
                    secrets.AssertRedacted(body);
                }
                secrets.AssertRedacted(JsonSerializer.Serialize(web.Services.GetRequiredService<RuntimeStartupService>().Report));
                secrets.AssertRedacted(JsonSerializer.Serialize(worker.Services.GetRequiredService<RuntimeStartupService>().Report));
                Assert.Equal(ConnectionState.Closed, webContext.Database.GetDbConnection().State);
                Assert.Equal(ConnectionState.Closed, workerContext.Database.GetDbConnection().State);
            }
            finally { await worker.StopAsync(Token); }
        }
        finally { await web.StopAsync(Token); }
        secrets.AssertRedacted(string.Join('\n', hosts.LogMessages));
    }

    private static string[] HostArguments() => ["--environment", "Development", "--urls", "http://127.0.0.1:0",
        "--SiteFactory:Runtime:ClientSlug", "client-test", "--SiteFactory:Runtime:PublicBaseUrl", "http://localhost"];
}
