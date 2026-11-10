using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationPlanDiagnosticTests
{
    [Theory]
    [InlineData(DiagnosticSeverity.Information)]
    [InlineData(DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticSeverity.Error)]
    public void CodeIsIndependentOfMessageAndOptionalLogicalPath(DiagnosticSeverity severity)
    {
        DiagnosticCode code = new("TEST-PLAN-001");
        GenerationPlanDiagnostic first = new(code, severity, "A test diagnostic.");
        GenerationPlanDiagnostic second = new(code, severity, "Another test message.", "files[0]");

        Assert.Equal(first.Code, second.Code);
        Assert.Equal("TEST-PLAN-001", first.Code.Value);
        Assert.Equal(severity, first.Severity);
        Assert.Null(first.PropertyPath);
        Assert.Equal("files[0]", second.PropertyPath);
        Assert.NotEqual(first.Message, second.Message);
    }

    [Fact]
    public void NullRequiredArgumentsAndUnknownSeverityAreRejected()
    {
        Assert.Throws<ArgumentNullException>("code", () => new GenerationPlanDiagnostic(null!, DiagnosticSeverity.Error, "Message."));
        Assert.Throws<ArgumentNullException>("message", () => new GenerationPlanDiagnostic(new("TEST-PLAN-001"), DiagnosticSeverity.Error, null!));
        Assert.Throws<ArgumentOutOfRangeException>("severity", () => new GenerationPlanDiagnostic(new("TEST-PLAN-001"), (DiagnosticSeverity)99, "Message."));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankMessageOrSuppliedPathIsRejected(string value)
    {
        Assert.Throws<ArgumentException>("message", () => new GenerationPlanDiagnostic(new("TEST-PLAN-001"), DiagnosticSeverity.Error, value));
        Assert.Throws<ArgumentException>("propertyPath", () => new GenerationPlanDiagnostic(new("TEST-PLAN-001"), DiagnosticSeverity.Error, "Message.", value));
    }
}
