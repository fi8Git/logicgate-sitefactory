using Xunit;

namespace LogicGate.SiteFactory.Domain.Tests;

public sealed class SiteFactoryVersionTests
{
    [Theory]
    [InlineData("0.1.0-dev")]
    [InlineData("1.20.300")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0-0.3.7")]
    [InlineData("1.0.0-x-y-z.--")]
    [InlineData("0.1.0-dev+abc123")]
    [InlineData("1.0.0+001")]
    [InlineData("1.0.0-01alpha+build.002")]
    public void ExactVersionAndBuildIdentityAreRetained(string value)
    {
        SiteFactoryVersion version = new(value);

        Assert.Equal(value, version.Value);
        Assert.Equal(value, version.ToString());
        Assert.Equal(version, new SiteFactoryVersion(value));
        Assert.NotEqual(version, new SiteFactoryVersion("2.0.0"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("latest")]
    [InlineData("main")]
    [InlineData("*")]
    [InlineData("1.*")]
    [InlineData(">=1.0.0")]
    [InlineData("[1.0.0,2.0.0)")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3\n")]
    [InlineData("1.2.3\r\n")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-alpha.01")]
    [InlineData("1.2.3-alpha..one")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+meta..data")]
    [InlineData("1.2.3+first+second")]
    [InlineData("1.2.3-préversion")]
    [InlineData("١.2.3")]
    [InlineData("1.2.3+/local/path")]
    [InlineData("1.2.3+user\\path")]
    public void FloatingMalformedOrEnvironmentalVersionsAreRejected(string value) =>
        Assert.Throws<ArgumentException>(() => new SiteFactoryVersion(value));

    [Fact]
    public void MissingVersionIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new SiteFactoryVersion(null!));

    [Fact]
    public void DistinctEmbeddedBuildIdentitiesAreNotDiscarded() =>
        Assert.NotEqual(new SiteFactoryVersion("0.1.0-dev+abc123"), new SiteFactoryVersion("0.1.0-dev+def456"));
}
