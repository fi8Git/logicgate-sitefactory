using LogicGate.SiteFactory.Runtime.Contracts;
using Xunit;

namespace LogicGate.SiteFactory.Runtime.Tests;

public sealed class RuntimeStartupValidatorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MultipleChecksExecuteAndAggregateInOrdinalOrderRegardlessOfRegistrationOrder()
    {
        List<string> calls = [];
        Check first = Fixed("startup.a", StartupValidationSeverity.Success, calls);
        Check second = Fixed("startup.z", StartupValidationSeverity.Warning, calls);
        RuntimeStartupReport forward = await new RuntimeStartupValidator([first, second]).ValidateAsync(Token);
        Assert.Equal(["startup.a", "startup.z"], calls);
        calls.Clear();
        RuntimeStartupReport reverse = await new RuntimeStartupValidator([second, first]).ValidateAsync(Token);
        Assert.Equal(["startup.a", "startup.z"], calls);
        Assert.Equal(forward.Results, reverse.Results);
        Assert.True(reverse.IsReady);
        Assert.True(reverse.HasWarnings);
    }

    [Fact]
    public async Task BlockingFailureCannotBeOverriddenByLaterSuccessOrWarning()
    {
        List<string> calls = [];
        RuntimeStartupReport report = await new RuntimeStartupValidator([
            Fixed("startup.a", StartupValidationSeverity.BlockingFailure, calls),
            Fixed("startup.b", StartupValidationSeverity.Success, calls),
            Fixed("startup.c", StartupValidationSeverity.Warning, calls)]).ValidateAsync(Token);
        Assert.False(report.IsReady);
        Assert.True(report.HasWarnings);
        Assert.Equal(3, report.Results.Count);
        Assert.Equal(["startup.a", "startup.b", "startup.c"], calls);
    }

    [Fact]
    public void EmptyOrDuplicateValidationRegistryIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeStartupValidator([]));
        Check check = Fixed("startup.same", StartupValidationSeverity.Success, []);
        Assert.Throws<ArgumentException>(() => new RuntimeStartupValidator([check, check]));
        Assert.False(new RuntimeStartupReport([]).IsReady);
    }

    [Fact]
    public async Task RegistrationsAndReportsAreImmutableSnapshots()
    {
        List<IStartupValidation> checks = [Fixed("startup.a", StartupValidationSeverity.Success, [])];
        RuntimeStartupValidator validator = new(checks);
        checks.Clear();
        RuntimeStartupReport report = await validator.ValidateAsync(Token);
        Assert.True(report.IsReady);
        List<StartupValidationResult> results = report.Results.ToList();
        RuntimeStartupReport snapshot = new(results);
        results.Clear();
        Assert.Single(snapshot.Results);
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)snapshot.Results).Clear());
    }

    [Fact]
    public async Task AlreadyCancelledPipelineDoesNotInvokeChecks()
    {
        List<string> calls = [];
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        RuntimeStartupReport report = await new RuntimeStartupValidator(
            [Fixed("startup.a", StartupValidationSeverity.Success, calls)]).ValidateAsync(cancellation.Token);
        Assert.True(report.Cancelled);
        Assert.False(report.IsReady);
        Assert.Equal("SF-STARTUP-005", Assert.Single(report.Results).ErrorCode);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task CancellationDuringAValidationPropagatesAndPreventsSubsequentChecks()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Check waiting = new(new("startup.a"), async token =>
        {
            Assert.Equal(cancellation.Token, token);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable.");
        });
        List<string> calls = [];
        Task<RuntimeStartupReport> validating = new RuntimeStartupValidator(
            [waiting, Fixed("startup.b", StartupValidationSeverity.Success, calls)]).ValidateAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancellation.CancelAsync();
        RuntimeStartupReport report = await validating.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.True(report.Cancelled);
        Assert.False(report.IsReady);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task CancellationAtTheLastCheckCannotReturnReady()
    {
        using CancellationTokenSource cancellation = new();
        StartupValidationId id = new("startup.a");
        Check last = new(id, async token =>
        {
            await cancellation.CancelAsync();
            return [new(id, StartupValidationSeverity.Success, null, "Valid.")];
        });
        RuntimeStartupReport report = await new RuntimeStartupValidator([last]).ValidateAsync(cancellation.Token);
        Assert.True(report.Cancelled);
        Assert.False(report.IsReady);
    }

    [Fact]
    public async Task ProgrammingErrorsAndUnrelatedCancellationAreNotAbsorbed()
    {
        InvalidOperationException error = new("Programming failure.");
        Check failing = new(new("startup.a"), _ => throw error);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RuntimeStartupValidator([failing]).ValidateAsync(Token)));
        Check unrelated = new(new("startup.a"), _ => throw new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => new RuntimeStartupValidator([unrelated]).ValidateAsync(Token));
    }

    [Fact]
    public async Task InvalidCheckResultContractsAreNotConsideredReady()
    {
        Check empty = new(new("startup.a"), _ => Task.FromResult<IReadOnlyList<StartupValidationResult>>([]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RuntimeStartupValidator([empty]).ValidateAsync(Token));
        Check wrongId = new(new("startup.a"), _ => Task.FromResult<IReadOnlyList<StartupValidationResult>>(
            [new(new("startup.other"), StartupValidationSeverity.Success, null, "Valid.")]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RuntimeStartupValidator([wrongId]).ValidateAsync(Token));
    }

    [Fact]
    public void NullRegistryIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RuntimeStartupValidator(null!));
    }

    [Fact]
    public void NullValidationOrIdentifierIsRejectedWithTheExistingCode()
    {
        ArgumentException nullValidation = Assert.Throws<ArgumentException>(() => new RuntimeStartupValidator([null!]));
        ArgumentException nullIdentifier = Assert.Throws<ArgumentException>(() => new RuntimeStartupValidator(
            [new Check(null!, _ => throw new InvalidOperationException("Unexpected invocation."))]));
        Assert.StartsWith("SF-STARTUP-006", nullValidation.Message, StringComparison.Ordinal);
        Assert.StartsWith("SF-STARTUP-006", nullIdentifier.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EqualIdentifiersOnDistinctValidationsAreRejected()
    {
        Check first = Fixed("startup.same", StartupValidationSeverity.Success, []);
        Check second = Fixed("startup.same", StartupValidationSeverity.Warning, []);
        Assert.NotSame(first, second);
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new RuntimeStartupValidator([first, second]));
        Assert.StartsWith("SF-STARTUP-006", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullResultElementIsRejectedWithTheExistingCode()
    {
        Check check = new(new("startup.a"), _ => Task.FromResult<IReadOnlyList<StartupValidationResult>>([null!]));
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new RuntimeStartupValidator([check]).ValidateAsync(Token));
        Assert.StartsWith("SF-STARTUP-006", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChecksAreSequentialAndMultipleResultsRetainTheirSuppliedOrder()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IReadOnlyList<StartupValidationResult>> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StartupValidationId id = new("startup.a");
        StartupValidationResult firstResult = new(id, StartupValidationSeverity.Warning, "SF-STARTUP-002", "First.");
        StartupValidationResult secondResult = new(id, StartupValidationSeverity.Success, null, "Second.");
        List<string> calls = [];
        Check first = new(id, async token =>
        {
            Assert.Equal(cancellation.Token, token);
            calls.Add(id.Value);
            entered.SetResult();
            return await release.Task.WaitAsync(token);
        });
        Check second = new(new("startup.z"), token =>
        {
            Assert.Equal(cancellation.Token, token);
            calls.Add("startup.z");
            return Task.FromResult<IReadOnlyList<StartupValidationResult>>(
                [new(new("startup.z"), StartupValidationSeverity.Success, null, "Third.")]);
        });
        Task<RuntimeStartupReport> running = new RuntimeStartupValidator([second, first]).ValidateAsync(cancellation.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal(["startup.a"], calls);
        Assert.False(running.IsCompleted);
        release.SetResult([firstResult, secondResult]);
        RuntimeStartupReport report = await running.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal(["startup.a", "startup.z"], calls);
        Assert.Equal([firstResult, secondResult], report.Results.Take(2));
        Assert.Equal(["First.", "Second.", "Third."], report.Results.Select(static result => result.Summary));
        Assert.True(report.IsReady);
        Assert.True(report.HasWarnings);
    }

    [Fact]
    public async Task CancellationRetainsPriorResultsAndAppendsThePipelineBlocker()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IReadOnlyList<StartupValidationResult>> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> calls = [];
        Check waiting = new(new("startup.b"), async token =>
        {
            Assert.Equal(cancellation.Token, token);
            entered.SetResult();
            return await pending.Task.WaitAsync(token);
        });
        Task<RuntimeStartupReport> running = new RuntimeStartupValidator([
            Fixed("startup.a", StartupValidationSeverity.Warning, calls), waiting,
            Fixed("startup.c", StartupValidationSeverity.Success, calls)]).ValidateAsync(cancellation.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancellation.CancelAsync();
        RuntimeStartupReport report = await running.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.True(report.Cancelled);
        Assert.False(report.IsReady);
        Assert.True(report.HasWarnings);
        Assert.Equal(["startup.a"], calls);
        Assert.Equal(["startup.a", "startup.pipeline"], report.Results.Select(static result => result.ValidationId.Value));
        Assert.Equal(["SF-STARTUP-002", "SF-STARTUP-005"], report.Results.Select(static result => result.ErrorCode));
        Assert.Equal(StartupValidationSeverity.BlockingFailure, report.Results[1].Severity);
        Assert.Throws<NotSupportedException>(() => ((IList<StartupValidationResult>)report.Results).Clear());
    }

    private static Check Fixed(string name, StartupValidationSeverity severity, List<string> calls)
    {
        StartupValidationId id = new(name);
        return new(id, token =>
        {
            token.ThrowIfCancellationRequested();
            calls.Add(name);
            return Task.FromResult<IReadOnlyList<StartupValidationResult>>(
                [new(id, severity, severity == StartupValidationSeverity.Success ? null : "SF-STARTUP-002", "Fixed test diagnostic.")]);
        });
    }

    private sealed class Check(StartupValidationId id, Func<CancellationToken, Task<IReadOnlyList<StartupValidationResult>>> validate) : IStartupValidation
    {
        public StartupValidationId Id { get; } = id;
        public Task<IReadOnlyList<StartupValidationResult>> ValidateAsync(CancellationToken cancellationToken) => validate(cancellationToken);
    }
}
