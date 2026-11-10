using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

internal static class PostgreSqlSchemaPrivilegeCases
{
    internal static readonly string[] ApplicationSchemas = ["pgreview", "pgclient", "pgame", "pg1", "client_schema"];
    private static readonly PostgreSqlTestLogin[] RuntimeLogins = [PostgreSqlTestLogin.Web, PostgreSqlTestLogin.Worker];
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    internal static async Task VerifyAsync(PostgreSqlRoleFixture fixture, string schema, Func<PostgreSqlTestLogin, bool, Task> check)
    {
        // Identifiers are fixed test constants, never supplied by a user or an environment variable.
        Assert.Contains(schema, ApplicationSchemas);
        await AdminAsync(fixture, $"CREATE SCHEMA \"{schema}\" AUTHORIZATION \"{fixture.Baseline.Owner}\"; REVOKE ALL ON SCHEMA \"{schema}\" FROM PUBLIC");
        foreach (PostgreSqlTestLogin login in RuntimeLogins)
        {
            string role = fixture.Name(login);
            await AdminAsync(fixture, $"GRANT USAGE ON SCHEMA \"{schema}\" TO \"{role}\"");
            await AssertCreateAsync(fixture, login, schema, allowed: false);
            await CheckWithoutWritesAsync(login, compatible: true);

            await AdminAsync(fixture, $"GRANT USAGE, CREATE ON SCHEMA \"{schema}\" TO \"{role}\"");
            // Probe/hosts must reject CREATE BEFORE the role owns any object, avoiding an ownership false positive.
            await CheckWithoutWritesAsync(login, compatible: false);
            await AssertCreateAsync(fixture, login, schema, allowed: true);

            await AdminAsync(fixture, $"REVOKE CREATE ON SCHEMA \"{schema}\" FROM \"{role}\"");
            await CheckWithoutWritesAsync(login, compatible: true);
        }
        await AdminAsync(fixture, $"DROP SCHEMA \"{schema}\""); // The CREATE TABLE proof was rolled back; no CASCADE.

        async Task CheckWithoutWritesAsync(PostgreSqlTestLogin login, bool compatible)
        {
            string[] before = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token);
            await check(login, compatible);
            Assert.Equal(before, await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: true, Token));
        }
    }

    private static Task AssertCreateAsync(PostgreSqlRoleFixture fixture, PostgreSqlTestLogin login, string schema, bool allowed) =>
        fixture.ExecuteAsync(login, async (connection, token) =>
        {
            await using NpgsqlCommand privilege = new("SELECT has_schema_privilege(current_user, $1, 'CREATE')", connection);
            privilege.Parameters.AddWithValue(schema);
            Assert.Equal(allowed, await privilege.ExecuteScalarAsync(token));
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
            await using NpgsqlCommand create = new($"CREATE TABLE \"{schema}\".unexpected(id int)", connection, transaction);
            if (allowed)
            {
                await create.ExecuteNonQueryAsync(token);
                await using NpgsqlCommand exists = new("SELECT to_regclass($1) IS NOT NULL", connection, transaction);
                exists.Parameters.AddWithValue(schema + ".unexpected");
                Assert.Equal(true, await exists.ExecuteScalarAsync(token));
            }
            else
            {
                PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync(token));
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
            }
            await transaction.RollbackAsync(token);
        }, Token);

    internal static Task AdminAsync(PostgreSqlRoleFixture fixture, string sql) => fixture.Database.ExecuteAsync(async (connection, token) =>
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }, Token);
}
