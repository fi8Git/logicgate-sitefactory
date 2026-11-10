using Xunit;

namespace LogicGate.SiteFactory.ModuleSdk.Tests;

public sealed class ModuleVersionTests
{
    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.2.3")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0-0.3.7")]
    [InlineData("1.0.0-x-y-z.--")]
    [InlineData("1.0.0-01a")]
    [InlineData("1.0.0+001")]
    [InlineData("1.0.0-beta+exp.sha.5114f85")]
    [InlineData("999999999999999999999999.0.0")]
    public void ExactSemVerValuesArePreserved(string value)
    {
        ModuleVersion version = new(value);
        Assert.Equal(value, version.Value);
        Assert.Equal(value, version.ToString());
        Assert.Equal(version, new ModuleVersion(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-alpha.01")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3-alpha_1")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+build..1")]
    [InlineData("1.2.3+build+1")]
    [InlineData("1.2.3+build_1")]
    [InlineData("1.2.3-alpha.")]
    [InlineData("1.2.3+build.")]
    [InlineData("1.2.3\n")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3 ")]
    [InlineData("v1.2.3")]
    [InlineData("1.2.*")]
    [InlineData(">=1.2.3")]
    [InlineData("latest")]
    [InlineData("١.2.3")]
    [InlineData("1.2.3-été")]
    public void InvalidVersionsAndVersionSelectorsAreRejected(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModuleVersion(value!));
    }

    [Fact]
    public void EqualityMeansExactIdentityNotSemVerPrecedence()
    {
        Assert.NotEqual(new ModuleVersion("1.0.0+one"), new ModuleVersion("1.0.0+two"));
        Assert.NotEqual(new ModuleVersion("1.0.0-alpha"), new ModuleVersion("1.0.0-Alpha"));
    }
}
