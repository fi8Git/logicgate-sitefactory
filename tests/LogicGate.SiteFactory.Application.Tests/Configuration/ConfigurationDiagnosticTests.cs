using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class ConfigurationDiagnosticTests
{
    private static readonly DiagnosticCode RequiredPropertyCode = new("SF-CONFIG-SCHEMA-003");

    [Theory]
    [InlineData(DiagnosticSeverity.Information)]
    [InlineData(DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticSeverity.Error)]
    public void DiagnosticPreservesItsFields(DiagnosticSeverity severity)
    {
        ConfigurationDiagnostic diagnostic = new(
            RequiredPropertyCode, severity, "A value is required.", "modules[1].enabled");

        Assert.Equal(RequiredPropertyCode, diagnostic.Code);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.Equal("A value is required.", diagnostic.Message);
        Assert.Equal("modules[1].enabled", diagnostic.PropertyPath);
    }

    [Fact]
    public void PropertyPathIsOptional()
    {
        ConfigurationDiagnostic diagnostic = new(
            RequiredPropertyCode, DiagnosticSeverity.Error, "A value is required.");

        Assert.Null(diagnostic.PropertyPath);
    }

    [Fact]
    public void DifferentHumanMessagesDoNotChangeTheMachineCode()
    {
        ConfigurationDiagnostic first = new(
            RequiredPropertyCode, DiagnosticSeverity.Error, "A value is required.");
        ConfigurationDiagnostic second = new(
            new DiagnosticCode("SF-CONFIG-SCHEMA-003"), DiagnosticSeverity.Error, "Supply the missing value.");

        Assert.NotEqual(first.Message, second.Message);
        Assert.Equal(first.Code, second.Code);
        Assert.Equal("SF-CONFIG-SCHEMA-003", second.Code.Value);
    }

    [Fact]
    public void NullCodeIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationDiagnostic(
            null!, DiagnosticSeverity.Error, "A value is required."));
    }

    [Fact]
    public void NullMessageIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationDiagnostic(
            RequiredPropertyCode, DiagnosticSeverity.Error, null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankMessageIsRejected(string message)
    {
        Assert.Throws<ArgumentException>(() => new ConfigurationDiagnostic(
            RequiredPropertyCode, DiagnosticSeverity.Error, message));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankPropertyPathIsRejected(string propertyPath)
    {
        Assert.Throws<ArgumentException>(() => new ConfigurationDiagnostic(
            RequiredPropertyCode, DiagnosticSeverity.Error, "A value is required.", propertyPath));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void UndefinedSeverityIsRejected(int severity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfigurationDiagnostic(
            RequiredPropertyCode, (DiagnosticSeverity)severity, "A value is required."));
    }
}
