using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class ConfigurationLoadResultTests
{
    [Fact]
    public void FailedLoadContainsAnErrorAndNoPartialConfiguration()
    {
        ConfigurationDiagnostic diagnostic = new(new("SF-CONFIG-YAML-001"), DiagnosticSeverity.Error, "File not found.");

        ConfigurationLoadResult result = ConfigurationLoadResult.Failure(diagnostic);

        Assert.Null(result.Configuration);
        Assert.False(result.Validation.IsValid);
        Assert.Same(diagnostic, Assert.Single(result.Validation.Diagnostics));
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Information)]
    [InlineData(DiagnosticSeverity.Warning)]
    public void AFailedLoadCannotContainOnlyANonBlockingDiagnostic(DiagnosticSeverity severity)
    {
        ConfigurationDiagnostic diagnostic = new(new("SF-CONFIG-SCHEMA-003"), severity, "A value is required.");

        Assert.Throws<ArgumentException>(() => ConfigurationLoadResult.Failure(diagnostic));
    }

    [Fact]
    public void ResultFactoriesRejectNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => ConfigurationLoadResult.Success(null!));
        Assert.Throws<ArgumentNullException>(() => ConfigurationLoadResult.Failure(null!));
    }
}
