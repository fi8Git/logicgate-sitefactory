using LogicGate.SiteFactory.Application.Configuration;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A stable code and a human explanation for a conflict or failure, without exception/provider details.</summary>
public sealed record GenerationExecutionError
{
    public GenerationExecutionError(DiagnosticCode code, string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }

    public DiagnosticCode Code { get; }

    public string Message { get; }
}
