namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>Maps a supplied manifest document to contracts without discovering sources or writing files.</summary>
public interface ITemplateManifestReader
{
    TemplateCatalogReadResult Read(string document);
}
