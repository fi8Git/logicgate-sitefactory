namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// Port for applying an explicitly supplied validated plan. No implementation or physical-root mapping is provided.
/// Implementations must honor ADR-0005 and propagate cancellation, without silently recalculating another plan.
/// </summary>
public interface IGenerationExecutor
{
    Task<GenerationExecutionResult> ExecuteAsync(GenerationExecutionRequest request, CancellationToken cancellationToken = default);
}
