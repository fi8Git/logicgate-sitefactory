using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// Builds initial logical intentions from validated configuration and a resolved manifest.
/// No target state is known: Create requires an absent target to be verified before any future execution.
/// </summary>
public static class GenerationPlanBuilder
{
    public static GenerationPlan Build(
        GenerationPlanId id,
        ClientConfigurationV1 configuration,
        TemplateManifest template)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(template);

        ClientSlug clientSlug = new(configuration.Client.Slug);
        ProductId productId = new(configuration.Site.Product);
        TemplateId templateId = new(configuration.Site.Template);
        List<GenerationPlanDiagnostic> diagnostics = [];

        // Validate the pairing supplied by the caller, without resolving the catalog a second time.
        if (!TemplateManifestValidation.IsValid(template))
        {
            diagnostics.Add(TemplateDiagnostics.InvalidManifest);
        }

        if (template.TemplateId != templateId)
        {
            diagnostics.Add(new(new("SF-GEN-002"), DiagnosticSeverity.Error,
                "Le manifest résolu ne correspond pas au template de la configuration.", "site.template"));
        }

        if (!template.SupportedProductIds.Contains(productId))
        {
            diagnostics.Add(TemplateDiagnostics.UnsupportedProduct);
        }

        diagnostics.AddRange(GenerationPlanPathValidator.ValidatePaths(template.Files.Select(static file => file.RelativePath)));

        if (diagnostics.Count != 0)
        {
            return new(id, clientSlug, productId, templateId, [], diagnostics);
        }

        PlannedFileOrigin origin = new(PlannedFileOriginKind.Template, template.TemplateId.Value);
        List<PlannedFile> files = [];

        foreach (TemplateFile file in template.Files)
        {
            PlannedFileAction action = file.Strategy switch
            {
                FileGenerationStrategy.Generated or FileGenerationStrategy.CreateOnce => PlannedFileAction.Create,
                FileGenerationStrategy.Custom or FileGenerationStrategy.Protected => PlannedFileAction.Preserve,
                FileGenerationStrategy.Merge => PlannedFileAction.Conflict,
                _ => throw new ArgumentOutOfRangeException(nameof(template)),
            };

            if (file.Strategy == FileGenerationStrategy.Merge)
            {
                // The minimal manifest has no merge handler. Do not silently plan a textual merge or creation.
                diagnostics.Add(new(new("SF-GEN-FILE-006"), DiagnosticSeverity.Error,
                    "Le manifest minimal ne déclare aucun handler de fusion structurée.", file.RelativePath));
            }

            files.Add(new(file.Id, file.RelativePath, origin, file.Strategy, action));
        }

        return new(id, clientSlug, productId, templateId, files, diagnostics);
    }
}
