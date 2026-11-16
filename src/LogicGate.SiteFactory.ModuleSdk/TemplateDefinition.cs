namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A complete immutable template declaration. Product and file duplicates are retained for engine validation.</summary>
public sealed class TemplateDefinition
{
    public TemplateDefinition(TemplateIdentity identity, IEnumerable<string> supportedProductIds,
        IEnumerable<TemplateFileDefinition> files)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(supportedProductIds);
        ArgumentNullException.ThrowIfNull(files);
        string[] products = supportedProductIds.ToArray();
        TemplateFileDefinition[] declarations = files.ToArray();
        if (products.Length == 0 || products.Any(static product => string.IsNullOrWhiteSpace(product) || product.Any(char.IsControl)))
            throw new ArgumentException("At least one non-empty product ID without control characters is required.", nameof(supportedProductIds));
        if (declarations.Length == 0 || declarations.Any(static file => file is null))
            throw new ArgumentException("At least one non-null file declaration is required.", nameof(files));
        Identity = identity;
        SupportedProductIds = Array.AsReadOnly(products);
        Files = Array.AsReadOnly(declarations);
    }

    public TemplateIdentity Identity { get; }
    public IReadOnlyList<string> SupportedProductIds { get; }
    public IReadOnlyList<TemplateFileDefinition> Files { get; }
}
