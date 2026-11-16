namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A trusted, explicitly supplied source of template declarations and bytes, with no destination access.</summary>
/// <remarks>Independent of module metadata. The host will bind a provider to a module; the SDK assigns no ownership.</remarks>
public interface ISiteFactoryTemplateProvider
{
    /// <summary>Returns a complete snapshot, retaining duplicates for the engine to detect ambiguity.</summary>
    Task<TemplateCatalogResult> ReadCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads an exact template and logical content key, never a destination path. Cancellation must propagate.</summary>
    Task<TemplateContentResult> ReadContentAsync(TemplateIdentity templateIdentity, string contentKey,
        TemplateRenderContext context, CancellationToken cancellationToken = default);
}
