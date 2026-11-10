using System.Globalization;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;
using MigrationCommand = LogicGate.SiteFactory.Migrator.MigrationCommand;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

public sealed class RuntimeMigrationTests : IDisposable
{
    private readonly PostgreSqlSecretFixture secrets = new();
    private string DirectoryPath => secrets.DirectoryPath;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RuntimeDbContext CreateContext() => new(new DbContextOptionsBuilder<RuntimeDbContext>()
        .UseNpgsql(options => options.MigrationsHistoryTable("__EFMigrationsHistory", "sitefactory_runtime")).Options);

    [Fact]
    public void SnapshotAndMigrationTargetMatchTheCurrentModelWithoutConnecting()
    {
        using RuntimeDbContext context = CreateContext();
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        Assert.Equal(MigrationCommand.InitialMigration, Assert.Single(assembly.Migrations).Key);
        Assert.NotNull(assembly.ModelSnapshot);
        Assert.False(context.Database.HasPendingModelChanges());
        Migration migration = assembly.CreateMigration(assembly.Migrations.Single().Value, context.Database.ProviderName!);
        IModel target = context.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
        IModel current = context.GetService<IDesignTimeModel>().Model;
        Assert.False(context.GetService<IMigrationsModelDiffer>().HasDifferences(target.GetRelationalModel(), current.GetRelationalModel()));
        Assert.Null(context.Database.GetConnectionString());
    }

    [Fact]
    public void InitialUpExactlyMatchesTheSqlRequiredByTheModelAndContainsNoSeed()
    {
        using RuntimeDbContext context = CreateContext();
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        Migration migration = assembly.CreateMigration(Assert.Single(assembly.Migrations).Value, context.Database.ProviderName!);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IReadOnlyList<MigrationOperation> expected = context.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        IMigrationsSqlGenerator sql = context.GetService<IMigrationsSqlGenerator>();
        Assert.Equal(sql.Generate(expected, model).Select(static command => command.CommandText),
            sql.Generate(migration.UpOperations, model).Select(static command => command.CommandText));
        Assert.Equal("sitefactory_runtime", Assert.Single(migration.UpOperations.OfType<EnsureSchemaOperation>()).Name);
        Assert.Equal(["database_identity", "schema_state"], migration.UpOperations.OfType<CreateTableOperation>().Select(static table => table.Name));
        Assert.Equal(4, migration.UpOperations.Count); // schema, two tables, unique index; no arbitrary SQL or seed.
        Assert.All(migration.UpOperations.OfType<CreateTableOperation>(), static table => Assert.Equal("sitefactory_runtime", table.Schema));
        Assert.All(migration.DownOperations, static operation => Assert.IsType<DropTableOperation>(operation));
        Assert.Equal(2, migration.DownOperations.Count);
    }

    [Fact]
    public void ReviewScriptIsDeterministicAndHistoryIsInTheRuntimeSchema()
    {
        using RuntimeDbContext context = CreateContext();
        IMigrator migrator = context.GetService<IMigrator>();
        string script = migrator.GenerateScript(toMigration: MigrationCommand.InitialMigration);
        Assert.Equal(script, migrator.GenerateScript(toMigration: MigrationCommand.InitialMigration));
        Assert.Contains("sitefactory_runtime.\"__EFMigrationsHistory\"", script, StringComparison.Ordinal);
        Assert.Contains(MigrationCommand.InitialMigration, script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE ROLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO sitefactory_runtime.database_identity", script, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO sitefactory_runtime.schema_state", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpDoesNotRequireConfigurationOrApplyAnything()
    {
        string[] before = Directory.GetFileSystemEntries(DirectoryPath);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        Assert.Equal(0, await MigrationCommand.RunAsync(["--help"], null, output, error, Token));
        Assert.Contains("--confirm-migration", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(before, Directory.GetFileSystemEntries(DirectoryPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("apply")]
    [InlineData("apply --environment Development")]
    [InlineData("apply --environment Production --confirm-migration")]
    [InlineData("apply --environment Staging --confirm-migration")]
    [InlineData("apply --environment Unknown --confirm-migration")]
    [InlineData("apply --environment Development --yes")]
    [InlineData("apply --environment Development --confirm-migration --force")]
    [InlineData("apply --environment Development --confirm-migration --connection sensitive-value")]
    public async Task NonExplicitOrUnsupportedCommandIsRejectedBeforeReadingSecrets(string command)
    {
        await AssertFailureAsync(command.Split(' ', StringSplitOptions.RemoveEmptyEntries), Path.Combine(DirectoryPath, "absent"),
            2, "SF-MIGRATION-USAGE-001", Token);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public async Task ApplyRequiresItsDedicatedConnectionWithoutRuntimeFallback(string environment)
    {
        await AssertFailureAsync(["apply", "--environment", environment, "--confirm-migration"], null,
            2, "SF-MIGRATION-CONFIG-001", Token);
    }

    [Fact]
    public async Task MissingConnectionFileHasASafeDiagnosticWithoutCreatingIt()
    {
        await AssertFailureAsync(["apply", "--environment", "Test", "--confirm-migration"], Path.Combine(DirectoryPath, "missing"),
            2, "SF-MIGRATION-CONFIG-001", Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-connection-sensitive-value")]
    [InlineData("Database=example;Username=migrator")]
    public async Task InvalidConnectionIsRejectedWithoutEchoingItsContent(string content)
    {
        string file = secrets.PathFor(PostgreSqlComponent.Migrator);
        secrets.Write(PostgreSqlComponent.Migrator, content);
        await AssertFailureAsync(["apply", "--environment", "Test", "--confirm-migration"], file,
            2, "SF-MIGRATION-CONFIG-001", Token);
        Assert.Equal(content, await File.ReadAllTextAsync(file, Token));
    }

    [Fact]
    public async Task CancellationPreventsEvenSecretReading()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await cancellation.CancelAsync();
        await AssertFailureAsync(["apply", "--environment", "Test", "--confirm-migration"], Path.Combine(DirectoryPath, "absent"),
            130, "SF-MIGRATION-CANCELLED-001", cancellation.Token);
    }

    private async Task AssertFailureAsync(string[] args, string? file, int exit, string code, CancellationToken cancellationToken)
    {
        string[] before = Directory.GetFileSystemEntries(DirectoryPath);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        Assert.Equal(exit, await MigrationCommand.RunAsync(args, file, output, error, cancellationToken));
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith(code + " ", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(DirectoryPath, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-value", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", error.ToString(), StringComparison.Ordinal);
        secrets.AssertRedacted(error.ToString());
        Assert.Equal(before, Directory.GetFileSystemEntries(DirectoryPath));
    }

    private static string[] BootstrapArguments(string environment = "Test") =>
        ["bootstrap", "--environment", environment, "--client-slug", "example-client", "--database-instance-id",
            "11111111-1111-4111-8111-111111111111", "--operation-id", "22222222-2222-4222-8222-222222222222", "--confirm-bootstrap"];

    [Theory]
    [InlineData(2, "Production")]
    [InlineData(2, "Staging")]
    [InlineData(2, "test")]
    [InlineData(4, "")]
    [InlineData(4, "Bad")]
    [InlineData(4, "con")]
    [InlineData(4, "a--b")]
    [InlineData(6, "00000000-0000-0000-0000-000000000000")]
    [InlineData(6, "invalid")]
    [InlineData(8, "00000000-0000-0000-0000-000000000000")]
    [InlineData(8, "invalid")]
    [InlineData(9, "--yes")]
    public async Task BootstrapRejectsInvalidIdentityBeforeReadingSecrets(int index, string value)
    {
        string[] args = BootstrapArguments();
        args[index] = value;
        await AssertFailureAsync(args, Path.Combine(DirectoryPath, "absent"), 2, "SF-BOOTSTRAP-USAGE-001", Token);
    }

    [Fact]
    public async Task BootstrapRequiresEveryOptionAndRejectsExtraOptions()
    {
        string[] args = BootstrapArguments();
        await AssertFailureAsync(["bootstrap"], null, 2, "SF-BOOTSTRAP-USAGE-001", Token);
        await AssertFailureAsync(args[..^1], null, 2, "SF-BOOTSTRAP-USAGE-001", Token);
        await AssertFailureAsync([.. args, "--force"], null, 2, "SF-BOOTSTRAP-USAGE-001", Token);
        await AssertFailureAsync([.. args[..7], "--confirm-bootstrap"], null, 2, "SF-BOOTSTRAP-USAGE-001", Token);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public async Task BootstrapRequiresItsOwnConnectionWithoutFallback(string environment)
    {
        await AssertFailureAsync(BootstrapArguments(environment), null, 2, "SF-MIGRATION-CONFIG-001", Token);
    }

    [Fact]
    public async Task BootstrapCancellationPreventsSecretReading()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await cancellation.CancelAsync();
        await AssertFailureAsync(BootstrapArguments(), Path.Combine(DirectoryPath, "absent"),
            130, "SF-BOOTSTRAP-CANCELLED-001", cancellation.Token);
    }

    public void Dispose() => secrets.Dispose();
}
