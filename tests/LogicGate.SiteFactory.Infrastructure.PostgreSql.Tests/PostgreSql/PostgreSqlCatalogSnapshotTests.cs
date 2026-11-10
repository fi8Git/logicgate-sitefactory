using System.Text.Json;
using Npgsql;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Collection(nameof(P02TestScope))]
[Trait("Category", "PostgreSql")]
[Trait("ExtractionLot", "P02")]
public sealed class PostgreSqlCatalogSnapshotTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static bool IsConfiguredOrRequired => PostgreSqlTestGate.IsConfiguredOrRequired;

    [Fact(SkipUnless = nameof(IsConfiguredOrRequired), Skip = "Requires dedicated PostgreSQL 18; see docs/extraction/SF-TASK-062-G01-allocation.md.")]
    public async Task CatalogSnapshotExcludesOnlyLiteralSystemSchemaPrefixes()
    {
        using IDisposable measurement = PostgreSqlTestMetrics.Test();
        PostgreSqlTestConfiguration configuration = await PostgreSqlTestGate.ReadAsync(Token);
        await using PostgreSqlRoleFixture fixture = await PostgreSqlRoleFixture.CreateAsync(configuration, Token);
        await fixture.Database.ExecuteAsync(async (connection, token) =>
        {
            await using NpgsqlCommand command = new("SELECT nspname::pg_catalog.text FROM pg_catalog.pg_namespace WHERE nspname IN ('pg_catalog', 'pg_toast') ORDER BY nspname", connection);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            List<string> names = [];
            while (await reader.ReadAsync(token)) names.Add(reader.GetString(0));
            Assert.Equal(["pg_catalog", "pg_toast"], names);
        }, Token);
        Assert.Equal(["public", "sitefactory_runtime"], await SchemaNamesAsync(fixture));
        foreach (string schema in PostgreSqlSchemaPrivilegeCases.ApplicationSchemas)
            await PostgreSqlSchemaPrivilegeCases.AdminAsync(fixture, $"CREATE SCHEMA \"{schema}\" AUTHORIZATION \"{fixture.Baseline.Owner}\"");
        // An independent expectation also catches an accidentally shortened setup/schema list.
        string[] expected = ["pgreview", "pgclient", "pgame", "pg1", "client_schema", "public", "sitefactory_runtime"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), await SchemaNamesAsync(fixture));
    }

    private static async Task<string[]> SchemaNamesAsync(PostgreSqlRoleFixture fixture)
    {
        string[] snapshot = await PostgreSqlCatalogSnapshot.ReadAsync(fixture, migrated: false, Token);
        return snapshot.Where(static row => row.StartsWith("schema|", StringComparison.Ordinal)).Select(static row =>
        {
            using JsonDocument document = JsonDocument.Parse(row[7..]);
            return document.RootElement[0].GetString()!;
        }).Order(StringComparer.Ordinal).ToArray();
    }
}
