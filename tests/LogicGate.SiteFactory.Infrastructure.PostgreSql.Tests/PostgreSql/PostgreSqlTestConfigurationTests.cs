using System.Text;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

public sealed class PostgreSqlTestConfigurationTests
{
    private static NpgsqlConnectionStringBuilder Valid() => new()
    {
        Host = "127.0.0.1",
        Port = 15432,
        Database = PostgreSqlTestConfiguration.AdministrativeDatabase,
        Username = PostgreSqlTestConfiguration.AdministrativeDatabase,
        Password = "<TEST_ONLY_PASSWORD>",
        SslMode = SslMode.VerifyFull,
        RootCertificate = Path.Combine(Path.GetTempPath(), "test-ca.crt"),
    };

    [Theory]
    [InlineData("Host", null)]
    [InlineData("Host", "database.example.test")]
    [InlineData("Host", "127.0.0.1,database.example.test")]
    [InlineData("Port", null)]
    [InlineData("Database", "developer")]
    [InlineData("Database", "postgres")]
    [InlineData("Username", "postgres")]
    [InlineData("Password", null)]
    [InlineData("SSL Mode", "Prefer")]
    [InlineData("SSL Mode", "Disable")]
    [InlineData("Root Certificate", null)]
    [InlineData("Include Error Detail", "true")]
    [InlineData("Passfile", "private-file")]
    [InlineData("Options", "-c search_path=public")]
    public void UnsafeOrImplicitAdministrativeConfigurationIsRejected(string key, string? value)
    {
        NpgsqlConnectionStringBuilder settings = Valid();
        if (value is null)
            settings.Remove(key);
        else
            settings[key] = value;
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => PostgreSqlTestConfiguration.Parse(settings.ConnectionString));
        Assert.StartsWith("SF-TEST-PG-003", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("<TEST_ONLY_PASSWORD>", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection;<TEST_ONLY_PASSWORD>")]
    [InlineData("Password=<TEST_ONLY_PASSWORD>;Port=invalid")]
    public void MalformedConfigurationNeverLeaksProviderDetails(string value)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => PostgreSqlTestConfiguration.Parse(value));
        Assert.StartsWith("SF-TEST-PG-00", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("<TEST_ONLY_PASSWORD>", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileConfigurationIsRequiredStrictUtf8AndBounded()
    {
        InvalidOperationException missing = await Assert.ThrowsAsync<InvalidOperationException>(() => PostgreSqlTestConfiguration.ReadAsync(null, TestContext.Current.CancellationToken));
        Assert.StartsWith("SF-TEST-PG-001", missing.Message, StringComparison.Ordinal);
        Assert.Null(missing.InnerException);
        Assert.DoesNotContain("<TEST_ONLY_PASSWORD>", missing.ToString(), StringComparison.Ordinal);
        DirectoryInfo directory = Directory.CreateTempSubdirectory("sitefactory-pg-config-test-");
        try
        {
            string file = Path.Combine(directory.FullName, "admin.txt");
            InvalidOperationException absent = await Assert.ThrowsAsync<InvalidOperationException>(() => PostgreSqlTestConfiguration.ReadAsync(file, TestContext.Current.CancellationToken));
            Assert.StartsWith("SF-TEST-PG-002", absent.Message, StringComparison.Ordinal);
            Assert.Null(absent.InnerException);
            Assert.DoesNotContain(directory.FullName, absent.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("<TEST_ONLY_PASSWORD>", absent.ToString(), StringComparison.Ordinal);

            string valid = Valid().ConnectionString + "\n";
            await File.WriteAllTextAsync(file, valid, new UTF8Encoding(false), TestContext.Current.CancellationToken);
            _ = await PostgreSqlTestConfiguration.ReadAsync(file, TestContext.Current.CancellationToken);

            byte[] limit = Encoding.UTF8.GetBytes(valid + new string(' ', 16384 - Encoding.UTF8.GetByteCount(valid)));
            Assert.Equal(16384, limit.Length);
            await File.WriteAllBytesAsync(file, limit, TestContext.Current.CancellationToken);
            _ = await PostgreSqlTestConfiguration.ReadAsync(file, TestContext.Current.CancellationToken);

            foreach (byte[] invalid in new[] { new byte[] { 0xff }, new byte[16385] })
            {
                await File.WriteAllBytesAsync(file, invalid, TestContext.Current.CancellationToken);
                InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => PostgreSqlTestConfiguration.ReadAsync(file, TestContext.Current.CancellationToken));
                Assert.StartsWith("SF-TEST-PG-002", error.Message, StringComparison.Ordinal);
                Assert.Null(error.InnerException);
                Assert.DoesNotContain(directory.FullName, error.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("<TEST_ONLY_PASSWORD>", error.ToString(), StringComparison.Ordinal);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
        Assert.False(Directory.Exists(directory.FullName));
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("sitefactory_test_admin")]
    [InlineData("sf_test_")]
    [InlineData("sf_test_0000000000000000000000000000000g")]
    [InlineData("sf_test_00000000000000000000000000000000;DROP DATABASE postgres")]
    public void CleanupCannotTargetCallerSuppliedOrAdministrativeNames(string name) => Assert.False(PostgreSqlTestDatabase.IsOwnedName(name));

    [Fact]
    public async Task CancellationIsNotTranslatedIntoConfigurationError()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PostgreSqlTestConfiguration.ReadAsync(null, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PostgreSqlTestDatabase.CreateAsync(PostgreSqlTestConfiguration.Parse(Valid().ConnectionString), cancelled.Token));
    }
}
