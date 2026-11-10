using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationPlanIdTests
{
    [Fact]
    public void IdentifierIsSuppliedVerbatimAndHasCaseSensitiveValueEquality()
    {
        GenerationPlanId id = new("test-plan-001");

        Assert.Equal("test-plan-001", id.Value);
        Assert.Equal("test-plan-001", id.ToString());
        Assert.Equal(id, new GenerationPlanId("test-plan-001"));
        Assert.Equal(id.GetHashCode(), new GenerationPlanId("test-plan-001").GetHashCode());
        Assert.NotEqual(id, new GenerationPlanId("TEST-PLAN-001"));
        Assert.NotEqual(id, new GenerationPlanId("test-plan-002"));
        Assert.Equal(" supplied id ", new GenerationPlanId(" supplied id ").Value);
    }

    [Fact]
    public void NullIdentifierIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new GenerationPlanId(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankIdentifierIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new GenerationPlanId(value));
    }
}
