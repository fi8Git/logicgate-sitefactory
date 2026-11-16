namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>An ordinal template ID and an exact stable major.minor.patch version; neither identifies a module or package.</summary>
public sealed record TemplateIdentity
{
    public TemplateIdentity(string id, string version)
    {
        TemplateContractValidation.RequireText(id, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (!System.Version.TryParse(version, out System.Version? parsed)
            || parsed.Build < 0 || parsed.Revision >= 0
            || !string.Equals(version, parsed.ToString(3), StringComparison.Ordinal))
            throw new ArgumentException("An exact stable major.minor.patch template version is required.", nameof(version));

        Id = id;
        Version = version;
    }

    public string Id { get; }
    public string Version { get; }
}
