namespace LogicGate.SiteFactory.Application.Configuration;

public sealed class ModulesConfiguration
{
    public ModulesConfiguration(IEnumerable<string> enabled)
    {
        ArgumentNullException.ThrowIfNull(enabled);

        string[] snapshot = [.. enabled];

        if (Array.Exists(snapshot, static module => module is null))
            throw new ArgumentException("Enabled modules cannot contain null entries.", nameof(enabled));

        Enabled = Array.AsReadOnly(snapshot);
    }

    /// <summary>A read-only snapshot retaining input order, spelling and duplicates.</summary>
    public IReadOnlyList<string> Enabled { get; }
}
