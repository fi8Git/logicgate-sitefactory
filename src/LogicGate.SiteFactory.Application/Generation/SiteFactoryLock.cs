using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// Minimal module-free composition candidate. It does not certify generation or client quality gates.
/// Only resolved structural metadata is retained, never configuration content or physical sources.
/// </summary>
public sealed class SiteFactoryLock
{
    public SiteFactoryLock(SiteFactoryVersion siteFactoryVersion, ClientConfigurationV1 configuration, TemplateManifest resolvedTemplate)
    {
        ArgumentNullException.ThrowIfNull(siteFactoryVersion);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(resolvedTemplate);

        if (configuration.Modules.Enabled.Count != 0)
        {
            throw new ArgumentException("A lock cannot be created for unresolved modules; module resolution is not implemented.", nameof(configuration));
        }

        if (!ClientConfigurationV1Validator.Validate(configuration).IsValid)
        {
            throw new ArgumentException("A validated client configuration is required.", nameof(configuration));
        }

        ProductId productId = new(configuration.Site.Product);
        if (!TemplateManifestValidation.IsValid(resolvedTemplate)
            || !string.Equals(resolvedTemplate.TemplateId.Value, configuration.Site.Template, StringComparison.Ordinal)
            || !resolvedTemplate.SupportedProductIds.Contains(productId))
        {
            throw new ArgumentException("A valid resolved template matching the configuration and product is required.", nameof(resolvedTemplate));
        }

        SiteFactoryVersion = siteFactoryVersion;
        ConfigurationSchemaVersion = configuration.SchemaVersion;
        ProductId = productId;
        TemplateId = resolvedTemplate.TemplateId;
        TemplateVersion = resolvedTemplate.TemplateVersion;
        TemplateManifestSchemaVersion = resolvedTemplate.SchemaVersion;
    }

    public int SchemaVersion { get; } = 1;

    public SiteFactoryVersion SiteFactoryVersion { get; }

    public int ConfigurationSchemaVersion { get; }

    public ProductId ProductId { get; }

    public TemplateId TemplateId { get; }

    public TemplateVersion TemplateVersion { get; }

    public int TemplateManifestSchemaVersion { get; }
}
