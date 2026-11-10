namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>Pure validation of the offer supplied by the composition root, including legacy diagnostics.</summary>
public interface IConfigurationSelectionValidator
{
    IEnumerable<ConfigurationDiagnostic> ValidateSelection(SiteConfiguration site);
}
