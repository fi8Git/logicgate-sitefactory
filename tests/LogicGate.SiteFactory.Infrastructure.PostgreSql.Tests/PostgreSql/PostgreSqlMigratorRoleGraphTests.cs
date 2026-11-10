using System.Text;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Trait("Category", "PostgreSql")]
[Collection(nameof(PostgreSqlMigrationTests))]
public sealed class PostgreSqlMigratorRoleGraphTests
{
    private static readonly string[] AdministrativeAttributes = ["CREATEDB", "CREATEROLE", "SUPERUSER", "REPLICATION", "BYPASSRLS"];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see tests/PostgreSql/README.md.")]
    public async Task ExtractedMigratorRejectsAnyRoleGraphOutsideItsSingleOwnerEdge()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        string sandbox = Directory.CreateTempSubdirectory("sitefactory-migrator-graph-").FullName;
        try
        {
            string project = Directory.CreateDirectory(Path.Combine(sandbox, "working directory")).FullName;
            await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
            string file = Path.Combine(sandbox, "migrator-connection");
            await fixture.WriteConnectionFileAsync(PostgreSqlTestLogin.Migrator, file, Token);
            string prefix = fixture.Baseline.Migrator[..^"migrator".Length];
            string extra = prefix + "extra_admin";
            string intermediate = prefix + "intermediate";
            // Only internally minted names in this isolated test cluster; never configuration-supplied identifiers.
            Assert.True(extra.Length <= 63 && intermediate.Length <= 63);
            await AdminAsync(fixture, $"""
                CREATE ROLE "{extra}" NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                CREATE ROLE "{intermediate}" NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
                """, Token);
            try
            {
                await CheckCasesAsync(fixture, project, file, extra, intermediate, migrated: false);
                AssertSuccess(await PostgreSqlMigrationTests.ApplyAsync(project, file), "Runtime metadata migration completed.");
                // The same preflight also protects bootstrap, not only an empty database's first migration.
                await CheckCasesAsync(fixture, project, file, extra, intermediate, migrated: true);
                AssertSuccess(await PostgreSqlBootstrapTests.BootstrapAsync(project, file), "Runtime metadata bootstrap completed.");
                string[] initialized = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, Token);
                AssertSuccess(await PostgreSqlMigrationTests.ApplyAsync(project, file), "Runtime metadata migration completed.");
                AssertSuccess(await PostgreSqlBootstrapTests.BootstrapAsync(project, file), "Runtime metadata bootstrap completed.");
                Assert.True(initialized.AsEnumerable().SequenceEqual(await PostgreSqlCatalogSnapshot.ReadAsync(fixture, true, Token), StringComparer.Ordinal), "SF-TEST-PG-PROOF Unexpected mutation.");
            }
            finally
            {
                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
                // These exact test roles own no objects. DROP ROLE removes their memberships, without CASCADE.
                await AdminAsync(fixture, $"DROP ROLE \"{intermediate}\"; DROP ROLE \"{extra}\"", cleanup.Token);
            }
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, sandbox, StringComparison.Ordinal);
            Assert.StartsWith("sitefactory-migrator-graph-", Path.GetFileName(sandbox), StringComparison.Ordinal);
            Directory.Delete(sandbox, recursive: true);
        }
    }

    private static async Task CheckCasesAsync(PostgreSqlRoleFixture fixture, string project, string file,
        string extra, string intermediate, bool migrated)
    {
        string migrator = fixture.Baseline.Migrator;
        string owner = fixture.Baseline.Owner;
        await AssertBaselineAsync(fixture);
        foreach (string attribute in AdministrativeAttributes)
            await RejectAsync($"ALTER ROLE \"{extra}\" {attribute}; {Grant(extra, migrator)}",
                $"REVOKE \"{extra}\" FROM \"{migrator}\"; ALTER ROLE \"{extra}\" NO{attribute}",
                proveCreatedbRole: attribute == "CREATEDB" ? extra : null);

        await RejectAsync($"ALTER ROLE \"{extra}\" CREATEDB; {Grant(extra, intermediate)} {Grant(intermediate, migrator)}",
            $"REVOKE \"{intermediate}\" FROM \"{migrator}\"; REVOKE \"{extra}\" FROM \"{intermediate}\"; ALTER ROLE \"{extra}\" NOCREATEDB",
            proveCreatedbRole: extra);
        await RejectAsync($"ALTER ROLE \"{extra}\" CREATEDB; {Grant(extra, intermediate)} {Grant(intermediate, owner)}",
            $"REVOKE \"{intermediate}\" FROM \"{owner}\"; REVOKE \"{extra}\" FROM \"{intermediate}\"; ALTER ROLE \"{extra}\" NOCREATEDB",
            proveCreatedbRole: extra);

        // Even a harmless NOLOGIN role, dormant membership or inherited/admin-only path is outside the allowlist.
        foreach (string options in new[] { "SET TRUE, INHERIT FALSE, ADMIN FALSE", "SET FALSE, INHERIT FALSE, ADMIN FALSE",
            "SET FALSE, INHERIT TRUE, ADMIN FALSE", "SET FALSE, INHERIT FALSE, ADMIN TRUE" })
        {
            await RejectAsync($"GRANT \"{extra}\" TO \"{migrator}\" WITH {options}", $"REVOKE \"{extra}\" FROM \"{migrator}\"");
            await RejectAsync($"GRANT \"{extra}\" TO \"{owner}\" WITH {options}", $"REVOKE \"{extra}\" FROM \"{owner}\"");
        }

        foreach ((string option, string reset) in new[] { ("SET FALSE", "SET TRUE"), ("INHERIT TRUE", "INHERIT FALSE"), ("ADMIN TRUE", "ADMIN FALSE") })
            await RejectAsync($"GRANT \"{owner}\" TO \"{migrator}\" WITH {option}", $"GRANT \"{owner}\" TO \"{migrator}\" WITH {reset}");
        await RejectAsync($"REVOKE \"{owner}\" FROM \"{migrator}\"", Grant(owner, migrator));
        foreach (string role in new[] { migrator, owner })
            foreach (string attribute in AdministrativeAttributes.Append("INHERIT"))
                await RejectAsync($"ALTER ROLE \"{role}\" {attribute}", $"ALTER ROLE \"{role}\" NO{attribute}");
        await RejectAsync($"ALTER ROLE \"{owner}\" LOGIN", $"ALTER ROLE \"{owner}\" NOLOGIN");
        await AssertBaselineAsync(fixture);

        async Task RejectAsync(string enable, string reset, string? proveCreatedbRole = null)
        {
            await AdminAsync(fixture, enable, Token);
            try
            {
                if (proveCreatedbRole is not null) await ProveElevationAsync(fixture, proveCreatedbRole);
                string[] before = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated, Token);
                string[] rolesBefore = await RoleSnapshotAsync(fixture);
                MigratorProcessResult result = migrated
                    ? await PostgreSqlBootstrapTests.BootstrapAsync(project, file)
                    : await PostgreSqlMigrationTests.ApplyAsync(project, file);
                Assert.Equal(1, result.ExitCode);
                Assert.Empty(result.StandardOutput);
                Assert.Equal("SF-DB-MIGRATOR-003 A dedicated migration login with controlled owner membership is required." + Environment.NewLine,
                    Encoding.UTF8.GetString(result.StandardError)); // Exact bounded diagnostic: no SQL, secret or stack trace.
                Assert.True(before.AsEnumerable().SequenceEqual(await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated, Token), StringComparer.Ordinal), "SF-TEST-PG-PROOF Unexpected mutation.");
                Assert.True(rolesBefore.AsEnumerable().SequenceEqual(await RoleSnapshotAsync(fixture), StringComparer.Ordinal), "SF-TEST-PG-PROOF Unexpected mutation."); // No grant repair either.
            }
            finally
            {
                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(15));
                await AdminAsync(fixture, reset, cleanup.Token);
            }
        }
    }

    private static string Grant(string role, string member) =>
        $"GRANT \"{role}\" TO \"{member}\" WITH SET TRUE, INHERIT FALSE, ADMIN FALSE;";

    private static Task ProveElevationAsync(PostgreSqlRoleFixture fixture, string target) => fixture.ExecuteAsync(
        PostgreSqlTestLogin.Migrator, async (connection, token) =>
        {
            await using NpgsqlCommand safe = new("SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls OR rolinherit) FROM pg_catalog.pg_roles WHERE rolname = current_user", connection);
            Assert.Equal(true, await safe.ExecuteScalarAsync(token));
            await using NpgsqlCommand reachable = new("SELECT pg_catalog.pg_has_role(current_user, $1, 'SET') AND NOT pg_catalog.pg_has_role(current_user, $1, 'USAGE')", connection);
            reachable.Parameters.AddWithValue(target);
            Assert.Equal(true, await reachable.ExecuteScalarAsync(token)); // NOINHERIT does not prevent SET ROLE.
            await using NpgsqlCommand assume = new($"SET ROLE \"{target}\"", connection);
            await assume.ExecuteNonQueryAsync(token);
            await using NpgsqlCommand identity = new("SELECT session_user::text, current_user::text, rolcreatedb FROM pg_catalog.pg_roles WHERE rolname = current_user", connection);
            await using (NpgsqlDataReader reader = await identity.ExecuteReaderAsync(token))
            {
                Assert.True(await reader.ReadAsync(token));
                Assert.Equal(fixture.Baseline.Migrator, reader.GetString(0));
                Assert.Equal(target, reader.GetString(1));
                Assert.True(reader.GetBoolean(2));
            }
            await using NpgsqlCommand reset = new("RESET ROLE", connection);
            await reset.ExecuteNonQueryAsync(token);
        }, Token);

    private static async Task AssertBaselineAsync(PostgreSqlRoleFixture fixture)
    {
        string[] snapshot = await RoleSnapshotAsync(fixture);
        string[] edges = snapshot.Where(static row => row.StartsWith("edge|", StringComparison.Ordinal)).ToArray();
        Assert.Equal([$"edge|{fixture.Baseline.Migrator}|{fixture.Baseline.Owner}|true|false|false"], edges);
    }

    private static async Task<string[]> RoleSnapshotAsync(PostgreSqlRoleFixture fixture)
    {
        List<string> rows = [];
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            // Traverse every membership, independent of SET/INHERIT, including all roles reachable through Owner.
            await using NpgsqlCommand command = new("""
                WITH RECURSIVE reachable(oid) AS (
                    SELECT oid FROM pg_catalog.pg_roles WHERE rolname = $1
                    UNION
                    SELECT m.roleid FROM pg_catalog.pg_auth_members m JOIN reachable r ON m.member = r.oid
                )
                SELECT 'role|' || r.rolname || '|' || r.rolcanlogin || '|' || r.rolinherit || '|' || r.rolsuper
                    || '|' || r.rolcreatedb || '|' || r.rolcreaterole || '|' || r.rolreplication || '|' || r.rolbypassrls
                FROM pg_catalog.pg_roles r WHERE r.oid IN (SELECT oid FROM reachable)
                UNION ALL
                SELECT 'edge|' || member.rolname || '|' || target.rolname || '|' || m.set_option || '|' || m.inherit_option || '|' || m.admin_option
                FROM pg_catalog.pg_auth_members m JOIN pg_catalog.pg_roles member ON member.oid = m.member
                JOIN pg_catalog.pg_roles target ON target.oid = m.roleid WHERE m.member IN (SELECT oid FROM reachable)
                """, connection);
            command.Parameters.AddWithValue(fixture.Baseline.Migrator);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(reader.GetString(0));
        }, Token);
        return rows.Order(StringComparer.Ordinal).ToArray();
    }

    private static Task AdminAsync(PostgreSqlRoleFixture fixture, string sql, CancellationToken token) =>
        fixture.Database.ExecuteAsync(async (connection, cancellationToken) =>
        {
            await using NpgsqlCommand command = new(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, token);

    private static void AssertSuccess(MigratorProcessResult result, string message)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Equal(message + Environment.NewLine, Encoding.UTF8.GetString(result.StandardOutput));
    }
}
