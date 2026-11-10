using LogicGate.SiteFactory.Runtime.Contracts;

namespace LogicGate.SiteFactory.Runtime;

/// <summary>Initial, independent configuration checks; no I/O, retries or dependency graph.</summary>
public sealed class RuntimeStartupValidator : IRuntimeStartupValidator
{
    private readonly IReadOnlyList<IStartupValidation> validations;

    public RuntimeStartupValidator(IEnumerable<IStartupValidation> validations)
    {
        ArgumentNullException.ThrowIfNull(validations);
        IStartupValidation[] snapshot = validations.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(static validation => validation is null || validation.Id is null)
            || snapshot.Select(static validation => validation.Id).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("SF-STARTUP-006 Validations must be nonempty and have unique identifiers.", nameof(validations));
        this.validations = Array.AsReadOnly(snapshot.OrderBy(static validation => validation.Id.Value, StringComparer.Ordinal).ToArray());
    }

    public async Task<RuntimeStartupReport> ValidateAsync(CancellationToken cancellationToken)
    {
        List<StartupValidationResult> results = [];
        try
        {
            foreach (IStartupValidation validation in validations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<StartupValidationResult> current = await validation.ValidateAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (current.Count == 0 || current.Any(result => result is null || result.ValidationId != validation.Id))
                    throw new InvalidOperationException("SF-STARTUP-006 A validation returned an invalid result contract.");
                results.AddRange(current);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            results.Add(new(new("startup.pipeline"), StartupValidationSeverity.BlockingFailure,
                "SF-STARTUP-005", "Startup validation was cancelled."));
            return new(results, cancelled: true);
        }

        return new(results);
    }
}
