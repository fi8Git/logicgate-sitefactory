namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

/// <summary>One persisted version/state per schema family; no migration runner or compatibility policy.</summary>
public sealed class SchemaState
{
    public required string SchemaFamily { get; init; }
    public required string CurrentVersion { get; init; }
    public string? LastMigrationId { get; init; }
    public required string MigrationState { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public required Guid UpdatedByOperationId { get; init; }
}
