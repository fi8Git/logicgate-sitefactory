using System.Globalization;
using Xunit;

namespace LogicGate.SiteFactory.ModuleSdk.Tests;

public sealed class ModuleDescriptorTests
{
    [Fact]
    public void SeparateAssemblyCanImplementTheEntirePublicContract()
    {
        ISiteFactoryModule module = new SampleModule();
        Assert.NotEqual(typeof(ISiteFactoryModule).Assembly, module.GetType().Assembly);
        Assert.Same(module.Descriptor, module.Descriptor);
        Assert.Equal(new ModuleId("sample-module"), module.Descriptor.Id);
        Assert.Equal(new ModuleVersion("1.0.0"), module.Descriptor.Version);
        Assert.Equal([new ModuleCapability("sample-module.read")], module.Descriptor.Capabilities);
    }

    [Fact]
    public void DescriptorMayDeclareNoCapabilities()
    {
        Assert.Empty(new ModuleDescriptor(new("sample-module"), new("0.1.0"), []).Capabilities);
    }

    [Fact]
    public void DescriptorRejectsMissingMetadata()
    {
        Assert.Throws<ArgumentNullException>(() => new ModuleDescriptor(null!, new("1.0.0"), []));
        Assert.Throws<ArgumentNullException>(() => new ModuleDescriptor(new("sample-module"), null!, []));
        Assert.Throws<ArgumentNullException>(() => new ModuleDescriptor(new("sample-module"), new("1.0.0"), null!));
        Assert.Throws<ArgumentException>(() => new ModuleDescriptor(new("sample-module"), new("1.0.0"), [null!]));
    }

    [Fact]
    public void DescriptorRejectsDuplicateCapabilityValuesFromDifferentInstances()
    {
        Assert.Throws<ArgumentException>(() => new ModuleDescriptor(new("sample-module"), new("1.0.0"),
            [new("sample-module.read"), new("sample-module.read")]));
    }

    [Theory]
    [InlineData("other.read")]
    [InlineData("sample-module-extra.read")]
    public void DescriptorCannotClaimAnotherModulesCapability(string value)
    {
        Assert.Throws<ArgumentException>(() => new ModuleDescriptor(new("sample-module"), new("1.0.0"), [new(value)]));
    }

    [Fact]
    public void DescriptorCopiesItsInputAndExposesOnlyReadOnlyCapabilities()
    {
        ModuleCapability[] input = [new("sample-module.write"), new("sample-module.read")];
        ModuleDescriptor descriptor = new(new("sample-module"), new("1.0.0"), input);
        input[0] = new("sample-module.changed");

        Assert.Equal(["sample-module.read", "sample-module.write"], descriptor.Capabilities.Select(static capability => capability.Value));
        IList<ModuleCapability> exposed = Assert.IsAssignableFrom<IList<ModuleCapability>>(descriptor.Capabilities);
        Assert.Throws<NotSupportedException>(() => exposed[0] = new("sample-module.changed"));
        Assert.Throws<NotSupportedException>(() => exposed.Add(new("sample-module.changed")));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void CapabilityOrderIsOrdinalAndIndependentOfInputOrder(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            ModuleCapability[] input = [new("sample-module.z"), new("sample-module.a-a"), new("sample-module.a0")];
            ModuleDescriptor first = new(new("sample-module"), new("1.0.0"), input);
            ModuleDescriptor reversed = new(first.Id, first.Version, input.Reverse());
            Assert.Equal(["sample-module.a-a", "sample-module.a0", "sample-module.z"], first.Capabilities.Select(static capability => capability.Value));
            Assert.Equal(first.Capabilities, reversed.Capabilities);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // This project references only ModuleSdk, not the engine. No InternalsVisibleTo is used.
    private sealed class SampleModule : ISiteFactoryModule
    {
        public ModuleDescriptor Descriptor { get; } = new(new("sample-module"), new("1.0.0"), [new("sample-module.read")]);
    }
}
