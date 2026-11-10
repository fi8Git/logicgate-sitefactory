namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>Declared preferences only; these values do not authorize or execute Git operations.</summary>
public sealed record GitConfiguration
{
    public GitConfiguration(bool initializeLocalRepository, bool publishRemote)
    {
        InitializeLocalRepository = initializeLocalRepository;
        PublishRemote = publishRemote;
    }

    public bool InitializeLocalRepository { get; }

    public bool PublishRemote { get; }
}
