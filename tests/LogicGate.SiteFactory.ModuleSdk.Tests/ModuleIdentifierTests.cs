using Xunit;

namespace LogicGate.SiteFactory.ModuleSdk.Tests;

public sealed class ModuleIdentifierTests
{
    [Theory]
    [InlineData("sample-module")]
    [InlineData("a")]
    [InlineData("123")]
    [InlineData("sample-2")]
    public void ModuleIdsHaveExactValueEquality(string value)
    {
        Assert.Equal(new ModuleId(value), new ModuleId(value));
        Assert.Equal(value, new ModuleId(value).ToString());
        Assert.NotEqual(new ModuleId(value), new ModuleId(value + "-other"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Sample")]
    [InlineData("sample_module")]
    [InlineData("sample.module")]
    [InlineData("-sample")]
    [InlineData("sample-")]
    [InlineData("sample--module")]
    [InlineData("modulé")]
    [InlineData("sample ")]
    [InlineData("sample\n")]
    [InlineData("../sample")]
    [InlineData("sample/module")]
    [InlineData("sample\\module")]
    public void InvalidModuleIdsAreRejectedWithoutNormalization(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModuleId(value!));
    }

    [Fact]
    public void ModuleIdLengthMatchesTheExistingConfigurationContract()
    {
        Assert.Equal(80, new ModuleId(new string('a', 80)).Value.Length);
        Assert.Throws<ArgumentException>(() => new ModuleId(new string('a', 81)));
    }

    [Theory]
    [InlineData("sample.read")]
    [InlineData("sample-module.generate-pages")]
    [InlineData("a.1")]
    public void CapabilitiesHaveExactValueEquality(string value)
    {
        Assert.Equal(new ModuleCapability(value), new ModuleCapability(value));
        Assert.Equal(value, new ModuleCapability(value).ToString());
        Assert.NotEqual(new ModuleCapability(value), new ModuleCapability(value + "-other"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("read")]
    [InlineData(".read")]
    [InlineData("sample.")]
    [InlineData("sample..read")]
    [InlineData("sample.read.more")]
    [InlineData("sample.Read")]
    [InlineData("Sample.read")]
    [InlineData("sample.read-all ")]
    [InlineData("sample.read\n")]
    [InlineData("sample.read_all")]
    [InlineData("sample.-read")]
    [InlineData("sample.read-")]
    [InlineData("sample.réad")]
    public void InvalidCapabilitiesAreRejected(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModuleCapability(value!));
    }

    [Fact]
    public void BothCapabilitySegmentsAreBounded()
    {
        string segment = new('a', 80);
        Assert.Equal(161, new ModuleCapability(segment + "." + segment).Value.Length);
        Assert.Throws<ArgumentException>(() => new ModuleCapability(segment + "a.read"));
        Assert.Throws<ArgumentException>(() => new ModuleCapability("sample." + segment + "a"));
    }
}
