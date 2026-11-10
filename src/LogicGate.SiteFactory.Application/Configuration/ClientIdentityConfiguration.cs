namespace LogicGate.SiteFactory.Application.Configuration;

public sealed record ClientIdentityConfiguration
{
    public ClientIdentityConfiguration(string slug, string displayName)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(displayName);

        Slug = slug;
        DisplayName = displayName;
    }

    public string Slug { get; }

    public string DisplayName { get; }
}
