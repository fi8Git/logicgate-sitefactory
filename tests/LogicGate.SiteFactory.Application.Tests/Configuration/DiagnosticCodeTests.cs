using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class DiagnosticCodeTests
{
    [Fact]
    public void CodePreservesItsStableIdentifier()
    {
        DiagnosticCode code = new("SF-CONFIG-SCHEMA-003");

        Assert.Equal("SF-CONFIG-SCHEMA-003", code.Value);
        Assert.Equal("SF-CONFIG-SCHEMA-003", code.ToString());
        Assert.Equal(code, new DiagnosticCode("SF-CONFIG-SCHEMA-003"));
        Assert.NotEqual(code, new DiagnosticCode("SF-CONFIG-SCHEMA-004"));
        Assert.NotEqual(code, new DiagnosticCode("sf-config-schema-003"));
    }

    [Fact]
    public void NullCodeIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagnosticCode(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankCodeIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticCode(value));
    }
}
