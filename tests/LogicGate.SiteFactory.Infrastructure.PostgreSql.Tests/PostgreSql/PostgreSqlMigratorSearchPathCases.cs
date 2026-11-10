using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Collection(nameof(PostgreSqlMigrationTests))]
public sealed class PostgreSqlMigratorSearchPathCases
{
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see docs/extraction/SF-TASK-062-G01-allocation.md.")]
    public async Task ExtractedMigratorRejectsHostileSearchPathBeforeApplyAndBootstrapMutation()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        CancellationToken token = TestContext.Current.CancellationToken;
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-migrator-search-path-").FullName;
        try
        {
            await VerifyAsync(configuration, sandbox, sandbox, token);
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-migrator-search-path-", Path.GetFileName(sandbox), StringComparison.Ordinal);
            Directory.Delete(sandbox, recursive: true);
        }
    }

    internal static async Task VerifyAsync(PostgreSqlTestConfiguration configuration, string project, string sandbox, CancellationToken token)
    {
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, token);
        string migrator = fixture.Baseline.Migrator;
        string extra = migrator[..^"migrator".Length] + "shadow_admin";
        string originalFile = Path.Combine(sandbox, "shadow-original-connection");
        string shadowFile = Path.Combine(sandbox, "shadow-migrator-connection");
        string catalogFile = Path.Combine(sandbox, "catalog-migrator-connection");
        await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, originalFile, token);
        NpgsqlConnectionStringBuilder settings = new(await File.ReadAllTextAsync(originalFile, token))
        {
            SearchPath = "sf_corr002_shadow, pg_catalog",
        };
        await PostgreSqlRoleFixture.WritePrivateConnectionAsync(settings.ConnectionString, shadowFile, token);
        settings.SearchPath = "pg_catalog";
        await PostgreSqlRoleFixture.WritePrivateConnectionAsync(settings.ConnectionString, catalogFile, token);

        await AdminAsync(fixture, $"""
            CREATE ROLE "{extra}" NOLOGIN NOINHERIT NOSUPERUSER CREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
            CREATE SCHEMA sf_corr002_shadow;
            CREATE FUNCTION sf_corr002_shadow.count_step(pg_catalog.int8) RETURNS pg_catalog.int8
                LANGUAGE sql IMMUTABLE AS 'SELECT 1::pg_catalog.int8';
            CREATE AGGREGATE sf_corr002_shadow.count(*) (
                SFUNC = sf_corr002_shadow.count_step, STYPE = pg_catalog.int8, INITCOND = '0');
            GRANT USAGE ON SCHEMA sf_corr002_shadow TO "{migrator}", "{fixture.Baseline.Owner}";
            GRANT "{extra}" TO "{migrator}" WITH SET TRUE, INHERIT FALSE, ADMIN FALSE;
            """, token);
        try
        {
            // The real login can SET ROLE into CREATEDB and really resolves the malicious aggregate.
            await fixture.ExecuteAsync(PostgreSqlTestLogin.Migrator, async (connection, cancellationToken) =>
            {
                await using NpgsqlCommand path = new("SET search_path = sf_corr002_shadow, pg_catalog", connection);
                await path.ExecuteNonQueryAsync(cancellationToken);
                await using NpgsqlCommand witness = new("""
                    SELECT pg_catalog.count(*), count(*) FROM pg_catalog.pg_auth_members
                    WHERE member OPERATOR(pg_catalog.=) (SELECT oid FROM pg_catalog.pg_roles WHERE rolname OPERATOR(pg_catalog.=) session_user)
                    """, connection);
                await using (NpgsqlDataReader reader = await witness.ExecuteReaderAsync(cancellationToken))
                {
                    Assert.True(await reader.ReadAsync(cancellationToken));
                    Assert.Equal(2L, reader.GetInt64(0));
                    Assert.Equal(1L, reader.GetInt64(1));
                }
                await using NpgsqlCommand elevate = new($"SET ROLE \"{extra}\"", connection);
                await elevate.ExecuteNonQueryAsync(cancellationToken);
                await using NpgsqlCommand attribute = new("SELECT rolcreatedb FROM pg_catalog.pg_roles WHERE rolname OPERATOR(pg_catalog.=) current_user", connection);
                Assert.Equal(true, await attribute.ExecuteScalarAsync(cancellationToken));
                await using NpgsqlCommand reset = new("RESET ROLE", connection);
                await reset.ExecuteNonQueryAsync(cancellationToken);
            }, token);

            string[] beforeApply = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, false, token);
            MigratorProcessResult apply = await PostgreSqlMigrationTests.ApplyAsync(project, shadowFile);
            string[] afterApply = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, false, token);

            // Prepare an actually migrated database for the second entrypoint with only the allowed edge.
            await AdminAsync(fixture, $"REVOKE \"{extra}\" FROM \"{migrator}\"", token);
            AssertSuccess(await PostgreSqlMigrationTests.ApplyAsync(project, catalogFile), "Runtime metadata migration completed.");
            await AdminAsync(fixture, $"GRANT \"{extra}\" TO \"{migrator}\" WITH SET TRUE, INHERIT FALSE, ADMIN FALSE", token);
            string[] beforeBootstrap = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, token);
            MigratorProcessResult bootstrap = await PostgreSqlBootstrapTests.BootstrapAsync(project, shadowFile);
            string[] afterBootstrap = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, token);

            // Both real commands are exercised before asserting rejection: the old bug is observable on both.
            bool applyUnchanged = beforeApply.SequenceEqual(afterApply, StringComparer.Ordinal);
            bool bootstrapUnchanged = beforeBootstrap.SequenceEqual(afterBootstrap, StringComparer.Ordinal);
            Assert.True(apply.ExitCode == 1 && bootstrap.ExitCode == 1,
                $"SF-CORR002-REPRO catalogCount=2 shadowCount=1 applyExit={apply.ExitCode} bootstrapExit={bootstrap.ExitCode} applyUnchanged={applyUnchanged} bootstrapUnchanged={bootstrapUnchanged}");
            AssertRefused(apply);
            AssertRefused(bootstrap);
            Assert.True(applyUnchanged);
            Assert.True(bootstrapUnchanged);

            // Qualifying count alone is insufficient: operator and type names also follow search_path.
            await AdminAsync(fixture, """
                CREATE FUNCTION sf_corr002_shadow.equal_count(pg_catalog.int8, pg_catalog.int4)
                    RETURNS pg_catalog.bool LANGUAGE sql IMMUTABLE AS 'SELECT true';
                CREATE OPERATOR sf_corr002_shadow.= (
                    LEFTARG = pg_catalog.int8, RIGHTARG = pg_catalog.int4, FUNCTION = sf_corr002_shadow.equal_count);
                CREATE DOMAIN sf_corr002_shadow.text AS pg_catalog.text CHECK (false);
                """, token);
            await fixture.ExecuteAsync(PostgreSqlTestLogin.Migrator, async (connection, cancellationToken) =>
            {
                await using NpgsqlCommand path = new("SET search_path = sf_corr002_shadow, pg_catalog", connection);
                await path.ExecuteNonQueryAsync(cancellationToken);
                await using NpgsqlCommand witness = new("""
                    SELECT 2::pg_catalog.int8 = 1, 2::pg_catalog.int8 OPERATOR(pg_catalog.=) 1
                    """, connection);
                await using (NpgsqlDataReader reader = await witness.ExecuteReaderAsync(cancellationToken))
                {
                    Assert.True(await reader.ReadAsync(cancellationToken));
                    Assert.True(reader.GetBoolean(0));
                    Assert.False(reader.GetBoolean(1));
                }
                // The hostile type is active too, not merely declared in a catalog snapshot.
                await using NpgsqlCommand typeWitness = new("SELECT current_user::text", connection);
                PostgresException denied = await Assert.ThrowsAsync<PostgresException>(async () =>
                    await typeWitness.ExecuteScalarAsync(cancellationToken));
                Assert.Equal(PostgresErrorCodes.CheckViolation, denied.SqlState);
                await using NpgsqlCommand qualifiedType = new("SELECT current_user::pg_catalog.text", connection);
                Assert.Equal(migrator, await qualifiedType.ExecuteScalarAsync(cancellationToken));
            }, token);
            string[] beforeOperators = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, token);
            AssertRefused(await PostgreSqlMigrationTests.ApplyAsync(project, shadowFile));
            AssertRefused(await PostgreSqlBootstrapTests.BootstrapAsync(project, shadowFile));
            Assert.True(beforeOperators.AsEnumerable().SequenceEqual(await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, token), StringComparer.Ordinal), "SF-TEST-PG-PROOF Catalog changed.");

            await AdminAsync(fixture, $"REVOKE \"{extra}\" FROM \"{migrator}\"", token);
            foreach (string file in new[] { shadowFile, catalogFile })
            {
                AssertSuccess(await PostgreSqlMigrationTests.ApplyAsync(project, file), "Runtime metadata migration completed.");
                AssertSuccess(await PostgreSqlBootstrapTests.BootstrapAsync(project, file), "Runtime metadata bootstrap completed.");
            }
            string[] initialized = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, token);
            AssertSuccess(await PostgreSqlBootstrapTests.BootstrapAsync(project, shadowFile), "Runtime metadata bootstrap completed.");
            Assert.True(initialized.AsEnumerable().SequenceEqual(await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, token), StringComparer.Ordinal), "SF-TEST-PG-PROOF Metadata changed.");


        }
        finally
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
            await AdminAsync(fixture, $"DROP ROLE \"{extra}\"", cleanup.Token);
        }
    }

    private static void AssertRefused(MigratorProcessResult result)
    {
        AssertExitCode(result, 1);
        Assert.Empty(result.StandardOutput);
        Assert.Equal("SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required." + Environment.NewLine,
            Encoding.UTF8.GetString(result.StandardError));
    }

    private static void AssertSuccess(MigratorProcessResult result, string message)
    {
        AssertExitCode(result, 0);
        Assert.Empty(result.StandardError);
        Assert.Equal(message + Environment.NewLine, Encoding.UTF8.GetString(result.StandardOutput));
    }

    private static void AssertExitCode(MigratorProcessResult result, int expected)
    {
        string diagnostic = Regex.Match(Encoding.UTF8.GetString(result.StandardError),
            @"SF-(?:DB-MIGRATOR|MIGRATION|BOOTSTRAP)-[A-Z]*-?[0-9]{3}").Value;
        Assert.True(result.ExitCode == expected,
            $"SF-CORR002-COMMAND expectedExit={expected} actualExit={result.ExitCode} diagnostic={diagnostic}");
    }

    private static Task AdminAsync(PostgreSqlRoleFixture fixture, string sql, CancellationToken token) =>
        fixture.Database.ExecuteAsync(async (connection, cancellationToken) =>
        {
            await using NpgsqlCommand command = new(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, token);
}
