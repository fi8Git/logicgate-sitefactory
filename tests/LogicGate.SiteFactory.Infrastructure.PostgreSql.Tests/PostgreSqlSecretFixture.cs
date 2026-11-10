using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.Tests.Generation;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

// Fictitious secrets allocated only for the current test, outside any repository.
internal sealed class PostgreSqlSecretFixture : IDisposable
{
    private readonly Dictionary<PostgreSqlComponent, string> files = [];
    private readonly Dictionary<PostgreSqlComponent, string> passwords = [];
    private readonly List<string> links = [];

    public PostgreSqlSecretFixture()
    {
        DirectoryPath = Directory.CreateTempSubdirectory("sitefactory-postgresql-config-test-").FullName;
        try
        {
            foreach (PostgreSqlComponent component in Enum.GetValues<PostgreSqlComponent>())
            {
                passwords.Add(component, " SF_TEST_SECRET_!é;e\u0301;$\\\"'" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + " ");
                files.Add(component, Path.Combine(DirectoryPath, component + ".txt"));
                Write(component, Connection(component));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string DirectoryPath { get; }
    public string PathFor(PostgreSqlComponent component) => files[component];
    public string PasswordFor(PostgreSqlComponent component) => passwords[component];
    public string Connection(PostgreSqlComponent component) => new NpgsqlConnectionStringBuilder
    {
        Host = "database.example.test",
        Database = "client_test",
        Username = "test_" + component,
        Password = passwords[component],
        SslMode = SslMode.VerifyFull,
    }.ConnectionString;

    public void Write(PostgreSqlComponent component, string content)
    {
        FileStreamOptions options = new() { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using FileStream stream = new(files[component], options);
        stream.Write(new UTF8Encoding(false, true).GetBytes(content));
    }

    public string CreateDirectoryLink()
    {
        string link = Path.Combine(DirectoryPath, "linked-source");
        links.Add(link);
        DirectoryLinkFixture.Create(link, DirectoryPath);
        return link;
    }

    public void AssertRedacted(string text)
    {
        Assert.True(!text.Contains("SF_TEST_SECRET_", StringComparison.Ordinal), "A fictitious secret marker was disclosed.");
        foreach (string password in passwords.Values)
            Assert.True(!text.Contains(password, StringComparison.Ordinal), "A fictitious password was disclosed.");
        Assert.True(!text.Contains(DirectoryPath, StringComparison.Ordinal), "The secret's physical path was disclosed.");
        Assert.True(!text.Contains("database.example.test", StringComparison.Ordinal), "Connection material was disclosed.");
        foreach (PostgreSqlComponent component in files.Keys)
            Assert.True(!text.Contains("test_" + component, StringComparison.Ordinal), "A connection username was disclosed.");
    }

    public void Dispose()
    {
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar;
        Assert.StartsWith(tempRoot, DirectoryPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        Assert.StartsWith("sitefactory-postgresql-config-test-", Path.GetFileName(DirectoryPath), StringComparison.Ordinal);
        Assert.False((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0);
        // Unlink only links made by this fixture, before deleting its own temporary tree.
        foreach (string link in links.Where(Directory.Exists))
            Directory.Delete(link);
        Directory.Delete(DirectoryPath, recursive: true);
        Assert.False(Directory.Exists(DirectoryPath));
    }
}
