using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>A catalog snapshot without version selection, or one blocking loading diagnostic.</summary>
public sealed class TemplateCatalogReadResult
{
    private TemplateCatalogReadResult(IReadOnlyList<TemplateManifest> manifests, GenerationPlanDiagnostic? diagnostic)
    {
        Manifests = manifests;
        Diagnostic = diagnostic;
    }

    public IReadOnlyList<TemplateManifest> Manifests { get; }

    public GenerationPlanDiagnostic? Diagnostic { get; }

    public bool IsSuccess => Diagnostic is null;

    public static TemplateCatalogReadResult Success(IEnumerable<TemplateManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        TemplateManifest[] snapshot = manifests.ToArray();

        if (snapshot.Any(static manifest => manifest is null))
        {
            throw new ArgumentException("Catalog manifests cannot contain null entries.", nameof(manifests));
        }

        return new(Array.AsReadOnly(snapshot), null);
    }

    public static TemplateCatalogReadResult Failure(GenerationPlanDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (diagnostic.Severity != DiagnosticSeverity.Error)
        {
            throw new ArgumentException("A failed catalog read requires an error diagnostic.", nameof(diagnostic));
        }

        return new(Array.AsReadOnly(Array.Empty<TemplateManifest>()), diagnostic);
    }
}
