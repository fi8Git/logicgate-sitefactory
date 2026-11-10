using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>A read-only preflight outcome, bound to a new output or validated exact-composition generation state.</summary>
public sealed class InitialGenerationPreparation
{
    internal InitialGenerationPreparation(InitialGenerationExecutor executor)
    {
        Executor = executor;
        Status = InitialGenerationPreparationStatus.Ready;
        Diagnostics = Array.Empty<GenerationPlanDiagnostic>();
    }

    internal InitialGenerationPreparation(InitialGenerationPreparationStatus status, IEnumerable<GenerationPlanDiagnostic> diagnostics)
    {
        Status = status;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public InitialGenerationPreparationStatus Status { get; }

    public InitialGenerationExecutor? Executor { get; }

    public IReadOnlyList<GenerationPlanDiagnostic> Diagnostics { get; }
}

public enum InitialGenerationPreparationStatus
{
    Ready,
    InvalidPlan,
    InvalidTemplate,
    InvalidRoot,
    Conflict,
}
