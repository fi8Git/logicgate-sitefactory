using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class PlannedFileIdTests
{
    [Fact]
    public void IdentifierIsSuppliedVerbatimWithCaseSensitiveValueEquality()
    {
        PlannedFileId id = new("template-readme");

        Assert.Equal("template-readme", id.Value);
        Assert.Equal("template-readme", id.ToString());
        Assert.Equal(id, new PlannedFileId("template-readme"));
        Assert.Equal(id.GetHashCode(), new PlannedFileId("template-readme").GetHashCode());
        Assert.NotEqual(id, new PlannedFileId("TEMPLATE-README"));
        Assert.NotEqual(id, new PlannedFileId("template-project"));
        Assert.Equal(" supplied-id ", new PlannedFileId(" supplied-id ").Value);
        Assert.NotEqual<object>(id, new GenerationPlanId("template-readme"));
    }

    [Fact]
    public void NullIdentifierIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PlannedFileId(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankIdentifierIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new PlannedFileId(value));
    }
}
