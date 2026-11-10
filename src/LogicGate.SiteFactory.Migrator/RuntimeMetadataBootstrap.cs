using System.Data;
using System.Data.Common;
using LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogicGate.SiteFactory.Migrator;

internal sealed record MetadataBootstrapRequest(Guid DatabaseInstanceId, string ClientSlug, string Environment, Guid OperationId);

/// <summary>One explicit initial provisioning transaction; never a repair or upgrade.</summary>
internal static class RuntimeMetadataBootstrap
{
    internal const string Usage = "LogicGate.SiteFactory.Migrator bootstrap --environment <Development|Test> --client-slug <slug> --database-instance-id <uuid> --operation-id <uuid> --confirm-bootstrap";
    private const string InvalidState = "SF-BOOTSTRAP-STATE-001 Existing runtime metadata is incomplete or incompatible; no repair was performed.";
    private const string InvalidHistory = "SF-BOOTSTRAP-HISTORY-001 Apply the exact initial migration before bootstrap.";

    internal static MetadataBootstrapRequest? Parse(string[] args)
    {
        if (args is not ["bootstrap", "--environment", "Development" or "Test", "--client-slug", var slug,
            "--database-instance-id", var instance, "--operation-id", var operation, "--confirm-bootstrap"]
            || !Guid.TryParseExact(instance, "D", out Guid instanceId) || instanceId == Guid.Empty
            || !Guid.TryParseExact(operation, "D", out Guid operationId) || operationId == Guid.Empty
            || !IsValidSlug(slug)) return null;
        return new(instanceId, slug, args[2], operationId);
    }

    // Same lexical contract as runtime/structural configuration; no synthetic host configuration or URL.
    private static bool IsValidSlug(string value) => value is { Length: >= 3 and <= 80 }
        && value[0] != '-' && value[^1] != '-' && !value.Contains("--", StringComparison.Ordinal)
        && value is not ("con" or "prn" or "aux" or "nul")
        && !(value.Length == 4 && (value.StartsWith("com", StringComparison.Ordinal) || value.StartsWith("lpt", StringComparison.Ordinal))
            && value[3] is >= '1' and <= '9')
        && value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    internal static async Task<string?> ExecuteAsync(RuntimeDbContext context, MetadataBootstrapRequest request,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        // No history/table creation here, including on a database where apply has never run.
        if (!await HasExpectedHistoryAsync(context, cancellationToken).ConfigureAwait(false)) return InvalidHistory;

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        // Operator-only, short transaction (ADR-0028 §46). Also serializes two bootstraps of empty tables.
        // Fixed ordering, bounded wait; ordinary SELECT remains available to Web/Worker.
        await context.Database.ExecuteSqlRawAsync("""
            SET LOCAL lock_timeout = '3s';
            SET LOCAL statement_timeout = '10s';
            LOCK TABLE sitefactory_runtime."__EFMigrationsHistory",
                sitefactory_runtime.database_identity, sitefactory_runtime.schema_state IN SHARE ROW EXCLUSIVE MODE;
            """, cancellationToken).ConfigureAwait(false);
        if (!await HasExpectedHistoryAsync(context, cancellationToken).ConfigureAwait(false)) return InvalidHistory;

        await using DbCommand ownerQuery = context.Database.GetDbConnection().CreateCommand();
        ownerQuery.Transaction = transaction.GetDbTransaction();
        ownerQuery.CommandText = "SELECT current_user::pg_catalog.text";
        // MigrationCommand has already authenticated a dedicated login and assumed the verified NOLOGIN owner.
        string owner = (string)(await ownerQuery.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        DatabaseIdentity[] identities = await context.DatabaseIdentities.AsNoTracking().Take(2).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        SchemaState[] states = await context.SchemaStates.AsNoTracking().Take(2).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (identities.Length != 0 || states.Length != 0)
        {
            if (identities.Length != 1 || states.Length != 1) return InvalidState;
            DatabaseIdentity identity = identities[0];
            SchemaState state = states[0];
            if (identity.ClientSlug != request.ClientSlug)
                return "SF-BOOTSTRAP-CLIENT-001 Existing database client does not match the requested identity.";
            if (identity.Environment != request.Environment)
                return "SF-BOOTSTRAP-ENVIRONMENT-001 Existing database environment does not match the requested identity.";
            if (identity.DatabaseInstanceId != request.DatabaseInstanceId)
                return "SF-BOOTSTRAP-INSTANCE-001 Existing database instance does not match the requested identity.";
            if (identity.ProvisioningVersion != "1" || identity.DatabaseOwnerRoleId != owner
                || state.SchemaFamily != "platform" || state.CurrentVersion != "1"
                || state.LastMigrationId != MigrationCommand.InitialMigration || state.MigrationState != "stable"
                || state.UpdatedByOperationId == Guid.Empty || state.UpdatedAtUtc != identity.CreatedAtUtc)
                return InvalidState;
            // New invocation IDs/times must not rewrite the evidence of the original bootstrap.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        context.DatabaseIdentities.Add(new()
        {
            DatabaseInstanceId = request.DatabaseInstanceId,
            ClientSlug = request.ClientSlug,
            Environment = request.Environment,
            CreatedAtUtc = now,
            ProvisioningVersion = "1",
            DatabaseOwnerRoleId = owner,
        }).Property<bool>("Singleton").CurrentValue = true;
        // Two documented stages within ONE transaction. A failure of SchemaState rolls back identity too.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.SchemaStates.Add(new()
        {
            SchemaFamily = "platform",
            CurrentVersion = "1",
            LastMigrationId = MigrationCommand.InitialMigration,
            MigrationState = "stable",
            UpdatedAtUtc = now,
            UpdatedByOperationId = request.OperationId,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
        // await using rolls back the entire transaction on any early refusal, provider failure or cancellation.
    }

    private static async Task<bool> HasExpectedHistoryAsync(RuntimeDbContext context, CancellationToken cancellationToken) =>
        (await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false))
            .SequenceEqual([MigrationCommand.InitialMigration], StringComparer.Ordinal);
}
