namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

internal static class PostgreSqlTestGate
{
    internal static IReadOnlyList<string> ExpectedTests { get; } = Array.AsReadOnly(new[]
    {
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlMigrationTests.ExtractedMigratorAppliesOnceWithItsOwnLoginAndRejectsRuntimeCredentials",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlMigrationTests.ExtractedMigratorPhysicallyClosesElevatedSessionsInsideTheLiveTestHost",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlMigratorRoleGraphTests.ExtractedMigratorRejectsAnyRoleGraphOutsideItsSingleOwnerEdge",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlMigratorSearchPathCases.ExtractedMigratorRejectsHostileSearchPathBeforeApplyAndBootstrapMutation",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlBootstrapTests.ExtractedBootstrapIsTransactionalIdempotentAndRefusesMismatchesAndRuntimeWrites",
    });

    internal static IReadOnlyList<string> ExpectedG05Tests { get; } = Array.AsReadOnly(new[]
    {
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlCompatibilityTests.ExtractedReaderAndProbeDistinguishCompatibilityFailuresWithoutChangingPostgreSql",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlCompatibilityTests.ExtractedReaderRejectsCreateOnNonSystemPgSchemasForWebAndWorker",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlDatabaseReaderSearchPathTests.ExtractedReaderPreservesObservationsAndRefusalsOnHostileSearchPaths",
    });

    internal static IReadOnlyList<string> ExpectedP01Tests { get; } = Array.AsReadOnly(new[]
    {
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.RoleAttributesAndMembershipAreLeastPrivilege",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.MigratorRequiresExplicitOwnerAndResetsItsIdentity",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.WebCanReadButCannotChangeRuntimeSchema",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.WorkerCanReadButCannotChangeRuntimeSchema",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.PublicPrivilegesAndDatabaseIsolationAreExplicit",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.FutureOwnerObjectsReceiveOnlyExpectedDefaultPrivileges",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.ControlledSearchPathIgnoresPublicHomonyms",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlRoleTests.RoleCleanupSurvivesFailureAndCancellation",
    });

    // Literal names keep this source-linked configuration gate independent of P02 test types.
    internal static IReadOnlyList<string> ExpectedP02Tests { get; } = Array.AsReadOnly(new[]
    {
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlCatalogSnapshotTests.CatalogSnapshotExcludesOnlyLiteralSystemSchemaPrefixes",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlIntegrationTests.RealPostgreSqlPersistsDataEnforcesConstraintsAndIsolatesDatabases",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlIntegrationTests.CancellationDoesNotPreventCleanupOrCreateAnotherDatabase",
        "LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql.PostgreSqlPermissionMatrixTests.GeneratedMetadataPermissionsAreEnforcedByPostgreSqlForEveryLogin",
    });

    internal static void ValidateInventories(IEnumerable<string> g04, IEnumerable<string> g05,
        IEnumerable<string> p01, IEnumerable<string> p02, IEnumerable<string> all)
    {
        string[] first = g04.ToArray(), second = g05.ToArray(), third = p01.ToArray(), fourth = p02.ToArray(), global = all.ToArray();
        string[] expectedUnion = ExpectedTests.Concat(ExpectedG05Tests).Concat(ExpectedP01Tests).Concat(ExpectedP02Tests).ToArray();
        if (!Exact(first, ExpectedTests) || !Exact(second, ExpectedG05Tests) || !Exact(third, ExpectedP01Tests) || !Exact(fourth, ExpectedP02Tests) ||
            first.Intersect(second, StringComparer.Ordinal).Any() || first.Intersect(third, StringComparer.Ordinal).Any() ||
            first.Intersect(fourth, StringComparer.Ordinal).Any() || second.Intersect(third, StringComparer.Ordinal).Any() ||
            second.Intersect(fourth, StringComparer.Ordinal).Any() || third.Intersect(fourth, StringComparer.Ordinal).Any() ||
            expectedUnion.Distinct(StringComparer.Ordinal).Count() != 20 ||
            !Exact(global, expectedUnion))
            throw new InvalidOperationException("SF-TEST-PG-INVENTORY The fixed, disjoint 5/3/8/4 inventories and their union must match exactly.");
    }

    private static bool Exact(string[] actual, IReadOnlyList<string> expected) =>
        actual.Length == expected.Count && actual.Distinct(StringComparer.Ordinal).Count() == actual.Length &&
        actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    internal static bool IsConfiguredOrRequired => ShouldRun(
        Environment.GetEnvironmentVariable(PostgreSqlTestConfiguration.FileVariable),
        Environment.GetEnvironmentVariable(PostgreSqlTestConfiguration.RequiredVariable));

    // A malformed or whitespace-only reference/flag must execute and fail, never silently skip.
    internal static bool ShouldRun(string? file, string? required) => !string.IsNullOrEmpty(file) || !string.IsNullOrEmpty(required);

    internal static Task<PostgreSqlTestConfiguration> ReadAsync(CancellationToken cancellationToken) => ReadAsync(
        Environment.GetEnvironmentVariable(PostgreSqlTestConfiguration.FileVariable),
        Environment.GetEnvironmentVariable(PostgreSqlTestConfiguration.RequiredVariable), cancellationToken);

    internal static Task<PostgreSqlTestConfiguration> ReadAsync(string? file, string? required, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrEmpty(required) && required != "1")
            throw PostgreSqlTestConfiguration.Error("SF-TEST-PG-REQUIRED The required flag must be exactly 1 or absent.");
        return PostgreSqlTestConfiguration.ReadAsync(file, cancellationToken);
    }
}
