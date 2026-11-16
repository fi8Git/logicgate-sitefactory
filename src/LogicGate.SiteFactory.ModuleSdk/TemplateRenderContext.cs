namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>Non-secret rendering inputs, without client configuration, services or destination information.</summary>
public sealed class TemplateRenderContext
{
    /// <summary>Retain this constructor when adding future optional inputs through additive overloads/defaults.</summary>
    public TemplateRenderContext(string displayName, string defaultLocale)
    {
        TemplateContractValidation.RequireText(displayName, nameof(displayName));
        TemplateContractValidation.RequireText(defaultLocale, nameof(defaultLocale));
        DisplayName = displayName;
        DefaultLocale = defaultLocale;
    }

    public string DisplayName { get; }
    public string DefaultLocale { get; }
}
