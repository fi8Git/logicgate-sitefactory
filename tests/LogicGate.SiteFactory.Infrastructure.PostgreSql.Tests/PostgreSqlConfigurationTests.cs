using System.Data;
using System.Reflection;
using System.Text.Json;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class PostgreSqlConfigurationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("TEST")]
    public async Task AllComponentsHaveIndependentImmutableRedactedSnapshots(string environment)
    {
        using PostgreSqlSecretFixture secrets = new();
        List<PostgreSqlRuntimeConfiguration> snapshots = [];
        foreach (PostgreSqlComponent component in Enum.GetValues<PostgreSqlComponent>())
        {
            byte[] original = await File.ReadAllBytesAsync(secrets.PathFor(component), Token);
            PostgreSqlRuntimeConfiguration snapshot = await ReadAsync(secrets, component, environment);
            snapshots.Add(snapshot);
            Assert.Equal(component, snapshot.Component);
            Assert.Equal($"PostgreSQL {component}: configured", snapshot.ToString());
            secrets.AssertRedacted(snapshot.ToString());
            secrets.AssertRedacted(JsonSerializer.Serialize(snapshot));
            Assert.Equal("Component", Assert.Single(snapshot.GetType().GetProperties()).Name);
            Assert.Empty(snapshot.GetType().GetConstructors());
            Assert.All(snapshot.GetType().GetProperties(), static property => Assert.Null(property.SetMethod));
            Assert.All(snapshot.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance), static field => Assert.True(field.IsInitOnly));
            Assert.Equal(original, await File.ReadAllBytesAsync(secrets.PathFor(component), Token));
            using NpgsqlConnection probe = CreateClosedProbe(snapshot);
            NpgsqlConnectionStringBuilder settings = new(probe.ConnectionString);
            Assert.True(settings.Password == secrets.PasswordFor(component), "The selected component's password changed.");
            Assert.Equal("test_" + component, settings.Username);
            Assert.Equal(ConnectionState.Closed, probe.State);
        }
        Assert.Equal(3, snapshots.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingReferencesHaveNoCredentialFallback(string? path)
    {
        foreach (PostgreSqlComponent component in Enum.GetValues<PostgreSqlComponent>())
        {
            PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() =>
                PostgreSqlConfigurationReader.ReadAsync(component, path, "Test", Token));
            Assert.Equal("SF-SECRET-001", error.Code);
            Assert.Null(error.InnerException);
        }
    }

    [Theory]
    [InlineData("empty", "SF-SECRET-002")]
    [InlineData("lf-only", "SF-SECRET-002")]
    [InlineData("crlf-only", "SF-SECRET-002")]
    [InlineData("whitespace", "SF-RUNTIME-CONFIG-004")]
    [InlineData("syntax", "SF-RUNTIME-CONFIG-004")]
    [InlineData("unknown-key", "SF-RUNTIME-CONFIG-004")]
    [InlineData("missing-host", "SF-RUNTIME-CONFIG-004")]
    [InlineData("missing-database", "SF-RUNTIME-CONFIG-004")]
    [InlineData("missing-username", "SF-RUNTIME-CONFIG-004")]
    [InlineData("missing-password", "SF-RUNTIME-CONFIG-004")]
    [InlineData("invalid-port", "SF-RUNTIME-CONFIG-004")]
    [InlineData("zero-port", "SF-RUNTIME-CONFIG-004")]
    [InlineData("too-large", "SF-RUNTIME-CONFIG-004")]
    [InlineData("persist", "SF-RUNTIME-CONFIG-009")]
    [InlineData("error-detail", "SF-RUNTIME-CONFIG-009")]
    [InlineData("log-parameters", "SF-RUNTIME-CONFIG-009")]
    [InlineData("passfile", "SF-RUNTIME-CONFIG-009")]
    [InlineData("disable-tls", "SF-RUNTIME-CONFIG-009")]
    public async Task InvalidDocumentsHaveDeterministicSafeDiagnostics(string mutation, string code)
    {
        using PostgreSqlSecretFixture secrets = new();
        secrets.Write(PostgreSqlComponent.Web, Mutate(secrets, mutation));
        byte[] original = await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token);
        PostgreSqlConfigurationException first = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets));
        PostgreSqlConfigurationException second = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets));
        Assert.Equal(code, first.Code);
        Assert.Equal(first.Message, second.Message);
        Assert.Equal(first.Message, first.ToString());
        Assert.Null(first.InnerException);
        secrets.AssertRedacted(first.Message + first + second);
        Assert.Equal(original, await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token));
    }

    [Fact]
    public async Task ReadFailuresDoNotDisclosePathsOrCreateAFile()
    {
        using PostgreSqlSecretFixture secrets = new();
        string[] originalFiles = Directory.GetFiles(secrets.DirectoryPath).Order(StringComparer.Ordinal).ToArray();
        Dictionary<string, byte[]> originalContents = [];
        Dictionary<string, DateTime> originalWrites = [];
        Dictionary<string, UnixFileMode> originalModes = [];
        foreach (string file in originalFiles)
        {
            originalContents.Add(file, await File.ReadAllBytesAsync(file, Token));
            originalWrites.Add(file, File.GetLastWriteTimeUtc(file));
            if (!OperatingSystem.IsWindows()) originalModes.Add(file, File.GetUnixFileMode(file));
        }
        DateTime directoryWrite = Directory.GetLastWriteTimeUtc(secrets.DirectoryPath);
        UnixFileMode? directoryMode = OperatingSystem.IsWindows() ? null : File.GetUnixFileMode(secrets.DirectoryPath);
        string missing = secrets.PathFor(PostgreSqlComponent.Web) + ".missing";
        foreach (string path in new[] { missing, secrets.DirectoryPath, secrets.DirectoryPath + '\0' })
        {
            PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() =>
                PostgreSqlConfigurationReader.ReadAsync(PostgreSqlComponent.Web, path, "Test", Token));
            Assert.Equal("SF-SECRET-004", error.Code);
            Assert.Null(error.InnerException);
            secrets.AssertRedacted(error.Message + error);
            Assert.True(Directory.Exists(secrets.DirectoryPath), "The source directory must remain present.");
            Assert.Equal(directoryWrite, Directory.GetLastWriteTimeUtc(secrets.DirectoryPath));
            if (!OperatingSystem.IsWindows()) Assert.Equal(directoryMode, File.GetUnixFileMode(secrets.DirectoryPath));
            Assert.True(originalFiles.SequenceEqual(Directory.GetFiles(secrets.DirectoryPath).Order(StringComparer.Ordinal)),
                "Reading a rejected reference must not create or remove any file.");
            foreach (string file in originalFiles)
            {
                byte[] currentContents = await File.ReadAllBytesAsync(file, Token);
                Assert.True(originalContents[file].SequenceEqual(currentContents),
                    "Reading a rejected reference must not change connection-file bytes.");
                Assert.Equal(originalWrites[file], File.GetLastWriteTimeUtc(file));
                if (!OperatingSystem.IsWindows()) Assert.Equal(originalModes[file], File.GetUnixFileMode(file));
            }
        }
        Assert.False(File.Exists(missing));
        Assert.Equal(3, Directory.GetFiles(secrets.DirectoryPath).Length);
    }

    [Fact]
    public async Task AnExclusivelyLockedSecretIsReportedWithoutDisclosure()
    {
        using PostgreSqlSecretFixture secrets = new();
        using FileStream locked = new(secrets.PathFor(PostgreSqlComponent.Web), FileMode.Open, FileAccess.Read, FileShare.None);
        PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets));
        Assert.Equal("SF-SECRET-004", error.Code);
        Assert.Null(error.InnerException);
        secrets.AssertRedacted(error.ToString());
    }

    [Theory]
    [InlineData(new byte[] { 0xff, 0xff })]
    [InlineData(new byte[] { 0xc3 })]
    [InlineData(new byte[] { 0xed, 0xa0, 0x80 })]
    public async Task InvalidUtf8IsRejectedWithoutDisclosure(byte[] bytes)
    {
        using PostgreSqlSecretFixture secrets = new();
        await File.WriteAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), bytes, Token);
        PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets));
        Assert.Equal("SF-SECRET-004", error.Code);
        Assert.Null(error.InnerException);
        secrets.AssertRedacted(error.ToString());
        Assert.Equal(bytes, await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Utf8QuotesWhitespaceAndTerminalNewlinePreservePasswordBytes(string ending)
    {
        using PostgreSqlSecretFixture secrets = new();
        secrets.Write(PostgreSqlComponent.Worker, secrets.Connection(PostgreSqlComponent.Worker) + ending);
        PostgreSqlRuntimeConfiguration snapshot = await ReadAsync(secrets, PostgreSqlComponent.Worker);
        using NpgsqlConnection probe = CreateClosedProbe(snapshot);
        Assert.True(new NpgsqlConnectionStringBuilder(probe.ConnectionString).Password == secrets.PasswordFor(PostgreSqlComponent.Worker),
            "Password characters or whitespace changed.");
        Assert.Equal(ConnectionState.Closed, probe.State);
    }

    [Fact]
    public async Task ProbeConstructionIsOfflineBoundedAndPreservesTls()
    {
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlRuntimeConfiguration snapshot = await ReadAsync(secrets);
        using NpgsqlConnection probe = CreateClosedProbe(snapshot);
        NpgsqlConnectionStringBuilder settings = new(probe.ConnectionString);
        Assert.Equal(ConnectionState.Closed, probe.State);
        Assert.False(settings.Pooling);
        Assert.False(settings.Multiplexing);
        Assert.False(settings.NoResetOnClose);
        Assert.Equal(3, settings.Timeout);
        Assert.Equal(3, settings.CommandTimeout);
        Assert.Equal(1000, settings.CancellationTimeout);
        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
        Assert.Equal("sitefactory-compatibility-probe", settings.ApplicationName);
    }

    [Fact]
    public async Task CancellationPreservesTheCallersTokenAndDoesNotReadOrChangeSecrets()
    {
        using PostgreSqlSecretFixture secrets = new();
        byte[] original = await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await cancellation.CancelAsync();
        foreach (string? path in new[] { secrets.PathFor(PostgreSqlComponent.Web), null })
        {
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                PostgreSqlConfigurationReader.ReadAsync(PostgreSqlComponent.Web, path, "Test", cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        Assert.Equal(original, await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public async Task UnknownComponentsAreProgrammingErrors(int value)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            PostgreSqlConfigurationReader.ReadAsync((PostgreSqlComponent)value, null, "Test", Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => PostgreSqlConfigurationReader.ConnectionFileKey((PostgreSqlComponent)value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData(" Production ")]
    [InlineData("unsupported-SF_TEST_SECRET_environment")]
    public async Task AnExplicitSupportedEnvironmentIsRequiredBeforeReadingTheFile(string? environment)
    {
        using PostgreSqlSecretFixture secrets = new();
        PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() =>
            PostgreSqlConfigurationReader.ReadAsync(PostgreSqlComponent.Web, secrets.PathFor(PostgreSqlComponent.Web) + ".missing", environment!, Token));
        Assert.Equal("SF-RUNTIME-CONFIG-007", error.Code);
        secrets.AssertRedacted(error.ToString());
    }

    [Fact]
    public void RuntimeFileKeysRemainDistinctAndDoNotOfferAMigratorFallback()
    {
        Assert.Equal("SITEFACTORY_WEB_CONNECTION_FILE", PostgreSqlConfigurationReader.ConnectionFileKey(PostgreSqlComponent.Web));
        Assert.Equal("SITEFACTORY_WORKER_CONNECTION_FILE", PostgreSqlConfigurationReader.ConnectionFileKey(PostgreSqlComponent.Worker));
        Assert.Throws<ArgumentOutOfRangeException>(() => PostgreSqlConfigurationReader.ConnectionFileKey(PostgreSqlComponent.Migrator));
    }

    [Theory]
    [InlineData(PostgreSqlComponent.Web)]
    [InlineData(PostgreSqlComponent.Worker)]
    [InlineData(PostgreSqlComponent.Migrator)]
    public async Task OnlyTheSelectedFileIsReadAndAnExistingSnapshotDoesNotReload(PostgreSqlComponent component)
    {
        using PostgreSqlSecretFixture secrets = new();
        foreach (PostgreSqlComponent other in Enum.GetValues<PostgreSqlComponent>().Where(candidate => candidate != component))
            secrets.Write(other, "deliberately-invalid-SF_TEST_SECRET_document");
        PostgreSqlRuntimeConfiguration snapshot = await ReadAsync(secrets, component);
        secrets.Write(component, "invalid-SF_TEST_SECRET_document");
        using NpgsqlConnection probe = CreateClosedProbe(snapshot);
        Assert.True(new NpgsqlConnectionStringBuilder(probe.ConnectionString).Password == secrets.PasswordFor(component), "The immutable snapshot reloaded.");
        PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets, component));
        Assert.Equal("SF-RUNTIME-CONFIG-004", error.Code);
        secrets.AssertRedacted(error.ToString());
        error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() =>
            PostgreSqlConfigurationReader.ReadAsync(component, null, "Test", Token));
        Assert.Equal("SF-SECRET-001", error.Code);
    }

    [Fact]
    public async Task ALinkedAncestorIsRejectedWithoutModifyingItsTarget()
    {
        using PostgreSqlSecretFixture secrets = new();
        byte[] original = await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token);
        string link = secrets.CreateDirectoryLink();
        PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() =>
            PostgreSqlConfigurationReader.ReadAsync(PostgreSqlComponent.Web, Path.Combine(link, "Web.txt"), "Test", Token));
        Assert.Equal("SF-SECRET-004", error.Code);
        secrets.AssertRedacted(error.ToString());
        Assert.Equal(original, await File.ReadAllBytesAsync(secrets.PathFor(PostgreSqlComponent.Web), Token));
    }

    [Theory]
    [InlineData(UnixFileMode.UserRead)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)]
    public async Task UnixPermissionsRemainOwnerOnlyWithoutBeingRepaired(UnixFileMode mode)
    {
        using PostgreSqlSecretFixture secrets = new();
        if (!OperatingSystem.IsWindows())
        {
            string path = secrets.PathFor(PostgreSqlComponent.Web);
            byte[] original = await File.ReadAllBytesAsync(path, Token);
            File.SetUnixFileMode(path, mode);
            if (mode == UnixFileMode.UserRead || mode == (UnixFileMode.UserRead | UnixFileMode.UserWrite))
                secrets.AssertRedacted((await ReadAsync(secrets)).ToString());
            else
            {
                PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets));
                Assert.Equal("SF-SECRET-005", error.Code);
                secrets.AssertRedacted(error.ToString());
            }
            Assert.Equal(mode, File.GetUnixFileMode(path));
            Assert.Equal(original, await File.ReadAllBytesAsync(path, Token));
        }
        else
            secrets.AssertRedacted((await ReadAsync(secrets)).ToString());
    }

    [Theory]
    [InlineData("password")]
    [InlineData("placeholder")]
    [InlineData("database-placeholder")]
    [InlineData("username-placeholder")]
    [InlineData("tls")]
    [InlineData("superuser")]
    [InlineData("localhost")]
    public async Task ProductionRejectsTheExistingUnsafeCases(string mutation)
    {
        using PostgreSqlSecretFixture secrets = new();
        NpgsqlConnectionStringBuilder builder = new(secrets.Connection(PostgreSqlComponent.Web));
        switch (mutation)
        {
            case "password": builder["Password"] = "password"; break;
            case "placeholder": builder.Password = "<POSTGRES_WEB_PASSWORD>"; break;
            case "database-placeholder": builder.Database = "TODO"; break;
            case "username-placeholder": builder.Username = "replace-me"; break;
            case "tls": builder.SslMode = SslMode.Prefer; break;
            case "superuser": builder.Username = "postgres"; break;
            case "localhost": builder.Host = "localhost"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        secrets.Write(PostgreSqlComponent.Web, builder.ConnectionString);
        PostgreSqlConfigurationException error = await Assert.ThrowsAsync<PostgreSqlConfigurationException>(() => ReadAsync(secrets, environment: "Production"));
        Assert.Equal("SF-RUNTIME-CONFIG-009", error.Code);
        secrets.AssertRedacted(error.ToString());
    }

    [Theory]
    [InlineData(SslMode.Prefer)]
    [InlineData(SslMode.Require)]
    public async Task NonProductionTlsPoliciesAreNotImplicitlyTightened(SslMode sslMode)
    {
        using PostgreSqlSecretFixture secrets = new();
        NpgsqlConnectionStringBuilder builder = new(secrets.Connection(PostgreSqlComponent.Worker)) { SslMode = sslMode };
        secrets.Write(PostgreSqlComponent.Worker, builder.ConnectionString);
        PostgreSqlRuntimeConfiguration snapshot = await ReadAsync(secrets, PostgreSqlComponent.Worker);
        using NpgsqlConnection probe = CreateClosedProbe(snapshot);
        Assert.Equal(sslMode, new NpgsqlConnectionStringBuilder(probe.ConnectionString).SslMode);
        Assert.Equal(ConnectionState.Closed, probe.State);
    }

    private static Task<PostgreSqlRuntimeConfiguration> ReadAsync(PostgreSqlSecretFixture secrets,
        PostgreSqlComponent component = PostgreSqlComponent.Web, string environment = "Test") =>
        PostgreSqlConfigurationReader.ReadAsync(component, secrets.PathFor(component), environment, Token);

    // Inspect existing internal infrastructure behavior without adding a public secret accessor or IVT.
    private static NpgsqlConnection CreateClosedProbe(PostgreSqlRuntimeConfiguration snapshot) => Assert.IsType<NpgsqlConnection>(
        typeof(PostgreSqlRuntimeConfiguration).GetMethod("CreateProbeConnection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(snapshot, null));

    private static string Mutate(PostgreSqlSecretFixture secrets, string mutation)
    {
        NpgsqlConnectionStringBuilder builder = new(secrets.Connection(PostgreSqlComponent.Web));
        switch (mutation)
        {
            case "empty": return string.Empty;
            case "lf-only": return "\n";
            case "crlf-only": return "\r\n";
            case "whitespace": return " ";
            case "syntax": return "\"" + secrets.PasswordFor(PostgreSqlComponent.Web);
            case "unknown-key": return "not-a-setting=" + secrets.PasswordFor(PostgreSqlComponent.Web);
            case "missing-host": builder.Remove("Host"); break;
            case "missing-database": builder.Remove("Database"); break;
            case "missing-username": builder.Remove("Username"); break;
            case "missing-password": builder.Remove("Password"); break;
            case "invalid-port": return "Port=invalid;" + builder.ConnectionString;
            case "zero-port": return "Port=0;" + builder.ConnectionString;
            case "too-large": return new string('x', 16385);
            case "persist": builder.PersistSecurityInfo = true; break;
            case "error-detail": builder.IncludeErrorDetail = true; break;
            case "log-parameters": builder.LogParameters = true; break;
            case "passfile": builder.Passfile = secrets.PathFor(PostgreSqlComponent.Web); break;
            case "disable-tls": builder.SslMode = SslMode.Disable; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        return builder.ConnectionString;
    }
}
