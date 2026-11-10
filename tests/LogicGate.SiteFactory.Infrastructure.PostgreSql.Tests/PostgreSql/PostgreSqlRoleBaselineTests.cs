using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

public sealed class PostgreSqlRoleBaselineTests
{
    [Fact]
    public void NamesAreIsolatedBoundedAndDeterministicWithoutInventingProductionNames()
    {
        PostgreSqlRoleBaseline first = new("sf_test_00000000000000000000000000000001");
        PostgreSqlRoleBaseline same = new(first.Database);
        PostgreSqlRoleBaseline other = new("sf_test_00000000000000000000000000000002");
        Assert.Equal(first.Names, same.Names);
        Assert.Equal(first.Sql, same.Sql);
        Assert.Empty(first.Names.Intersect(other.Names, StringComparer.Ordinal));
        Assert.Equal(4, first.Names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(first.Names, name =>
        {
            Assert.Matches("^sf_[0-9a-f]{32}_test_(owner|migrator|web|worker)$", name);
            Assert.True(name.Length <= 63);
        });
        Assert.DoesNotContain("PASSWORD", first.Sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("sitefactory_test_admin")]
    [InlineData("sf_test_0000000000000000000000000000000g")]
    [InlineData("sf_test_00000000000000000000000000000000;DROP ROLE postgres")]
    [InlineData("sf_client_production")]
    public void ArbitraryNamesCannotEnterRoleSql(string name) => Assert.Throws<ArgumentException>(() => new PostgreSqlRoleBaseline(name));
}
