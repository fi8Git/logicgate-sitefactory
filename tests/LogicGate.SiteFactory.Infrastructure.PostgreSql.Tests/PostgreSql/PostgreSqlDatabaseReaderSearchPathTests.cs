using LogicGate.SiteFactory.Runtime.Database;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "G05")]
[Collection(nameof(PostgreSqlMigrationTests))]
public sealed class PostgreSqlDatabaseReaderSearchPathTests
{
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see tests/PostgreSql/README.md.")]
    public async Task ExtractedReaderPreservesObservationsAndRefusalsOnHostileSearchPaths()
    {
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-reader-search-path-").FullName;
        try
        {
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string migrator = Path.Combine(sandbox, "migration-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, migrator, Token);
            await PostgreSqlCompatibilityTests.SetupAsync(migrator, bootstrap: false);
            await PostgreSqlCompatibilityTests.SetupAsync(migrator, bootstrap: true);
            // Same shadow aggregate, operator and type as the private regression. These really
            // resolve differently on the hostile connection: no forced safe search_path in the reader.
            await PostgreSqlSchemaPrivilegeCases.AdminAsync(fixture, """
                CREATE SCHEMA sf_corr002_shadow;
                CREATE FUNCTION sf_corr002_shadow.count_step(pg_catalog.int8) RETURNS pg_catalog.int8
                    LANGUAGE sql IMMUTABLE AS 'SELECT 1::pg_catalog.int8';
                CREATE AGGREGATE sf_corr002_shadow.count(*) (
                    SFUNC = sf_corr002_shadow.count_step, STYPE = pg_catalog.int8, INITCOND = '0');
                CREATE FUNCTION sf_corr002_shadow.equal_count(pg_catalog.int8, pg_catalog.int4)
                    RETURNS pg_catalog.bool LANGUAGE sql IMMUTABLE AS 'SELECT true';
                CREATE OPERATOR sf_corr002_shadow.= (
                    LEFTARG = pg_catalog.int8, RIGHTARG = pg_catalog.int4, FUNCTION = sf_corr002_shadow.equal_count);
                CREATE DOMAIN sf_corr002_shadow.text AS pg_catalog.text CHECK (false);
                """);
            foreach (PostgreSqlTestLogin login in new[] { PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker })
            {
                string original = Path.Combine(sandbox, "original-" + login);
                string shadow = Path.Combine(sandbox, "shadow-" + login);
                await fixture.WriteConnectionFileAsync(login, original, Token);
                NpgsqlConnectionStringBuilder settings = new(await File.ReadAllTextAsync(original, Token))
                {
                    SearchPath = "sf_corr002_shadow, pg_catalog",
                };
                await PostgreSqlRoleFixture.WritePrivateConnectionAsync(settings.ConnectionString, shadow, Token);
                await PostgreSqlSchemaPrivilegeCases.AdminAsync(fixture,
                    $"GRANT USAGE ON SCHEMA sf_corr002_shadow TO \"{fixture.Name(login)}\"");
                await fixture.ExecuteAsync(login, async (connection, token) =>
                {
                    await using NpgsqlCommand path = new("SET search_path = sf_corr002_shadow, pg_catalog", connection);
                    await path.ExecuteNonQueryAsync(token);
                    await using NpgsqlCommand witness = new("""
                        SELECT 2::pg_catalog.int8 = 1, 2::pg_catalog.int8 OPERATOR(pg_catalog.=) 1,
                            count(*), pg_catalog.count(*) FROM (VALUES (1), (2)) AS input(value)
                        """, connection);
                    await using NpgsqlDataReader reader = await witness.ExecuteReaderAsync(token);
                    Assert.True(await reader.ReadAsync(token));
                    Assert.True(reader.GetBoolean(0));
                    Assert.False(reader.GetBoolean(1));
                    Assert.Equal(1L, reader.GetInt64(2));
                    Assert.Equal(2L, reader.GetInt64(3));
                }, Token);

                string[] before = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token);
                RuntimeDatabaseObservation baseline = await PostgreSqlCompatibilityTests.ReadObservationAsync(original, Token, login.ToString());
                RuntimeDatabaseObservation hostile = await PostgreSqlCompatibilityTests.ReadObservationAsync(shadow, Token, login.ToString());
                Assert.Equal(DatabaseReadOutcome.Read, hostile.Outcome);
                Assert.Equal(baseline.Identities, hostile.Identities);
                Assert.Equal(baseline.Schemas, hostile.Schemas);
                Assert.Equal(baseline.Migrations, hostile.Migrations);
                Assert.Equal(baseline.Role!.Privileges, hostile.Role!.Privileges);
                Assert.Equal("sf_corr002_shadow, pg_catalog", hostile.Role.SearchPath);
                await CheckAsync(shadow, ["SF-DB-STARTUP-007"]);

                await PostgreSqlSchemaPrivilegeCases.AdminAsync(fixture,
                    $"GRANT CREATE ON SCHEMA sf_corr002_shadow TO \"{fixture.Name(login)}\"");
                RuntimeDatabaseObservation privileged = await PostgreSqlCompatibilityTests.ReadObservationAsync(shadow, Token, login.ToString());
                Assert.True(privileged.Role!.Privileges.HasFlag(DatabaseRolePrivileges.SchemaCreate));
                await CheckAsync(shadow, ["SF-DB-STARTUP-007", "SF-DB-STARTUP-008"]);
                await PostgreSqlSchemaPrivilegeCases.AdminAsync(fixture,
                    $"REVOKE CREATE ON SCHEMA sf_corr002_shadow FROM \"{fixture.Name(login)}\"");
                Assert.Equal(before, await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token));
                await PostgreSqlCompatibilityTests.AssertNoProbeSessionsAsync(fixture);

                async Task CheckAsync(string file, string[] codes)
                {
                    string[] snapshot = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token);
                    string[] results = await DatabaseCompatibilityIntegrationBridge.ProbeRealAsync(file, "example-client", "Test",
                        fixture.Name(login), login.ToString(), null, Token);
                    Assert.False(results.Any(value => value.Contains(settings.Password!, StringComparison.Ordinal)
                        || value.Contains(settings.ConnectionString, StringComparison.Ordinal)
                        || value.Contains(file, StringComparison.Ordinal)), "Private connection material disclosed.");
                    Assert.Equal(codes, results.Select(static result => result.Split('|')[0]));
                    Assert.Equal(snapshot, await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token));
                }
            }
        }
        finally
        {
            PostgreSqlCompatibilityTests.DeleteSandbox(sandbox, "sitefactory-reader-search-path-");
        }
    }
}
