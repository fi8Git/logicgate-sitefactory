using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>Stable, value-free errors for template loading and resolution.</summary>
public static class TemplateDiagnostics
{
    public static GenerationPlanDiagnostic UnknownTemplate { get; } = new(
        new("SF-TEMPLATE-001"), DiagnosticSeverity.Error,
        "Le template demandé est absent du catalogue.", "site.template");

    public static GenerationPlanDiagnostic UnsupportedProduct { get; } = new(
        new("SF-TEMPLATE-002"), DiagnosticSeverity.Error,
        "Le template ne prend pas en charge le produit demandé.", "site.product");

    public static GenerationPlanDiagnostic InvalidManifest { get; } = new(
        new("SF-TEMPLATE-003"), DiagnosticSeverity.Error,
        "Le catalogue contient un manifest de template invalide.", "templates");

    public static GenerationPlanDiagnostic AmbiguousTemplate { get; } = new(
        new("SF-TEMPLATE-004"), DiagnosticSeverity.Error,
        "Plusieurs manifests correspondent au template demandé.", "site.template");
}
