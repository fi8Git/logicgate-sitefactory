using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>Minimal immutable template metadata; no loading, catalogue resolution, rendering or planning.</summary>
public sealed class TemplateManifest
{
    public TemplateManifest(
        TemplateId templateId,
        TemplateVersion templateVersion,
        IEnumerable<ProductId> supportedProductIds,
        IEnumerable<TemplateFile> files)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentNullException.ThrowIfNull(templateVersion);
        ArgumentNullException.ThrowIfNull(supportedProductIds);
        ArgumentNullException.ThrowIfNull(files);

        ProductId[] productSnapshot = supportedProductIds.ToArray();
        TemplateFile[] fileSnapshot = files.ToArray();

        if (productSnapshot.Any(static product => product is null))
        {
            throw new ArgumentException("Supported products cannot contain null entries.", nameof(supportedProductIds));
        }

        if (fileSnapshot.Any(static file => file is null))
        {
            throw new ArgumentException("Template files cannot contain null entries.", nameof(files));
        }

        TemplateId = templateId;
        TemplateVersion = templateVersion;
        SupportedProductIds = Array.AsReadOnly(productSnapshot
            .OrderBy(static product => product.Value, StringComparer.Ordinal).ToArray());
        Files = Array.AsReadOnly(fileSnapshot
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ThenBy(static file => file.Strategy)
            .ThenBy(static file => file.Source, StringComparer.Ordinal)
            .ThenBy(static file => file.Id.Value, StringComparer.Ordinal).ToArray());
    }

    public int SchemaVersion { get; } = 2;

    public TemplateId TemplateId { get; }

    public TemplateVersion TemplateVersion { get; }

    public IReadOnlyList<ProductId> SupportedProductIds { get; }

    public IReadOnlyList<TemplateFile> Files { get; }
}
