namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>Provides declared manifests without selecting a version or assigning discovery-order priority.</summary>
public interface ITemplateCatalog
{
    /// <summary>Returns a complete snapshot, retaining duplicates so ambiguity can be detected.</summary>
    Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default);
}
