using Xunit;

namespace LogicGate.SiteFactory.Runtime.Contracts.Tests;

public sealed class StartupValidationContractsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidIdsAndSummariesAreRejected(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new StartupValidationId(value!));
        Assert.ThrowsAny<ArgumentException>(() => new StartupValidationResult(new("startup.a"), StartupValidationSeverity.Success, null, value!));
    }

    [Theory]
    [InlineData(StartupValidationSeverity.Warning, null)]
    [InlineData(StartupValidationSeverity.Warning, "")]
    [InlineData(StartupValidationSeverity.Warning, " ")]
    [InlineData(StartupValidationSeverity.BlockingFailure, null)]
    [InlineData(StartupValidationSeverity.BlockingFailure, "")]
    [InlineData(StartupValidationSeverity.BlockingFailure, " ")]
    public void NonSuccessRequiresAnErrorCode(StartupValidationSeverity severity, string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new StartupValidationResult(new("startup.a"), severity, code, "Safe summary."));
    }

    [Fact]
    public void InvalidContractInstancesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new StartupValidationResult(null!, StartupValidationSeverity.Success, null, "Valid."));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StartupValidationResult(new("startup.a"), (StartupValidationSeverity)42, null, "Valid."));
        Assert.Throws<ArgumentNullException>(() => new RuntimeStartupReport(null!));
        Assert.Throws<ArgumentException>(() => new RuntimeStartupReport([null!]));
    }

    [Fact]
    public void ReportsAreDefensiveImmutableSnapshotsInTheSuppliedOrder()
    {
        StartupValidationResult last = Result("startup.z", StartupValidationSeverity.Warning);
        StartupValidationResult first = Result("startup.a", StartupValidationSeverity.Success);
        List<StartupValidationResult> results = [last, first];
        RuntimeStartupReport snapshot = new(results);
        RuntimeStartupReport repeat = new(results);
        results.Clear();
        Assert.Equal([last, first], snapshot.Results);
        Assert.Equal(snapshot.Results, repeat.Results);
        Assert.Equal(1, snapshot.SchemaVersion);
        Assert.True(snapshot.IsReady);
        Assert.True(snapshot.HasWarnings);
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)snapshot.Results).Clear());
        foreach (Type type in new[] { typeof(StartupValidationId), typeof(StartupValidationResult), typeof(RuntimeStartupReport) })
        {
            Assert.All(type.GetProperties(), static property => Assert.Null(property.SetMethod));
        }
    }

    [Fact]
    public void EmptyCancelledOrBlockingReportsNeverBecomeReady()
    {
        Assert.False(new RuntimeStartupReport([]).IsReady);
        RuntimeStartupReport cancelled = new([Result("startup.a", StartupValidationSeverity.Success)], cancelled: true);
        Assert.True(cancelled.Cancelled);
        Assert.False(cancelled.IsReady);
        RuntimeStartupReport blocked = new([
            Result("startup.a", StartupValidationSeverity.BlockingFailure),
            Result("startup.b", StartupValidationSeverity.Success),
            Result("startup.c", StartupValidationSeverity.Warning)]);
        Assert.False(blocked.IsReady);
        Assert.True(blocked.HasWarnings);
        Assert.Equal(3, blocked.Results.Count);
    }

    [Fact]
    public void ExistingValueSemanticsAndLogicalDiagnosticsArePreserved()
    {
        Assert.Equal(new StartupValidationId("startup.a"), new StartupValidationId("startup.a"));
        StartupValidationResult result = new(new("startup.a"), StartupValidationSeverity.Warning, "SF-STARTUP-002", "Safe summary.", "Runtime");
        Assert.Equal("SF-STARTUP-002 Runtime Safe summary.", result.ToString());
        Assert.Equal(result, new(new("startup.a"), StartupValidationSeverity.Warning, "SF-STARTUP-002", "Safe summary.", "Runtime"));
        Assert.False(new RuntimeStartupReport([Result("startup.a", StartupValidationSeverity.Success)]).HasWarnings);
    }

    [Fact]
    public async Task PublicInterfacesCarryTheExactCallerCancellationToken()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        IStartupValidation check = new ValidationDouble(caller.Token);
        IRuntimeStartupValidator validator = new ValidatorDouble(caller.Token);
        OperationCanceledException checkFailure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.ValidateAsync(caller.Token));
        OperationCanceledException validatorFailure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => validator.ValidateAsync(caller.Token));
        Assert.Equal(caller.Token, checkFailure.CancellationToken);
        Assert.Equal(caller.Token, validatorFailure.CancellationToken);
    }

    private static StartupValidationResult Result(string id, StartupValidationSeverity severity) =>
        new(new(id), severity, severity == StartupValidationSeverity.Success ? null : "SF-STARTUP-002", "Fixed test diagnostic.");

    // Contract consumers only, not an implementation of the out-of-scope startup pipeline.
    private sealed class ValidationDouble(CancellationToken expected) : IStartupValidation
    {
        public StartupValidationId Id { get; } = new("startup.test");
        public Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken)
        {
            Assert.Equal(expected, cancellationToken);
            return Task.FromCanceled<IReadOnlyList<StartupValidationResult>>(cancellationToken);
        }
    }

    private sealed class ValidatorDouble(CancellationToken expected) : IRuntimeStartupValidator
    {
        public Task<RuntimeStartupReport> ValidateAsync(CancellationToken cancellationToken)
        {
            Assert.Equal(expected, cancellationToken);
            return Task.FromCanceled<RuntimeStartupReport>(cancellationToken);
        }
    }
}
