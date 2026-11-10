namespace LogicGate.SiteFactory.Application.Configuration;

public sealed record SiteConfiguration
{
    public SiteConfiguration(string product, string template, string defaultLocale, string publicBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(defaultLocale);
        ArgumentNullException.ThrowIfNull(publicBaseUrl);

        Product = product;
        Template = template;
        DefaultLocale = defaultLocale;
        PublicBaseUrl = publicBaseUrl;
    }

    public string Product { get; }

    public string Template { get; }

    public string DefaultLocale { get; }

    /// <summary>The supplied URL text; parsing and validation belong to a later step.</summary>
    public string PublicBaseUrl { get; }
}
