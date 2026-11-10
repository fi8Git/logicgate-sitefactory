using System.Globalization;
using Xunit;

namespace LogicGate.SiteFactory.Domain.Tests;

public sealed class TemplateVersionTests
{
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("1.20.300")]
    public void ExactStableVersionsHaveValueEquality(string value)
    {
        TemplateVersion version = new(value);

        Assert.Equal(value, version.Value);
        Assert.Equal(value, version.ToString());
        Assert.Equal(version, new TemplateVersion(value));
        Assert.Equal(version.GetHashCode(), new TemplateVersion(value).GetHashCode());
        Assert.NotEqual(version, new TemplateVersion("2.0.0"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("latest")]
    [InlineData("*")]
    [InlineData("1.*")]
    [InlineData(">=1.0.0")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("-1.2.3")]
    [InlineData(" 1.2.3 ")]
    [InlineData("1.2.3-preview")]
    [InlineData("1.2.3+build")]
    public void ValuesOutsideTheInitialStableVersionContractAreRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new TemplateVersion(value));
    }

    [Fact]
    public void NullVersionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TemplateVersion(null!));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void VersionRepresentationDoesNotDependOnCulture(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.Equal("1.20.300", new TemplateVersion("1.20.300").Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
