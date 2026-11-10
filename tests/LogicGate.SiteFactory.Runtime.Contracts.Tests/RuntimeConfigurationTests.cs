using Xunit;

namespace LogicGate.SiteFactory.Runtime.Contracts.Tests;

public sealed class RuntimeConfigurationTests
{
    [Theory]
    [InlineData("Development", "http://localhost:5080", RuntimeEnvironment.Development)]
    [InlineData("Test", "http://127.0.0.1:5080", RuntimeEnvironment.Test)]
    [InlineData("Staging", "https://staging.example.test", RuntimeEnvironment.Staging)]
    [InlineData("Production", "https://www.example.test", RuntimeEnvironment.Production)]
    [InlineData("production", "https://www.example.test:443/path@name?key=value", RuntimeEnvironment.Production)]
    public void ValidConfigurationIsTypedAndHasCanonicalIdentity(string environment, string url, RuntimeEnvironment expected)
    {
        foreach (RuntimeComponent component in new[] { RuntimeComponent.Web, RuntimeComponent.Worker })
        {
            Assert.True(RuntimeConfiguration.TryCreate("client-test", environment, component, url, out RuntimeConfiguration? runtime, out IReadOnlyList<StartupValidationResult> errors));
            Assert.Empty(errors);
            Assert.Equal("client-test", runtime.Identity.ClientSlug);
            Assert.Equal(expected, runtime.Identity.Environment);
            Assert.Equal(component, runtime.Identity.Component);
            Assert.Equal(new Uri(url), runtime.PublicBaseUrl);
        }
    }

    [Theory]
    [InlineData(null, "Development", "https://www.example.test", "ClientSlug")]
    [InlineData("", "Development", "https://www.example.test", "ClientSlug")]
    [InlineData("Bad Slug", "Development", "https://www.example.test", "ClientSlug")]
    [InlineData("con", "Development", "https://www.example.test", "ClientSlug")]
    [InlineData("client-test", null, "https://www.example.test", "Environment")]
    [InlineData("client-test", "", "https://www.example.test", "Environment")]
    [InlineData("client-test", "Invalid", "https://www.example.test", "Environment")]
    [InlineData("client-test", "3", "https://www.example.test", "Environment")]
    [InlineData("client-test", "Production", null, "PublicBaseUrl")]
    [InlineData("client-test", "Production", "/relative", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "http://www.example.test", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://user:do-not-log@example.test", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://@example.test", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://www.example.test/#fragment", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://www.example.test/#", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://localhost", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://127.0.0.1", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://[::1]", "PublicBaseUrl")]
    [InlineData("client-test", "Production", "https://www.example.test:8443", "PublicBaseUrl")]
    [InlineData("client-test", "Development", "file:///config", "PublicBaseUrl")]
    [InlineData("client-test", "Staging", "ftp://www.example.test", "PublicBaseUrl")]
    public void InvalidInputHasDeterministicDiagnosticsWithoutEchoingValues(string? slug, string? environment, string? url, string property)
    {
        Assert.False(RuntimeConfiguration.TryCreate(slug, environment, RuntimeComponent.Web, url, out RuntimeConfiguration? runtime, out IReadOnlyList<StartupValidationResult> first));
        Assert.Null(runtime);
        Assert.Equal(property, Assert.Single(first).PropertyPath);
        Assert.DoesNotContain("do-not-log", first[0].Summary, StringComparison.Ordinal);
        Assert.False(RuntimeConfiguration.TryCreate(slug, environment, RuntimeComponent.Web, url, out _, out IReadOnlyList<StartupValidationResult> second));
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void ClientSlugLengthMatchesTheStructuralV1Contract(int length, bool valid)
    {
        Assert.Equal(valid, RuntimeConfiguration.TryCreate(new string('a', length), "Development", RuntimeComponent.Web,
            "http://localhost", out _, out _));
    }

    [Fact]
    public void UnsupportedComponentIsNotSilentlyWeb()
    {
        Assert.False(RuntimeConfiguration.TryCreate("client-test", "Development", (RuntimeComponent)42, "http://localhost", out _, out IReadOnlyList<StartupValidationResult> errors));
        Assert.Equal("Component", Assert.Single(errors).PropertyPath);
    }


    [Fact]
    public void ConfigurationComposesTheAlreadyValidatedIdentity()
    {
        Assert.True(RuntimeIdentity.TryCreate("client-test", "Production", RuntimeComponent.Worker,
            out RuntimeIdentity? identity, out _));
        Assert.True(RuntimeConfiguration.TryCreate(identity, "https://www.example.test",
            out RuntimeConfiguration? runtime, out _));
        Assert.Same(identity, runtime.Identity);
        Assert.Empty(typeof(RuntimeConfiguration).GetConstructors());
        Assert.All(typeof(RuntimeConfiguration).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.Throws<ArgumentNullException>(() => RuntimeConfiguration.TryCreate(null!, "https://example.test", out _, out _));
    }

    [Theory]
    [InlineData("Development", "http://localhost:8443")]
    [InlineData("Test", "https://127.0.0.1:8443")]
    [InlineData("Staging", "http://localhost:5080")]
    [InlineData("Production", "https://www.example.test:443")]
    public void BothCreationPathsPreserveThePositiveUrlMatrix(string environment, string url)
    {
        Assert.True(RuntimeIdentity.TryCreate("client-test", environment, RuntimeComponent.Web, out RuntimeIdentity? identity, out _));
        Assert.True(RuntimeConfiguration.TryCreate(identity, url, out RuntimeConfiguration? composed, out _));
        Assert.True(RuntimeConfiguration.TryCreate("client-test", environment, RuntimeComponent.Web, url, out RuntimeConfiguration? direct, out _));
        Assert.Equal(direct.PublicBaseUrl, composed.PublicBaseUrl);
    }

    [Theory]
    [InlineData("https://sub.localhost")]
    [InlineData("https://localhost.")]
    [InlineData("https://0.0.0.0")]
    [InlineData("https://[::]")]
    [InlineData("https://www.example.test:0")]
    [InlineData(" https://www.example.test")]
    [InlineData("https://www.example.test/path with space")]
    public void AdditionalProductionUrlRefusalsArePreservedInBothCreationPaths(string url)
    {
        Assert.True(RuntimeIdentity.TryCreate("client-test", "Production", RuntimeComponent.Web, out RuntimeIdentity? identity, out _));
        Assert.False(RuntimeConfiguration.TryCreate(identity, url, out RuntimeConfiguration? composed, out IReadOnlyList<StartupValidationResult> first));
        Assert.False(RuntimeConfiguration.TryCreate("client-test", "Production", RuntimeComponent.Web, url, out RuntimeConfiguration? direct, out IReadOnlyList<StartupValidationResult> second));
        Assert.Null(composed);
        Assert.Null(direct);
        Assert.Equal(first, second);
        Assert.Equal("SF-CONFIG-STARTUP-005", Assert.Single(first).ErrorCode);
    }

    [Fact]
    public void AllDiagnosticsAreCollectedInTheOriginalOrderWithoutEcho()
    {
        const string sensitive = "do-not-log";
        Assert.False(RuntimeConfiguration.TryCreate(sensitive + "!", "Invalid", (RuntimeComponent)42,
            "https://user:" + sensitive + "@example.test/#", out RuntimeConfiguration? runtime, out IReadOnlyList<StartupValidationResult> errors));
        Assert.Null(runtime);
        Assert.Equal(["ClientSlug", "Environment", "Component", "PublicBaseUrl"], errors.Select(static error => error.PropertyPath));
        Assert.Equal(["SF-CONFIG-STARTUP-003", "SF-CONFIG-STARTUP-003", "SF-CONFIG-STARTUP-003", "SF-CONFIG-STARTUP-005"],
            errors.Select(static error => error.ErrorCode));
        Assert.All(errors, static error =>
        {
            Assert.Equal("startup.configuration.schema", error.ValidationId.Value);
            Assert.Equal(StartupValidationSeverity.BlockingFailure, error.Severity);
            Assert.DoesNotContain(sensitive, error.ToString(), StringComparison.Ordinal);
        });
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)errors).Clear());
        Assert.False(RuntimeConfiguration.TryCreate("", "Production", RuntimeComponent.Web, "http://example.test", out _, out errors));
        Assert.Equal(["ClientSlug", "PublicBaseUrl"], errors.Select(static error => error.PropertyPath));
    }
}
