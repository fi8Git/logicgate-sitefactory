using Xunit;

namespace LogicGate.SiteFactory.Runtime.Contracts.Tests;

public sealed class RuntimeIdentityTests
{
    [Theory]
    [InlineData("Development", RuntimeEnvironment.Development)]
    [InlineData("Test", RuntimeEnvironment.Test)]
    [InlineData("Staging", RuntimeEnvironment.Staging)]
    [InlineData("Production", RuntimeEnvironment.Production)]
    [InlineData("production", RuntimeEnvironment.Production)]
    [InlineData("PRODUCTION", RuntimeEnvironment.Production)]
    public void IdentityIsValidatedWithoutAnyUrl(string environment, RuntimeEnvironment expected)
    {
        foreach (RuntimeComponent component in new[] { RuntimeComponent.Web, RuntimeComponent.Worker })
        {
            Assert.True(RuntimeIdentity.TryCreate("client-test", environment, component, out RuntimeIdentity? identity, out IReadOnlyList<StartupValidationResult> diagnostics));
            Assert.Empty(diagnostics);
            Assert.Equal("client-test", identity.ClientSlug);
            Assert.Equal(expected, identity.Environment);
            Assert.Equal(component, identity.Component);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("Bad Slug")]
    [InlineData("CLIENT")]
    [InlineData("-client")]
    [InlineData("client-")]
    [InlineData("client--test")]
    [InlineData("client_test")]
    [InlineData("../client")]
    [InlineData("clïent")]
    [InlineData("con")]
    [InlineData("prn")]
    [InlineData("aux")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("com9")]
    [InlineData("lpt1")]
    [InlineData("lpt9")]
    public void InvalidSlugIsRejectedWithTheExistingDiagnostic(string? slug)
    {
        Assert.False(RuntimeIdentity.TryCreate(slug, "Test", RuntimeComponent.Web, out RuntimeIdentity? identity, out IReadOnlyList<StartupValidationResult> errors));
        Assert.Null(identity);
        AssertDiagnostic(Assert.Single(errors), "ClientSlug");
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void SlugLengthBoundariesArePreserved(int length, bool valid)
    {
        Assert.Equal(valid, RuntimeIdentity.TryCreate(new string('a', length), "Test", RuntimeComponent.Worker, out _, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Production ")]
    [InlineData("Invalid")]
    [InlineData("3")]
    public void EnvironmentIsExplicitAndNeverDefaulted(string? environment)
    {
        Assert.False(RuntimeIdentity.TryCreate("client-test", environment, RuntimeComponent.Worker, out RuntimeIdentity? identity, out IReadOnlyList<StartupValidationResult> errors));
        Assert.Null(identity);
        AssertDiagnostic(Assert.Single(errors), "Environment");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(42)]
    public void UnknownComponentNeverBecomesWebOrMigrator(int component)
    {
        Assert.False(RuntimeIdentity.TryCreate("client-test", "Test", (RuntimeComponent)component, out RuntimeIdentity? identity, out IReadOnlyList<StartupValidationResult> errors));
        Assert.Null(identity);
        AssertDiagnostic(Assert.Single(errors), "Component");
        Assert.Equal([RuntimeComponent.Web, RuntimeComponent.Worker], Enum.GetValues<RuntimeComponent>());
    }

    [Fact]
    public void IdentityCannotBeConstructedOrMutatedWithoutValidation()
    {
        Assert.Empty(typeof(RuntimeIdentity).GetConstructors());
        Assert.All(typeof(RuntimeIdentity).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(typeof(RuntimeIdentity).GetProperties(), static property => property.PropertyType == typeof(Uri));
    }

    [Fact]
    public void FailureIsImmutableDeterministicAndNeverEchoesInput()
    {
        const string sensitive = "do-not-log!";
        Assert.False(RuntimeIdentity.TryCreate(sensitive, sensitive, (RuntimeComponent)42, out _, out IReadOnlyList<StartupValidationResult> first));
        Assert.False(RuntimeIdentity.TryCreate(sensitive, sensitive, (RuntimeComponent)42, out _, out IReadOnlyList<StartupValidationResult> second));
        Assert.Equal(first, second);
        Assert.Equal(["ClientSlug", "Environment", "Component"], first.Select(static result => result.PropertyPath));
        Assert.All(first, static result => Assert.DoesNotContain(sensitive, result.ToString(), StringComparison.Ordinal));
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)first).Clear());
    }

    private static void AssertDiagnostic(StartupValidationResult result, string property)
    {
        Assert.Equal("startup.configuration.schema", result.ValidationId.Value);
        Assert.Equal("SF-CONFIG-STARTUP-003", result.ErrorCode);
        Assert.Equal(property, result.PropertyPath);
        Assert.Equal(StartupValidationSeverity.BlockingFailure, result.Severity);
    }
}
