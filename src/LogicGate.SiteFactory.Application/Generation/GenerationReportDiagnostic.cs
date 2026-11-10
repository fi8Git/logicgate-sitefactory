namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A publication-safe diagnostic. Raw caller/provider messages and unrecognized codes are never retained.</summary>
public sealed class GenerationReportDiagnostic
{
    internal GenerationReportDiagnostic(GenerationExecutionError error, int? operationIndex)
    {
        OperationIndex = operationIndex;
        string? explanation = error.Code.Value switch
        {
            "SF-GEN-FILE-001" => "The target path is invalid.",
            "SF-GEN-FILE-002" => "The file strategy is unsupported.",
            "SF-GEN-FILE-003" => "The file has multiple exclusive owners.",
            "SF-GEN-FILE-004" => "The file differs from the last generated content.",
            "SF-GEN-FILE-005" => "The generation source is unavailable.",
            "SF-GEN-FILE-006" => "No structured merge handler is available.",
            "SF-GEN-FILE-007" => "A structured merge conflict occurred.",
            "SF-GEN-FILE-008" => "Automatic writes to custom files are forbidden.",
            "SF-GEN-FILE-009" => "Automatic writes to protected files are forbidden.",
            "SF-GEN-FILE-010" => "File deletion is not authorized.",
            "SF-GEN-FILE-011" => "An unsafe link was detected.",
            "SF-GEN-FILE-012" => "A case-insensitive path collision was detected.",
            "SF-GEN-FILE-013" => "The destination changed during application.",
            "SF-GEN-FILE-014" => "The generation reference or planned action does not authorize the operation.",
            "SF-GEN-FILE-015" => "The file could not be inspected or written safely.",
            "SF-GEN-FILE-016" => "Local restoration failed.",
            "SF-GEN-FILE-017" => "The working tree is not clean.",
            "SF-GEN-FILE-018" => "Binary merge is unsupported.",
            "SF-GEN-FILE-019" => "The file strategy transition is invalid.",
            "SF-GEN-FILE-020" => "The file rename is unsafe.",
            "SF-GEN-FILE-021" => "An orphaned generated file has local changes.",
            "SF-GEN-FILE-022" => "Replacement confirmation is missing.",
            "SF-GEN-FILE-023" => "A merged element has unknown ownership.",
            "SF-GEN-FILE-024" => "The target is outside the generation root.",
            "SF-GEN-FILE-025" => "The file name is not portable.",
            _ => null,
        };
        Code = explanation is null ? null : error.Code.Value;
        Message = explanation ?? "An unrecognized execution error was reported; its details were omitted.";
    }

    /// <summary>Zero-based index into report operations; null for an execution-wide error.</summary>
    public int? OperationIndex { get; }

    /// <summary>ADR-0005 code, or null when the source code is not in the publication allowlist.</summary>
    public string? Code { get; }

    public string Message { get; }
}
