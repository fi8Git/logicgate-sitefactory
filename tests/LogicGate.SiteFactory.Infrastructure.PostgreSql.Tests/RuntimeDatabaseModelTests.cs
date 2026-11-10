using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests;

// Npgsql model inspection only: no connection, migration, InMemory provider or claim of database enforcement.
public sealed class RuntimeDatabaseModelTests
{
    private static RuntimeDbContext CreateContext() => new(new DbContextOptionsBuilder<RuntimeDbContext>().UseNpgsql().Options);

    private static IModel ReadModel(RuntimeDbContext context) => context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void ModelContainsOnlyTheTwoRuntimeTablesWithoutSeedOrBusinessEntities()
    {
        using RuntimeDbContext context = CreateContext();
        IModel model = ReadModel(context);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal("sitefactory_runtime", model.GetDefaultSchema());
        Assert.Equal([typeof(DatabaseIdentity), typeof(SchemaState)], model.GetEntityTypes().Select(static entity => entity.ClrType));
        Assert.Equal(["database_identity", "schema_state"], model.GetRelationalModel().Tables.Select(static table => table.Name));
        foreach (IEntityType entity in model.GetEntityTypes())
        {
            Assert.Equal("sitefactory_runtime", entity.GetSchema());
            Assert.Empty(entity.GetForeignKeys());
            Assert.Empty(entity.GetNavigations());
            Assert.Empty(entity.GetSeedData());
            Assert.False(entity.IsOwned());
        }
        Assert.Equal(7, model.FindEntityType(typeof(DatabaseIdentity))!.GetProperties().Count());
        Assert.Equal(6, model.FindEntityType(typeof(SchemaState))!.GetProperties().Count());
    }

    [Theory]
    [InlineData("DatabaseInstanceId", "database_instance_id", "uuid", null, typeof(Guid))]
    [InlineData("ClientSlug", "client_slug", "character varying(80)", 80, typeof(string))]
    [InlineData("Environment", "environment", "character varying(32)", 32, typeof(string))]
    [InlineData("CreatedAtUtc", "created_at_utc", "timestamp with time zone", null, typeof(DateTimeOffset))]
    [InlineData("ProvisioningVersion", "provisioning_version", "character varying(64)", 64, typeof(string))]
    [InlineData("DatabaseOwnerRoleId", "database_owner_role_id", "character varying(100)", 100, typeof(string))]
    [InlineData("Singleton", "singleton", "boolean", null, typeof(bool))]
    public void DatabaseIdentityColumnsAreExplicitAndRequired(string property, string column, string storeType, int? length, Type clrType)
    {
        using RuntimeDbContext context = CreateContext();
        VerifyProperty(ReadModel(context).FindEntityType(typeof(DatabaseIdentity))!, "database_identity", property, column, storeType, length, clrType, nullable: false);
    }

    [Theory]
    [InlineData("SchemaFamily", "schema_family", "character varying(100)", 100, typeof(string), false)]
    [InlineData("CurrentVersion", "current_version", "character varying(64)", 64, typeof(string), false)]
    [InlineData("LastMigrationId", "last_migration_id", "character varying(150)", 150, typeof(string), true)]
    [InlineData("MigrationState", "migration_state", "character varying(32)", 32, typeof(string), false)]
    [InlineData("UpdatedAtUtc", "updated_at_utc", "timestamp with time zone", null, typeof(DateTimeOffset), false)]
    [InlineData("UpdatedByOperationId", "updated_by_operation_id", "uuid", null, typeof(Guid), false)]
    public void SchemaStateColumnsAreExplicitWithOnlyTheLastMigrationOptional(string property, string column, string storeType, int? length, Type clrType, bool nullable)
    {
        using RuntimeDbContext context = CreateContext();
        VerifyProperty(ReadModel(context).FindEntityType(typeof(SchemaState))!, "schema_state", property, column, storeType, length, clrType, nullable);
    }

    [Fact]
    public void KeysAndIndexesDeclareOneIdentityAndOneStatePerFamilyWithoutAutomaticIds()
    {
        using RuntimeDbContext context = CreateContext();
        IModel model = ReadModel(context);
        IEntityType identity = model.FindEntityType(typeof(DatabaseIdentity))!;
        IEntityType state = model.FindEntityType(typeof(SchemaState))!;
        Assert.Equal("DatabaseInstanceId", Assert.Single(Assert.Single(identity.GetKeys()).Properties).Name);
        Assert.Equal("pk_database_identity", identity.FindPrimaryKey()!.GetName());
        IIndex singleton = Assert.Single(identity.GetIndexes());
        Assert.Equal("Singleton", Assert.Single(singleton.Properties).Name);
        Assert.Equal("ux_database_identity_singleton", singleton.GetDatabaseName());
        Assert.True(singleton.IsUnique);
        Assert.Null(singleton.GetFilter());
        Assert.True(identity.FindProperty("Singleton")!.IsShadowProperty());
        Assert.Equal(true, identity.FindProperty("Singleton")!.GetDefaultValue());
        Assert.Equal("SchemaFamily", Assert.Single(Assert.Single(state.GetKeys()).Properties).Name);
        Assert.Equal("pk_schema_state", state.FindPrimaryKey()!.GetName());
        Assert.Empty(state.GetIndexes()); // The family primary key already provides the required uniqueness/lookup.
        foreach (IProperty property in model.GetEntityTypes().SelectMany(static entity => entity.GetProperties()).Where(static property => property.Name != "Singleton"))
        {
            Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
            Assert.Null(property.GetDefaultValueSql());
            Assert.Null(property.GetComputedColumnSql());
        }
    }

    [Fact]
    public void CheckConstraintDefinitionsRejectEmptyMetadataAndUnknownMigrationStates()
    {
        using RuntimeDbContext context = CreateContext();
        IModel model = ReadModel(context);
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["ck_database_identity_singleton"] = "singleton = TRUE",
            ["ck_database_identity_instance_id"] = "database_instance_id <> '00000000-0000-0000-0000-000000000000'::uuid",
            ["ck_database_identity_client_slug"] = "length(btrim(client_slug)) > 0",
            ["ck_database_identity_environment"] = "length(btrim(environment)) > 0",
            ["ck_database_identity_provisioning_version"] = "length(btrim(provisioning_version)) > 0",
            ["ck_database_identity_owner_role"] = "length(btrim(database_owner_role_id)) > 0",
            ["ck_schema_state_family"] = "length(btrim(schema_family)) > 0",
            ["ck_schema_state_version"] = "length(btrim(current_version)) > 0",
            ["ck_schema_state_last_migration"] = "last_migration_id IS NULL OR length(btrim(last_migration_id)) > 0",
            ["ck_schema_state_migration_state"] = "migration_state IN ('stable', 'migration-planned', 'migration-running', 'migration-failed', 'restore-pending', 'reconciliation-required')",
            ["ck_schema_state_operation_id"] = "updated_by_operation_id <> '00000000-0000-0000-0000-000000000000'::uuid",
        };
        Dictionary<string, string> actual = model.GetEntityTypes().SelectMany(static entity => entity.GetCheckConstraints())
            .ToDictionary(static constraint => Assert.IsType<string>(constraint.Name), static constraint => constraint.Sql, StringComparer.Ordinal);
        Assert.Equal(expected.OrderBy(static entry => entry.Key, StringComparer.Ordinal), actual.OrderBy(static entry => entry.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(typeof(DatabaseIdentity), "CreatedAtUtc", 0)]
    [InlineData(typeof(DatabaseIdentity), "CreatedAtUtc", 2)]
    [InlineData(typeof(DatabaseIdentity), "CreatedAtUtc", -7)]
    [InlineData(typeof(SchemaState), "UpdatedAtUtc", 0)]
    [InlineData(typeof(SchemaState), "UpdatedAtUtc", 2)]
    [InlineData(typeof(SchemaState), "UpdatedAtUtc", -7)]
    public void TimestampMappingNormalizesOffsetsWithoutChangingTheInstant(Type entityType, string propertyName, int offsetHours)
    {
        using RuntimeDbContext context = CreateContext();
        IProperty property = ReadModel(context).FindEntityType(entityType)!.FindProperty(propertyName)!;
        var converter = property.GetValueConverter()!;
        DateTimeOffset instant = new(2026, 1, 15, 12, 0, 0, TimeSpan.FromHours(offsetHours));
        DateTimeOffset stored = Assert.IsType<DateTimeOffset>(converter.ConvertToProvider(instant));
        DateTimeOffset read = Assert.IsType<DateTimeOffset>(converter.ConvertFromProvider(stored));
        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(TimeSpan.Zero, read.Offset);
        Assert.Equal(instant.UtcTicks, stored.UtcTicks);
        Assert.Equal(instant.UtcTicks, read.UtcTicks);
    }

    [Fact]
    public void ModelConstructionNeedsNoConnectionAndDoesNotTrackRows()
    {
        DbContextOptions<RuntimeDbContext> options = new DbContextOptionsBuilder<RuntimeDbContext>().UseNpgsql().Options;
        RelationalOptionsExtension relational = Assert.Single(options.Extensions.OfType<RelationalOptionsExtension>());
        Assert.Null(relational.Connection);
        Assert.Null(relational.ConnectionString);
        using RuntimeDbContext context = new(options);
        Assert.NotNull(context.Model);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void InitialMigrationAndSnapshotMatchTheCurrentModelWithoutAConnection()
    {
        using RuntimeDbContext context = CreateContext();
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        var registration = Assert.Single(assembly.Migrations);
        Assert.Equal("20260923093309_CreateRuntimeMetadata", registration.Key);
        Assert.NotNull(assembly.ModelSnapshot);
        Assert.False(context.Database.HasPendingModelChanges());
        Migration migration = assembly.CreateMigration(registration.Value, context.Database.ProviderName!);
        IModel target = context.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
        Assert.False(context.GetService<IMigrationsModelDiffer>().HasDifferences(target.GetRelationalModel(), ReadModel(context).GetRelationalModel()));
        Assert.Null(context.Database.GetConnectionString());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void InitialMigrationDeclaresOnlyTheRuntimeSchemaTablesAndIndexWithNoSeed()
    {
        using RuntimeDbContext context = CreateContext();
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        Migration migration = assembly.CreateMigration(Assert.Single(assembly.Migrations).Value, context.Database.ProviderName!);
        IModel model = ReadModel(context);
        IMigrationsSqlGenerator generator = context.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationOperation> expected = context.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        Assert.Equal(generator.Generate(expected, model).Select(static command => command.CommandText),
            generator.Generate(migration.UpOperations, model).Select(static command => command.CommandText));
        Assert.Equal("sitefactory_runtime", Assert.Single(migration.UpOperations.OfType<EnsureSchemaOperation>()).Name);
        Assert.Equal(["database_identity", "schema_state"], migration.UpOperations.OfType<CreateTableOperation>().Select(static table => table.Name));
        Assert.Equal(4, migration.UpOperations.Count);
        Assert.All(migration.UpOperations.OfType<CreateTableOperation>(), static table => Assert.Equal("sitefactory_runtime", table.Schema));
        CreateIndexOperation index = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>());
        Assert.Equal("ux_database_identity_singleton", index.Name);
        Assert.True(index.IsUnique);
        Assert.Equal(["singleton"], index.Columns);
        Assert.Equal(["database_identity", "schema_state"], migration.DownOperations.Select(static operation => Assert.IsType<DropTableOperation>(operation).Name));
        Assert.Null(context.Database.GetConnectionString());
    }

    private static void VerifyProperty(IEntityType entity, string table, string propertyName, string column, string storeType, int? length, Type clrType, bool nullable)
    {
        Assert.Equal(table, entity.GetTableName());
        IProperty property = entity.FindProperty(propertyName)!;
        Assert.NotNull(property);
        Assert.Equal(column, property.GetColumnName(StoreObjectIdentifier.Table(table, "sitefactory_runtime")));
        Assert.Equal(storeType, property.GetColumnType());
        Assert.Equal(length, property.GetMaxLength());
        Assert.Equal(clrType, property.ClrType);
        Assert.Equal(nullable, property.IsNullable);
    }
}
