namespace LogicGate.SiteFactory.Application.Configuration;

public sealed record DeploymentConfiguration
{
    public DeploymentConfiguration(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
    }

    public string Profile { get; }
}
