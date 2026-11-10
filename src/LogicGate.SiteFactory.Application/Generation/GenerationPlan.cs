using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>An immutable description of intended files, not an executable or validated write plan.</summary>
public sealed class GenerationPlan
{
    public GenerationPlan(
        GenerationPlanId id,
        ClientSlug clientSlug,
        ProductId productId,
        TemplateId templateId,
        IEnumerable<PlannedFile> files,
        IEnumerable<GenerationPlanDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(clientSlug);
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(diagnostics);

        PlannedFile[] fileSnapshot = files.ToArray();
        GenerationPlanDiagnostic[] diagnosticSnapshot = diagnostics.ToArray();

        if (fileSnapshot.Any(static file => file is null))
        {
            throw new ArgumentException("Planned files cannot contain null entries.", nameof(files));
        }

        if (diagnosticSnapshot.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        Id = id;
        ClientSlug = clientSlug;
        ProductId = productId;
        TemplateId = templateId;
        Files = Array.AsReadOnly(fileSnapshot
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ThenBy(static file => file.Strategy)
            .ThenBy(static file => file.Action)
            .ThenBy(static file => file.Origin.Kind)
            .ThenBy(static file => file.Origin.ComponentId, StringComparer.Ordinal)
            .ThenBy(static file => file.Id.Value, StringComparer.Ordinal)
            .ToArray());
        Diagnostics = Array.AsReadOnly(diagnosticSnapshot
            .OrderBy(static diagnostic => diagnostic.PropertyPath, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code.Value, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Severity)
            .ToArray());
    }

    public int SchemaVersion { get; } = 1;

    public GenerationPlanId Id { get; }

    public ClientSlug ClientSlug { get; }

    public ProductId ProductId { get; }

    public TemplateId TemplateId { get; }

    public IReadOnlyList<PlannedFile> Files { get; }

    public IReadOnlyList<GenerationPlanDiagnostic> Diagnostics { get; }

    /// <summary>Indicates supplied error diagnostics only; does not authorize execution.</summary>
    public bool HasErrors => Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
