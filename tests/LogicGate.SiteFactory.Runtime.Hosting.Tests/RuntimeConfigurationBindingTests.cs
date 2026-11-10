using LogicGate.SiteFactory.Runtime.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Hosting.Tests;

public sealed class RuntimeConfigurationBindingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void BindingProducesASnapshotNotALiveConfigurationDependency()
    {
        using ConfigurationManager configuration = new();
        configuration["environment"] = "Development";
        configuration["SiteFactory:Runtime:ClientSlug"] = "client-test";
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "http://localhost";
        RuntimeConfiguration runtime = RuntimeConfigurationReader.Read(configuration, "Development", RuntimeComponent.Worker);
        configuration["SiteFactory:Runtime:ClientSlug"] = "different-client";
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "invalid";
        Assert.Equal("client-test", runtime.Identity.ClientSlug);
        Assert.Equal(new Uri("http://localhost"), runtime.PublicBaseUrl);
        Assert.All(typeof(RuntimeConfiguration).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.All(typeof(RuntimeIdentity).GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(typeof(RuntimeIdentity).Assembly.GetReferencedAssemblies(), static assembly =>
            assembly.Name!.StartsWith("Microsoft.", StringComparison.Ordinal)
            || assembly.Name is "Client.Infrastructure" or "Client.Web" or "Client.Worker");
    }

    [Fact]
    public void ImplicitProductionAndConflictingEnvironmentAreRejected()
    {
        using ConfigurationManager configuration = new();
        configuration["SiteFactory:Runtime:ClientSlug"] = "client-test";
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://www.example.test";
        RuntimeConfigurationException absent = Assert.Throws<RuntimeConfigurationException>(() =>
            RuntimeConfigurationReader.Read(configuration, "Production", RuntimeComponent.Web));
        Assert.Contains("SF-CONFIG-STARTUP-003 Environment", absent.Message, StringComparison.Ordinal);
        configuration["environment"] = "Development";
        RuntimeConfigurationException conflict = Assert.Throws<RuntimeConfigurationException>(() =>
            RuntimeConfigurationReader.Read(configuration, "Production", RuntimeComponent.Web));
        Assert.Contains("SF-CONFIG-STARTUP-006 Environment", conflict.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SiteFactory:Runtime:Unknown", "value")]
    [InlineData("SiteFactory:Runtime:Component", "migrator")]
    [InlineData("SiteFactory:Runtime:Environment", "Production")]
    [InlineData("SiteFactory:Runtime:ClientSlug:Nested", "value")]
    public void StructuredBindingRejectsUnknownOrNestedProperties(string key, string value)
    {
        using ConfigurationManager configuration = new();
        configuration[key] = value;
        RuntimeConfigurationException exception = Assert.Throws<RuntimeConfigurationException>(() =>
            RuntimeConfigurationReader.Read(configuration, "Development", RuntimeComponent.Web));
        Assert.StartsWith("SF-CONFIG-STARTUP-002 Runtime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSectionHasAStableDiagnostic()
    {
        using ConfigurationManager configuration = new();
        RuntimeConfigurationException exception = Assert.Throws<RuntimeConfigurationException>(() =>
            RuntimeConfigurationReader.Read(configuration, "Development", RuntimeComponent.Web));
        Assert.StartsWith("SF-CONFIG-STARTUP-001 Runtime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActualConfigurationCheckValidatesBothComponentsWithoutChangingConfiguration()
    {
        using ConfigurationManager configuration = ValidConfiguration();
        KeyValuePair<string, string?>[] before = configuration.AsEnumerable().ToArray();
        foreach (RuntimeComponent component in new[] { RuntimeComponent.Web, RuntimeComponent.Worker })
        {
            RuntimeConfigurationStartupValidation check = new(configuration, "Production", component);
            Assert.Null(check.Configuration);
            RuntimeStartupReport report = await new RuntimeStartupValidator([check]).ValidateAsync(Token);
            Assert.True(report.IsReady);
            Assert.False(report.Cancelled);
            Assert.False(report.HasWarnings);
            StartupValidationResult result = Assert.Single(report.Results);
            Assert.Equal("startup.configuration.schema", result.ValidationId.Value);
            Assert.Equal(StartupValidationSeverity.Success, result.Severity);
            Assert.Null(result.ErrorCode);
            Assert.Equal(component, Assert.IsType<RuntimeConfiguration>(check.Configuration).Identity.Component);
        }
        Assert.Equal(before, configuration.AsEnumerable());
        Assert.DoesNotContain(typeof(IRuntimeStartupValidator).Assembly.GetReferencedAssemblies(), static assembly =>
            assembly.Name!.StartsWith("Microsoft.", StringComparison.Ordinal) || assembly.Name == "Client.Infrastructure");
    }

    [Theory]
    [InlineData("SiteFactory:Runtime:ClientSlug", "", "SF-CONFIG-STARTUP-003", "ClientSlug")]
    [InlineData("environment", "Invalid", "SF-CONFIG-STARTUP-003", "Environment")]
    [InlineData("SiteFactory:Runtime:PublicBaseUrl", "https://user:do-not-log@example.test", "SF-CONFIG-STARTUP-005", "PublicBaseUrl")]
    [InlineData("SiteFactory:Runtime:PublicBaseUrl", "http://example.test", "SF-CONFIG-STARTUP-005", "PublicBaseUrl")]
    [InlineData("SiteFactory:Runtime:Unknown", "do-not-log", "SF-CONFIG-STARTUP-002", "Runtime")]
    public async Task ExistingConfigurationFailuresBecomeStructuredBlockers(string key, string value, string code, string property)
    {
        using ConfigurationManager configuration = ValidConfiguration();
        configuration[key] = value;
        RuntimeConfigurationStartupValidation check = new(configuration, configuration["environment"]!, RuntimeComponent.Web);
        RuntimeStartupReport report = await new RuntimeStartupValidator([check]).ValidateAsync(Token);
        Assert.False(report.IsReady);
        Assert.Null(check.Configuration);
        StartupValidationResult result = Assert.Single(report.Results);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(property, result.PropertyPath);
        Assert.Equal(StartupValidationSeverity.BlockingFailure, result.Severity);
        Assert.DoesNotContain("do-not-log", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSectionAndUnknownComponentNeverBecomeReady()
    {
        using ConfigurationManager empty = new();
        RuntimeStartupReport missing = await new RuntimeStartupValidator(
            [new RuntimeConfigurationStartupValidation(empty, "Production", RuntimeComponent.Worker)]).ValidateAsync(Token);
        Assert.Equal("SF-CONFIG-STARTUP-001", Assert.Single(missing.Results).ErrorCode);
        Assert.False(missing.IsReady);
        using ConfigurationManager configuration = ValidConfiguration();
        RuntimeStartupReport unknown = await new RuntimeStartupValidator(
            [new RuntimeConfigurationStartupValidation(configuration, "Production", (RuntimeComponent)42)]).ValidateAsync(Token);
        Assert.Equal("Component", Assert.Single(unknown.Results).PropertyPath);
        Assert.False(unknown.IsReady);
    }


    [Fact]
    public async Task CancellationBeforeReadingRetainsThePreviousSnapshot()
    {
        using ConfigurationManager configuration = ValidConfiguration();
        int reads = 0;
        ObservedConfiguration observed = new(configuration, _ => reads++);
        RuntimeConfigurationStartupValidation check = new(observed, "Production", RuntimeComponent.Web);
        await check.ValidateAsync(Token);
        RuntimeConfiguration previous = Assert.IsType<RuntimeConfiguration>(check.Configuration);
        reads = 0;
        configuration["SiteFactory:Runtime:ClientSlug"] = "different-client";
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => check.ValidateAsync(cancellation.Token));

        Assert.Equal(0, reads);
        Assert.Same(previous, check.Configuration);
        Assert.Equal("client-test", previous.Identity.ClientSlug);
    }

    [Fact]
    public async Task CancellationAfterReadingCannotPublishANewSnapshot()
    {
        using ConfigurationManager configuration = ValidConfiguration();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bool cancelOnRead = false;
        int cancellationReads = 0;
        ObservedConfiguration observed = new(configuration, key =>
        {
            if (cancelOnRead && key == "PublicBaseUrl")
            {
                cancellationReads++;
                cancellation.Cancel();
            }
        });
        RuntimeConfigurationStartupValidation check = new(observed, "Production", RuntimeComponent.Worker);
        await check.ValidateAsync(Token);
        RuntimeConfiguration previous = Assert.IsType<RuntimeConfiguration>(check.Configuration);
        configuration["SiteFactory:Runtime:ClientSlug"] = "different-client";
        cancelOnRead = true;

        await Assert.ThrowsAsync<OperationCanceledException>(() => check.ValidateAsync(cancellation.Token));

        Assert.Equal(1, cancellationReads);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Null(check.Configuration);
        Assert.Equal("client-test", previous.Identity.ClientSlug);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderErrorsAndUnrelatedCancellationAreNotConvertedToDiagnostics(bool unrelatedCancellation)
    {
        using ConfigurationManager configuration = ValidConfiguration();
        Exception failure = unrelatedCancellation ? new OperationCanceledException() : new InvalidOperationException("Provider failure.");
        bool failOnRead = false;
        ObservedConfiguration observed = new(configuration, _ =>
        {
            if (failOnRead) throw failure;
        });
        RuntimeConfigurationStartupValidation check = new(observed, "Production", RuntimeComponent.Web);
        await check.ValidateAsync(Token);
        Assert.NotNull(check.Configuration);
        failOnRead = true;

        Exception readerError = Assert.ThrowsAny<Exception>(() => RuntimeConfigurationReader.Read(observed, "Production", RuntimeComponent.Web));
        Exception validationError = await Assert.ThrowsAnyAsync<Exception>(() => check.ValidateAsync(Token));

        Assert.Same(failure, readerError);
        Assert.Same(failure, validationError);
        Assert.False(Token.IsCancellationRequested);
        Assert.Null(check.Configuration);
    }

    [Fact]
    public void ExceptionDiagnosticsAreAReadOnlyDefensiveSnapshot()
    {
        StartupValidationResult first = new(new("startup.configuration.schema"), StartupValidationSeverity.BlockingFailure,
            "SF-CONFIG-STARTUP-003", "An explicit environment is required.", "Environment");
        StartupValidationResult second = new(new("startup.configuration.schema"), StartupValidationSeverity.BlockingFailure,
            "SF-CONFIG-STARTUP-005", "An absolute URL is required.", "PublicBaseUrl");
        List<StartupValidationResult> diagnostics = [first, second];
        RuntimeConfigurationException exception = new(diagnostics);
        string message = exception.Message;
        diagnostics.Clear();

        Assert.Equal([first, second], exception.Diagnostics);
        Assert.Equal($"{first}\n{second}", message);
        Assert.Equal(message, exception.Message);
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)exception.Diagnostics).Clear());
    }

    [Fact]
    public async Task RevalidationReadsOnlyOnExplicitCallsAndClearsTheSnapshotOnFailure()
    {
        using ConfigurationManager configuration = ValidConfiguration();
        RuntimeConfigurationStartupValidation check = new(configuration, "Production", RuntimeComponent.Web);
        StartupValidationResult first = Assert.Single(await check.ValidateAsync(Token));
        Assert.Equal(StartupValidationSeverity.Success, first.Severity);
        RuntimeConfiguration original = Assert.IsType<RuntimeConfiguration>(check.Configuration);
        configuration["SiteFactory:Runtime:ClientSlug"] = "different-client";
        Assert.Same(original, check.Configuration);

        Assert.Equal(StartupValidationSeverity.Success, Assert.Single(await check.ValidateAsync(Token)).Severity);
        RuntimeConfiguration changed = Assert.IsType<RuntimeConfiguration>(check.Configuration);
        Assert.NotSame(original, changed);
        Assert.Equal("different-client", changed.Identity.ClientSlug);
        Assert.Equal("client-test", original.Identity.ClientSlug);
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://user:do-not-log@example.test";

        StartupValidationResult blocker = Assert.Single(await check.ValidateAsync(Token));

        Assert.Null(check.Configuration);
        Assert.Equal(StartupValidationSeverity.BlockingFailure, blocker.Severity);
        Assert.Equal("SF-CONFIG-STARTUP-005", blocker.ErrorCode);
        Assert.Equal("PublicBaseUrl", blocker.PropertyPath);
        Assert.DoesNotContain("do-not-log", blocker.ToString(), StringComparison.Ordinal);
        Assert.Equal(new Uri("https://www.example.test"), changed.PublicBaseUrl);
    }

    private static ConfigurationManager ValidConfiguration()
    {
        ConfigurationManager configuration = new();
        configuration["environment"] = "Production";
        configuration["SiteFactory:Runtime:ClientSlug"] = "client-test";
        configuration["SiteFactory:Runtime:PublicBaseUrl"] = "https://www.example.test";
        return configuration;
    }

    private class ObservedConfiguration(IConfiguration inner, Action<string> observe) : IConfiguration
    {
        protected void Observe(string key) => observe(key);

        public string? this[string key]
        {
            get
            {
                observe(key);
                return inner[key];
            }
            set => throw new NotSupportedException("This test adapter is read-only.");
        }

        public IConfigurationSection GetSection(string key)
        {
            observe(key);
            return new ObservedSection(inner.GetSection(key), observe);
        }

        public IEnumerable<IConfigurationSection> GetChildren() =>
            inner.GetChildren().Select(section => new ObservedSection(section, observe));

        public IChangeToken GetReloadToken() => inner.GetReloadToken();
    }

    private sealed class ObservedSection : ObservedConfiguration, IConfigurationSection
    {
        private readonly IConfigurationSection section;

        public ObservedSection(IConfigurationSection section, Action<string> observe) : base(section, observe)
        {
            this.section = section;
        }

        public string Key => section.Key;
        public string Path => section.Path;
        public string? Value
        {
            get
            {
                Observe(section.Path);
                return section.Value;
            }
            set => throw new NotSupportedException("This test adapter is read-only.");
        }
    }
}
