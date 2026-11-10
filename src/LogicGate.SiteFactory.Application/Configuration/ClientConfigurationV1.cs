namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>The first vertical slice's configuration, before semantic validation.</summary>
public sealed class ClientConfigurationV1
{
    public ClientConfigurationV1(
        int schemaVersion,
        ClientIdentityConfiguration client,
        SiteConfiguration site,
        ModulesConfiguration modules,
        DeploymentConfiguration deployment,
        GitConfiguration git)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(git);

        SchemaVersion = schemaVersion;
        Client = client;
        Site = site;
        Modules = modules;
        Deployment = deployment;
        Git = git;
    }

    public int SchemaVersion { get; }

    public ClientIdentityConfiguration Client { get; }

    public SiteConfiguration Site { get; }

    public ModulesConfiguration Modules { get; }

    public DeploymentConfiguration Deployment { get; }

    public GitConfiguration Git { get; }
}
