namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence;

/// <summary>Persisted database identity, supplied by explicit Migrator bootstrap, never by host startup.</summary>
public sealed class DatabaseIdentity
{
    public required Guid DatabaseInstanceId { get; init; }
    public required string ClientSlug { get; init; }
    public required string Environment { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required string ProvisioningVersion { get; init; }
    public required string DatabaseOwnerRoleId { get; init; }
}
