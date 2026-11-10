using LogicGate.SiteFactory.Runtime.Contracts;
using LogicGate.SiteFactory.Runtime.Database;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Tests;

public sealed class DatabaseCompatibilityTests
{
    private static readonly Guid Instance = new("11111111-1111-4111-8111-111111111111");
    private static readonly DatabaseRoleObservation Role = new("expected_web", "expected_web", true, false,
        "pg_catalog, sitefactory_runtime", DatabaseRolePrivileges.None);
    private static readonly DatabaseIdentityObservation Identity = new(Instance, "example-client", "Test");
    private static readonly DatabaseSchemaObservation Schema = new("platform", "1", DatabaseCompatibilityProbe.InitialMigration, "stable");

    private static DatabaseCompatibilityExpectation Expected(string component = "Web")
    {
        Assert.True(RuntimeConfiguration.TryCreate("example-client", "Test", Enum.Parse<RuntimeComponent>(component),
            "https://example.invalid", out RuntimeConfiguration? runtime, out _));
        return new(runtime.Identity, "expected_web", Instance);
    }

    private static RuntimeDatabaseObservation Good() => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration]);

    [Theory]
    [InlineData("Web")]
    [InlineData("Worker")]
    public void CompatibleRuntimeHasOneSafeSuccess(string component)
    {
        StartupValidationResult result = Assert.Single(DatabaseCompatibilityProbe.Evaluate(Expected(component), Good()));
        Assert.Equal(StartupValidationSeverity.Success, result.Severity);
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [InlineData("unavailable", "SF-DB-STARTUP-001")]
    [InlineData("denied", "SF-DB-STARTUP-009")]
    [InlineData("malformed", "SF-SCHEMA-STARTUP-009")]
    [InlineData("absent", "SF-DB-STARTUP-003")]
    [InlineData("client", "SF-DB-STARTUP-004")]
    [InlineData("environment", "SF-DB-STARTUP-005")]
    [InlineData("instance", "SF-DB-STARTUP-006")]
    [InlineData("family", "SF-SCHEMA-STARTUP-009")]
    [InlineData("version", "SF-SCHEMA-STARTUP-009")]
    [InlineData("state-absent", "SF-SCHEMA-STARTUP-001")]
    [InlineData("planned", "SF-SCHEMA-STARTUP-005")]
    [InlineData("running", "SF-SCHEMA-STARTUP-005")]
    [InlineData("failed", "SF-SCHEMA-STARTUP-006")]
    [InlineData("restore", "SF-SCHEMA-STARTUP-009")]
    [InlineData("pending", "SF-SCHEMA-STARTUP-004")]
    [InlineData("unknown", "SF-SCHEMA-STARTUP-007")]
    [InlineData("history-mismatch", "SF-SCHEMA-STARTUP-008")]
    [InlineData("login", "SF-DB-STARTUP-007")]
    [InlineData("session", "SF-DB-STARTUP-007")]
    [InlineData("search-path", "SF-DB-STARTUP-007")]
    [InlineData("inherit", "SF-DB-STARTUP-008")]
    public void DecisionUsesObservedFactsAndStableCodes(string scenario, string code)
    {
        RuntimeDatabaseObservation observation = scenario switch
        {
            "unavailable" => new(DatabaseReadOutcome.Unavailable),
            "denied" => new(DatabaseReadOutcome.PermissionDenied, Role),
            "malformed" => new(DatabaseReadOutcome.InvalidDocument, Role),
            "absent" => new(DatabaseReadOutcome.Read, Role, [], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "client" => new(DatabaseReadOutcome.Read, Role, [Identity with { ClientSlug = "private-observed-value" }], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "environment" => new(DatabaseReadOutcome.Read, Role, [Identity with { Environment = "Development" }], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "instance" => new(DatabaseReadOutcome.Read, Role, [Identity with { InstanceId = new("33333333-3333-4333-8333-333333333333") }], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "family" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema with { Family = "other" }], [DatabaseCompatibilityProbe.InitialMigration]),
            "version" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema with { Version = "opaque-future-version" }], [DatabaseCompatibilityProbe.InitialMigration]),
            "state-absent" => new(DatabaseReadOutcome.Read, Role, [Identity], [], [DatabaseCompatibilityProbe.InitialMigration]),
            "planned" or "running" or "failed" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema with { State = "migration-" + scenario }], [DatabaseCompatibilityProbe.InitialMigration]),
            "restore" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema with { State = "restore-pending" }], [DatabaseCompatibilityProbe.InitialMigration]),
            "pending" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema], []),
            "unknown" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration, "future"]),
            "history-mismatch" => new(DatabaseReadOutcome.Read, Role, [Identity], [Schema with { LastMigrationId = null }], [DatabaseCompatibilityProbe.InitialMigration]),
            "login" => new(DatabaseReadOutcome.Read, Role with { SessionUser = "different", CurrentUser = "different" }, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "session" => new(DatabaseReadOutcome.Read, Role with { CurrentUser = "owner" }, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "search-path" => new(DatabaseReadOutcome.Read, Role with { SearchPath = "public" }, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            "inherit" => new(DatabaseReadOutcome.Read, Role with { Inherit = true }, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        StartupValidationResult result = Assert.Single(DatabaseCompatibilityProbe.Evaluate(Expected(), observation));
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(StartupValidationSeverity.BlockingFailure, result.Severity);
        Assert.DoesNotContain("private-observed-value", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryCriticalPrivilegeIsRejectedWithoutGrantingAnExceptionForTheExpectedName()
    {
        foreach (DatabaseRolePrivileges privilege in Enum.GetValues<DatabaseRolePrivileges>().Where(static privilege => privilege != DatabaseRolePrivileges.None))
        {
            RuntimeDatabaseObservation observation = new(DatabaseReadOutcome.Read, Role with { Privileges = privilege }, [Identity], [Schema], [DatabaseCompatibilityProbe.InitialMigration]);
            Assert.Equal("SF-DB-STARTUP-008", Assert.Single(DatabaseCompatibilityProbe.Evaluate(Expected(), observation)).ErrorCode);
        }
    }

    [Fact]
    public void MultipleFailuresHaveStableOrderAndObservationsCopyTheirInputs()
    {
        DatabaseIdentityObservation[] identities = [Identity with { ClientSlug = "different", Environment = "Development" }];
        RuntimeDatabaseObservation observation = new(DatabaseReadOutcome.Read, Role, identities, [], []);
        identities[0] = Identity;
        IReadOnlyList<StartupValidationResult> first = DatabaseCompatibilityProbe.Evaluate(Expected(), observation);
        Assert.Equal(["SF-DB-STARTUP-004", "SF-DB-STARTUP-005", "SF-SCHEMA-STARTUP-001", "SF-SCHEMA-STARTUP-004"], first.Select(static result => result.ErrorCode));
        Assert.Equal(first, DatabaseCompatibilityProbe.Evaluate(Expected(), observation));
    }

    [Fact]
    public async Task CancellationIsNotConvertedToAnUnavailableDatabase()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        DatabaseCompatibilityProbe probe = new(new NeverRead());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.ValidateAsync(Expected(), cancellation.Token));
    }

    [Fact]
    public async Task ProgrammingErrorsFromThePortAreNotAbsorbed()
    {
        DatabaseCompatibilityProbe probe = new(new NeverRead());
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.ValidateAsync(Expected(), TestContext.Current.CancellationToken));
    }

    private sealed class NeverRead : IRuntimeDatabaseReader
    {
        public Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected reader invocation.");
    }

    [Fact]
    public async Task ValidateReadsOnceWithTheCallerTokenAndReturnsThePureDecision()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ControlledReader reader = new();
        DatabaseCompatibilityProbe probe = new(reader);
        Task<IReadOnlyList<StartupValidationResult>> validation = probe.ValidateAsync(Expected(), cancellation.Token);

        await reader.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        Assert.False(validation.IsCompleted);

        reader.Complete(Good());
        IReadOnlyList<StartupValidationResult> results = await validation;
        Assert.Equal(DatabaseCompatibilityProbe.Evaluate(Expected(), Good()), results);
        Assert.Equal(StartupValidationSeverity.Success, Assert.Single(results).Severity);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task CancellationDuringTheReadIsPropagatedWithoutPublishingAResult()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ControlledReader reader = new();
        DatabaseCompatibilityProbe probe = new(reader);
        Task<IReadOnlyList<StartupValidationResult>> validation = probe.ValidateAsync(Expected(), cancellation.Token);

        await reader.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        await cancellation.CancelAsync();
        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validation);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task CancellationAfterTheReadIsNotConvertedToSuccess()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellingReader reader = new(cancellation);
        DatabaseCompatibilityProbe probe = new(reader);
        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => probe.ValidateAsync(Expected(), cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public void AllObservationCollectionsAreDefensiveReadOnlySnapshots()
    {
        DatabaseIdentityObservation[] identities = [Identity];
        DatabaseSchemaObservation[] schemas = [Schema];
        string[] migrations = [DatabaseCompatibilityProbe.InitialMigration];
        RuntimeDatabaseObservation observation = new(DatabaseReadOutcome.Read, Role, identities, schemas, migrations);
        identities[0] = Identity with { ClientSlug = "changed" };
        schemas[0] = Schema with { Version = "changed" };
        migrations[0] = "changed";

        Assert.Equal(Identity, Assert.Single(observation.Identities));
        Assert.Equal(Schema, Assert.Single(observation.Schemas));
        Assert.Equal(DatabaseCompatibilityProbe.InitialMigration, Assert.Single(observation.Migrations));
        Assert.Throws<NotSupportedException>(() => ((IList<DatabaseIdentityObservation>)observation.Identities)[0] = identities[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<DatabaseSchemaObservation>)observation.Schemas)[0] = schemas[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)observation.Migrations)[0] = migrations[0]);
    }

    private sealed class ControlledReader : IRuntimeDatabaseReader
    {
        private readonly TaskCompletionSource<RuntimeDatabaseObservation> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public async Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            ReceivedToken = cancellationToken;
            Entered.SetResult();
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Complete(RuntimeDatabaseObservation observation) => completion.SetResult(observation);
    }

    private sealed class CancellingReader(CancellationTokenSource cancellation) : IRuntimeDatabaseReader
    {
        public async Task<RuntimeDatabaseObservation> ReadAsync(CancellationToken cancellationToken)
        {
            Assert.Equal(cancellation.Token, cancellationToken);
            await cancellation.CancelAsync();
            return Good();
        }
    }
}
