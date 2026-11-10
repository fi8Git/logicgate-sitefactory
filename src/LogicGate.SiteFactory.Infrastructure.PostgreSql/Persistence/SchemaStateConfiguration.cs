using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

internal sealed class SchemaStateConfiguration : IEntityTypeConfiguration<SchemaState>
{
    public void Configure(EntityTypeBuilder<SchemaState> builder)
    {
        builder.ToTable("schema_state", "sitefactory_runtime", table =>
        {
            table.HasCheckConstraint("ck_schema_state_family", "length(btrim(schema_family)) > 0");
            table.HasCheckConstraint("ck_schema_state_version", "length(btrim(current_version)) > 0");
            table.HasCheckConstraint("ck_schema_state_last_migration", "last_migration_id IS NULL OR length(btrim(last_migration_id)) > 0");
            table.HasCheckConstraint("ck_schema_state_migration_state",
                "migration_state IN ('stable', 'migration-planned', 'migration-running', 'migration-failed', 'restore-pending', 'reconciliation-required')");
            table.HasCheckConstraint("ck_schema_state_operation_id", "updated_by_operation_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        builder.HasKey(state => state.SchemaFamily).HasName("pk_schema_state");
        builder.Property(state => state.SchemaFamily).HasColumnName("schema_family")
            .HasColumnType("character varying(100)").HasMaxLength(100).IsRequired().ValueGeneratedNever();
        builder.Property(state => state.CurrentVersion).HasColumnName("current_version")
            .HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(state => state.LastMigrationId).HasColumnName("last_migration_id")
            .HasColumnType("character varying(150)").HasMaxLength(150).IsRequired(false);
        builder.Property(state => state.MigrationState).HasColumnName("migration_state")
            .HasColumnType("character varying(32)").HasMaxLength(32).IsRequired();
        builder.Property(state => state.UpdatedAtUtc).HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone").IsRequired().ValueGeneratedNever()
            .HasConversion(value => value.ToUniversalTime(), value => value.ToUniversalTime());
        builder.Property(state => state.UpdatedByOperationId).HasColumnName("updated_by_operation_id")
            .HasColumnType("uuid").IsRequired().ValueGeneratedNever();
    }
}
