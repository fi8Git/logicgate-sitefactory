using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class ConfigurationValidationResultTests
{
    [Fact]
    public void EmptyResultIsValid()
    {
        ConfigurationValidationResult result = new([]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Information, true)]
    [InlineData(DiagnosticSeverity.Warning, true)]
    [InlineData(DiagnosticSeverity.Error, false)]
    public void OnlyErrorSeverityMakesAResultInvalid(DiagnosticSeverity severity, bool expectedValidity)
    {
        ConfigurationDiagnostic diagnostic = CreateDiagnostic(severity);
        ConfigurationValidationResult result = new([diagnostic]);

        Assert.Equal(expectedValidity, result.IsValid);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void MultipleNonErrorDiagnosticsRemainValid()
    {
        ConfigurationValidationResult result = new(
            [CreateDiagnostic(DiagnosticSeverity.Warning), CreateDiagnostic(DiagnosticSeverity.Information)]);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Diagnostics.Count);
    }

    [Fact]
    public void MultipleDiagnosticsArePreservedInStableOrdinalOrder()
    {
        ConfigurationDiagnostic[] expected =
        [
            CreateDiagnostic(propertyPath: null),
            CreateDiagnostic(propertyPath: "Zeta"),
            CreateDiagnostic(propertyPath: "alpha", message: "A value is required."),
            CreateDiagnostic(propertyPath: "alpha", message: "Supply a value.", severity: DiagnosticSeverity.Warning),
            CreateDiagnostic(propertyPath: "alpha", message: "Supply a value."),
            CreateDiagnostic(propertyPath: "alpha", code: "SF-CONFIG-SCHEMA-004"),
        ];
        ConfigurationValidationResult result = new(expected.Reverse());
        ConfigurationValidationResult repeated = new(expected);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Diagnostics);
        Assert.Equal(result.Diagnostics, repeated.Diagnostics);
    }

    [Fact]
    public void ResultDoesNotChangeWhenTheInputCollectionChanges()
    {
        ConfigurationDiagnostic diagnostic = CreateDiagnostic();
        List<ConfigurationDiagnostic> source = [diagnostic];
        ConfigurationValidationResult result = new(source);

        source.Clear();

        Assert.False(result.IsValid);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void ExposedDiagnosticsCannotBeModified()
    {
        ConfigurationDiagnostic diagnostic = CreateDiagnostic();
        ConfigurationValidationResult result = new([diagnostic]);
        IList<ConfigurationDiagnostic> collection = Assert.IsAssignableFrom<IList<ConfigurationDiagnostic>>(result.Diagnostics);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Throws<NotSupportedException>(() => collection[0] = CreateDiagnostic(DiagnosticSeverity.Warning));
        Assert.False(result.IsValid);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void NullCollectionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationValidationResult(null!));
    }

    [Fact]
    public void NullDiagnosticIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ConfigurationValidationResult([null!]));
    }

    private static ConfigurationDiagnostic CreateDiagnostic(
        DiagnosticSeverity severity = DiagnosticSeverity.Error,
        string? propertyPath = null,
        string code = "SF-CONFIG-SCHEMA-003",
        string message = "A value is required.")
    {
        return new ConfigurationDiagnostic(new DiagnosticCode(code), severity, message, propertyPath);
    }
}
