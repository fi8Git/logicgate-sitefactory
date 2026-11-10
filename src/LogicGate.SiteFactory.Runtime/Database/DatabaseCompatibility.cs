using LogicGate.SiteFactory.Runtime.Contracts;

namespace LogicGate.SiteFactory.Runtime.Database;

public enum DatabaseReadOutcome { Read, Unavailable, PermissionDenied, InvalidDocument }

[Flags]
public enum DatabaseRolePrivileges
{
    None = 0, Superuser = 1, CreateDatabase = 2, CreateRole = 4, Replication = 8, BypassRls = 16,
    Membership = 32, Ownership = 64, DatabaseCreate = 128, Temporary = 256, SchemaCreate = 512,
    MetadataWrite = 1024, Delegation = 2048,
}

public sealed record DatabaseRoleObservation(string SessionUser, string CurrentUser, bool CanLogin,
    bool Inherit, string SearchPath, DatabaseRolePrivileges Privileges);
public sealed record DatabaseIdentityObservation(Guid InstanceId, string ClientSlug, string Environment);
public sealed record DatabaseSchemaObservation(string Family, string Version, string? LastMigrationId, string State);

/// <summary>Bounded observations only: no provider, connection, exception or secret.</summary>
public sealed class RuntimeDatabaseObservation
{
    public RuntimeDatabaseObservation(DatabaseReadOutcome outcome, DatabaseRoleObservation? role = null,
        IEnumerable<DatabaseIdentityObservation>? identities = null, IEnumerable<DatabaseSchemaObservation>? schemas = null,
        IEnumerable<string>? migrations = null)
    {
        Outcome = outcome;
        Role = role;
        Identities = Array.AsReadOnly(identities?.ToArray() ?? []);
        Schemas = Array.AsReadOnly(schemas?.ToArray() ?? []);
        Migrations = Array.AsReadOnly(migrations?.ToArray() ?? []);
    }

    public DatabaseReadOutcome Outcome { get; }
    public DatabaseRoleObservation? Role { get; }
    public IReadOnlyList<DatabaseIdentityObservation> Identities { get; }
    public IReadOnlyList<DatabaseSchemaObservation> Schemas { get; }
    public IReadOnlyList<string> Migrations { get; }
}

/// <summary>The login name is independently expected, never inferred from the supplied credential.</summary>
public sealed class DatabaseCompatibilityExpectation
{
    public DatabaseCompatibilityExpectation(RuntimeIdentity identity, string loginRole, Guid? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(loginRole);
        if (instanceId == Guid.Empty) throw new ArgumentException("An expected instance ID cannot be empty.", nameof(instanceId));
        Identity = identity;
        LoginRole = loginRole;
        InstanceId = instanceId;
    }

    public RuntimeIdentity Identity { get; }
    public string LoginRole { get; }
    public Guid? InstanceId { get; }
}

public interface IRuntimeDatabaseReader
{
    Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Pure initial compatibility policy plus a read-only port. Not registered with hosts by SF-046.</summary>
public sealed class DatabaseCompatibilityProbe(IRuntimeDatabaseReader reader)
{
    public const string SchemaFamily = "platform";
    public const string SchemaVersion = "1";
    public const string InitialMigration = "20260923093309_CreateRuntimeMetadata";
    public static StartupValidationId Id { get; } = new("startup.database.compatibility");

    public async Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(DatabaseCompatibilityExpectation expected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeDatabaseObservation observed = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return Evaluate(expected, observed);
    }

    public static IReadOnlyList<StartupValidationResult> Evaluate(DatabaseCompatibilityExpectation expected, RuntimeDatabaseObservation observed)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        if (observed.Outcome == DatabaseReadOutcome.Unavailable)
            return Failure("SF-DB-STARTUP-001", "PostgreSQL could not be checked within the allowed time.");

        List<StartupValidationResult> errors = [];
        DatabaseRoleObservation? role = observed.Role;
        if (role is not null)
        {
            if (!role.CanLogin || role.SessionUser != expected.LoginRole || role.CurrentUser != role.SessionUser
                || role.CurrentUser == "postgres" || role.SearchPath != "pg_catalog, sitefactory_runtime")
                errors.Add(Error("SF-DB-STARTUP-007", "The PostgreSQL login or session does not match the expected runtime profile."));
            // SF-043 baseline: no memberships, ownership, DDL, DML, delegation or implicit privileges.
            if (role.Privileges != DatabaseRolePrivileges.None || role.Inherit)
                errors.Add(Error("SF-DB-STARTUP-008", "The PostgreSQL role has privileges outside the initial runtime profile."));
        }
        if (errors.Count != 0) return errors.AsReadOnly();
        if (observed.Outcome == DatabaseReadOutcome.PermissionDenied)
            return Failure("SF-DB-STARTUP-009", "A required metadata read privilege is missing.");
        if (observed.Outcome == DatabaseReadOutcome.InvalidDocument)
            return Failure("SF-SCHEMA-STARTUP-009", "The runtime metadata schema is incompatible.");
        if (observed.Outcome != DatabaseReadOutcome.Read || role is null)
            throw new InvalidOperationException("The database reader returned an invalid observation contract.");

        if (observed.Identities.Count == 0)
            errors.Add(Error("SF-DB-STARTUP-003", "Database identity is absent."));
        else if (observed.Identities.Count != 1 || observed.Identities[0].InstanceId == Guid.Empty)
            errors.Add(Error("SF-SCHEMA-STARTUP-009", "The runtime metadata schema is incompatible."));
        else
        {
            DatabaseIdentityObservation identity = observed.Identities[0];
            if (identity.ClientSlug != expected.Identity.ClientSlug)
                errors.Add(Error("SF-DB-STARTUP-004", "Database identity belongs to a different client."));
            if (identity.Environment != expected.Identity.Environment.ToString())
                errors.Add(Error("SF-DB-STARTUP-005", "Database identity belongs to a different environment."));
            if (expected.InstanceId is Guid instance && instance != identity.InstanceId)
                errors.Add(Error("SF-DB-STARTUP-006", "Database instance does not match the expected identity."));
        }

        if (observed.Schemas.Count == 0)
            errors.Add(Error("SF-SCHEMA-STARTUP-001", "Runtime schema state is absent."));
        else if (observed.Schemas.Count != 1 || observed.Schemas[0].Family != SchemaFamily || observed.Schemas[0].Version != SchemaVersion)
            errors.Add(Error("SF-SCHEMA-STARTUP-009", "The runtime metadata schema is incompatible."));
        else
        {
            DatabaseSchemaObservation schema = observed.Schemas[0];
            string? code = schema.State switch
            {
                "stable" => null,
                "migration-planned" or "migration-running" => "SF-SCHEMA-STARTUP-005",
                "migration-failed" => "SF-SCHEMA-STARTUP-006",
                _ => "SF-SCHEMA-STARTUP-009",
            };
            if (code is not null) errors.Add(Error(code, "The schema migration state does not permit normal runtime operation."));
            if (schema.LastMigrationId != InitialMigration)
                errors.Add(Error("SF-SCHEMA-STARTUP-008", "Schema state and expected migration history are inconsistent."));
        }
        if (!observed.Migrations.Contains(InitialMigration, StringComparer.Ordinal))
            errors.Add(Error("SF-SCHEMA-STARTUP-004", "The expected database migration has not been applied."));
        if (observed.Migrations.Any(static migration => migration != InitialMigration))
            errors.Add(Error("SF-SCHEMA-STARTUP-007", "The database contains an unknown migration."));
        return errors.Count == 0 ? [new(Id, StartupValidationSeverity.Success, null, "PostgreSQL runtime compatibility is valid.")]
            : errors.AsReadOnly();
    }

    private static StartupValidationResult Error(string code, string summary) => new(Id, StartupValidationSeverity.BlockingFailure, code, summary, "PostgreSql");
    private static IReadOnlyList<StartupValidationResult> Failure(string code, string summary) => [Error(code, summary)];
}
