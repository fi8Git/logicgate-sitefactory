using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

internal sealed class DatabaseIdentityConfiguration : IEntityTypeConfiguration<DatabaseIdentity>
{
    public void Configure(EntityTypeBuilder<DatabaseIdentity> builder)
    {
        builder.ToTable("database_identity", "sitefactory_runtime", table =>
        {
            table.HasCheckConstraint("ck_database_identity_singleton", "singleton = TRUE");
            table.HasCheckConstraint("ck_database_identity_instance_id", "database_instance_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_database_identity_client_slug", "length(btrim(client_slug)) > 0");
            table.HasCheckConstraint("ck_database_identity_environment", "length(btrim(environment)) > 0");
            table.HasCheckConstraint("ck_database_identity_provisioning_version", "length(btrim(provisioning_version)) > 0");
            table.HasCheckConstraint("ck_database_identity_owner_role", "length(btrim(database_owner_role_id)) > 0");
        });
        builder.HasKey(identity => identity.DatabaseInstanceId).HasName("pk_database_identity");
        builder.Property(identity => identity.DatabaseInstanceId).HasColumnName("database_instance_id")
            .HasColumnType("uuid").IsRequired().ValueGeneratedNever();

        // A unique, required TRUE marker allows at most one identity in this database (ADR-0031 §36.2).
        builder.Property<bool>("Singleton").HasColumnName("singleton").HasColumnType("boolean")
            .IsRequired().HasDefaultValue(true);
        builder.HasIndex("Singleton").IsUnique().HasDatabaseName("ux_database_identity_singleton");

        builder.Property(identity => identity.ClientSlug).HasColumnName("client_slug")
            .HasColumnType("character varying(80)").HasMaxLength(80).IsRequired();
        builder.Property(identity => identity.Environment).HasColumnName("environment")
            .HasColumnType("character varying(32)").HasMaxLength(32).IsRequired();
        builder.Property(identity => identity.ProvisioningVersion).HasColumnName("provisioning_version")
            .HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(identity => identity.DatabaseOwnerRoleId).HasColumnName("database_owner_role_id")
            .HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(identity => identity.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone").IsRequired().ValueGeneratedNever()
            .HasConversion(value => value.ToUniversalTime(), value => value.ToUniversalTime());
    }
}
