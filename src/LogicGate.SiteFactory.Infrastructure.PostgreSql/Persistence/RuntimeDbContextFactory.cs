using LogicGate.SiteFactory.Infrastructure.PostgreSql.Configuration;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

/// <summary>Builds an independent context offline from an already validated configuration.</summary>
public static class RuntimeDbContextFactory
{
    /// <summary>The caller must dispose the context, which owns its dedicated connection.</summary>
    public static RuntimeDbContext Create(PostgreSqlRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        NpgsqlConnection connection = configuration.CreateConnection();
        RuntimeDbContext? context = null;
        try
        {
            DbContextOptions<RuntimeDbContext> options = new DbContextOptionsBuilder<RuntimeDbContext>()
                .UseNpgsql(connection, contextOwnsConnection: true,
                    options => options.MigrationsHistoryTable("__EFMigrationsHistory", "sitefactory_runtime"))
                .Options;
            context = new RuntimeDbContext(options);
            // Initialize EF's connection lifetime offline, even if the caller never otherwise uses the context.
            _ = context.Database.GetDbConnection();
            return context;
        }
        catch
        {
            // Cleanup only: programming failures remain failures, never user diagnostics or credential fallbacks.
            try { context?.Dispose(); }
            finally { connection.Dispose(); }
            throw;
        }
    }
}
