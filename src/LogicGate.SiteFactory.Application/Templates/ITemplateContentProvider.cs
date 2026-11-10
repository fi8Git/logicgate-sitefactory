using LogicGate.SiteFactory.Application.Configuration;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>
/// Prepares content for an exact resolved template version and logical source.
/// Returns null for an unsupported source/version. Never writes to the generation destination.
/// </summary>
public interface ITemplateContentProvider
{
    Task<string?> ReadAsync(TemplateManifest manifest, ClientConfigurationV1 configuration,
        string source, CancellationToken cancellationToken = default);
}
