using LogicGate.SiteFactory.Application.Configuration;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>Resolves an already validated configuration against a catalog, without choosing a latest version.</summary>
public sealed class TemplateResolver
{
    private readonly ITemplateCatalog catalog;

    public TemplateResolver(ITemplateCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.catalog = catalog;
    }

    public async Task<TemplateResolutionResult> ResolveAsync(
        ClientConfigurationV1 configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        TemplateCatalogReadResult read = await catalog.ReadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (read.Diagnostic is not null)
        {
            return TemplateResolutionResult.Failure(read.Diagnostic);
        }

        foreach (TemplateManifest manifest in read.Manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // TemplateVersion already guarantees an exact stable M.m.p version.
            // These checks concern declarations only, not physical file collisions or source presence.
            if (!TemplateManifestValidation.IsValid(manifest))
            {
                return TemplateResolutionResult.Failure(TemplateDiagnostics.InvalidManifest);
            }
        }

        TemplateManifest[] candidates = read.Manifests.Where(manifest =>
            string.Equals(manifest.TemplateId.Value, configuration.Site.Template, StringComparison.Ordinal)).ToArray();

        if (candidates.Length == 0)
        {
            return TemplateResolutionResult.Failure(TemplateDiagnostics.UnknownTemplate);
        }

        // V1 configuration supplies no version selector. Even identical duplicates are ambiguous.
        if (candidates.Length != 1)
        {
            return TemplateResolutionResult.Failure(TemplateDiagnostics.AmbiguousTemplate);
        }

        TemplateManifest selected = candidates[0];

        if (!selected.SupportedProductIds.Any(product =>
                string.Equals(product.Value, configuration.Site.Product, StringComparison.Ordinal)))
        {
            return TemplateResolutionResult.Failure(TemplateDiagnostics.UnsupportedProduct);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return TemplateResolutionResult.Success(selected);
    }
}
