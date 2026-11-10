using Microsoft.EntityFrameworkCore;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

/// <summary>Runtime metadata model. Configuration, provisioning and migrations remain explicit caller responsibilities.</summary>
public sealed class RuntimeDbContext(DbContextOptions<RuntimeDbContext> options) : DbContext(options)
{
    public DbSet<DatabaseIdentity> DatabaseIdentities => Set<DatabaseIdentity>();
    public DbSet<SchemaState> SchemaStates => Set<SchemaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema("sitefactory_runtime");
        modelBuilder.ApplyConfiguration(new DatabaseIdentityConfiguration());
        modelBuilder.ApplyConfiguration(new SchemaStateConfiguration());
    }
}
