namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>Checks declarations only, without inspecting physical sources or a target filesystem.</summary>
internal static class TemplateManifestValidation
{
    internal static bool IsValid(TemplateManifest manifest) =>
        manifest.SupportedProductIds.Count != 0 && manifest.Files.Count != 0
        && manifest.SupportedProductIds.Distinct().Count() == manifest.SupportedProductIds.Count
        && manifest.Files.Select(static file => file.RelativePath).Distinct(StringComparer.Ordinal).Count() == manifest.Files.Count
        && manifest.Files.Select(static file => file.Id).Distinct().Count() == manifest.Files.Count;
}
