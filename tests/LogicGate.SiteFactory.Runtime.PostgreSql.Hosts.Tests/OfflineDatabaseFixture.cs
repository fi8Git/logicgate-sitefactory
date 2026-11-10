using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Runtime;
using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using LogicGate.SiteFactory.Runtime.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests;

// Fictitious files and observations only. The production probe, never this fixture, decides compatibility.
internal sealed class OfflineDatabaseFixture : IDisposable
{
    private const string DirectoryPrefix = "sitefactory-postgresql-host-secret-";
    private readonly Dictionary<PostgreSqlComponent, string> files = [];
    private readonly Dictionary<PostgreSqlComponent, string> passwords = [];
    private int reads;

    public OfflineDatabaseFixture()
    {
        DirectoryPath = Directory.CreateTempSubdirectory(DirectoryPrefix).FullName;
        try
        {
            foreach (PostgreSqlComponent component in Enum.GetValues<PostgreSqlComponent>())
            {
                passwords.Add(component, " SF_TEST_SECRET_!é;$\\\"'" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + " ");
                files.Add(component, Path.Combine(DirectoryPath, component + ".txt"));
                DbConnectionStringBuilder connection = new()
                {
                    ["Host"] = "database.example.test",
                    ["Database"] = "client_test",
                    ["Username"] = "test_" + component,
                    ["Password"] = passwords[component],
                    ["SSL Mode"] = "VerifyFull",
                };
                Write(component, connection.ConnectionString);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string DirectoryPath { get; }
    public int Reads => Volatile.Read(ref reads);
    public string PathFor(PostgreSqlComponent component) => files[component];
    public string PasswordFor(PostgreSqlComponent component) => passwords[component];

    public void Write(PostgreSqlComponent component, string content)
    {
        FileStreamOptions options = new() { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using FileStream stream = new(files[component], options);
        stream.Write(new UTF8Encoding(false, true).GetBytes(content));
    }

    public string[] Arguments(params string[] args) => [.. args, "--" + WebDatabaseReadiness.ExpectedRoleKey, "test_Web",
        "--" + WorkerDatabaseStartupValidator.ExpectedRoleKey, "test_Worker", .. new[] { PostgreSqlComponent.Web, PostgreSqlComponent.Worker }
        .SelectMany(component => new[] { "--" + PostgreSqlConfigurationReader.ConnectionFileKey(component), files[component] })];

    public void ConfigureDatabaseServices(IServiceCollection services) => services.AddSingleton<IRuntimeDatabaseReader>(provider =>
        new CompatibleReader(provider.GetRequiredService<RuntimeConfigurationStartupValidation>().Configuration!.Identity, this));

    internal static RuntimeDatabaseObservation Compatible(RuntimeIdentity identity) => new(DatabaseReadOutcome.Read,
        new("test_" + identity.Component, "test_" + identity.Component, true, false, "pg_catalog, sitefactory_runtime", DatabaseRolePrivileges.None),
        [new(new("11111111-1111-4111-8111-111111111111"), identity.ClientSlug, identity.Environment.ToString())],
        [new(DatabaseCompatibilityProbe.SchemaFamily, DatabaseCompatibilityProbe.SchemaVersion, DatabaseCompatibilityProbe.InitialMigration, "stable")],
        [DatabaseCompatibilityProbe.InitialMigration]);

    private sealed class CompatibleReader(RuntimeIdentity identity, OfflineDatabaseFixture fixture) : IRuntimeDatabaseReader
    {
        public Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref fixture.reads);
            return Task.FromResult(Compatible(identity));
        }
    }

    public void AssertRedacted(string text)
    {
        Assert.True(!text.Contains("SF_TEST_SECRET_", StringComparison.Ordinal), "A fictitious secret marker was disclosed.");
        foreach (string password in passwords.Values)
            Assert.True(!text.Contains(password, StringComparison.Ordinal), "A fictitious password was disclosed.");
        Assert.True(!text.Contains(DirectoryPath, StringComparison.Ordinal), "The secret's physical path was disclosed.");
    }

    public void Dispose()
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, DirectoryPath, comparison);
        Assert.StartsWith(DirectoryPrefix, Path.GetFileName(DirectoryPath), StringComparison.Ordinal);
        Assert.False((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0);
        Directory.Delete(DirectoryPath, recursive: true);
        Assert.False(Directory.Exists(DirectoryPath));
    }
}
