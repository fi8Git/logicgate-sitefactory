using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>One exact resolved manifest, or one blocking diagnostic. No generated files or version upgrade.</summary>
public sealed class TemplateResolutionResult
{
    private TemplateResolutionResult(TemplateManifest? template, GenerationPlanDiagnostic? diagnostic)
    {
        Template = template;
        Diagnostic = diagnostic;
    }

    public TemplateManifest? Template { get; }

    public GenerationPlanDiagnostic? Diagnostic { get; }

    public bool IsSuccess => Template is not null;

    public static TemplateResolutionResult Success(TemplateManifest template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new(template, null);
    }

    public static TemplateResolutionResult Failure(GenerationPlanDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (diagnostic.Severity != DiagnosticSeverity.Error)
        {
            throw new ArgumentException("Failed resolution requires an error diagnostic.", nameof(diagnostic));
        }

        return new(null, diagnostic);
    }
}
