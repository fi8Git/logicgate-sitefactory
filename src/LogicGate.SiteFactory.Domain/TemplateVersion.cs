namespace LogicGate.SiteFactory.Domain;

/// <summary>An exact stable template version in canonical major.minor.patch form; no version resolution.</summary>
public sealed record TemplateVersion
{
    public TemplateVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Version.TryParse(value, out Version? version)
            || version.Build < 0 || version.Revision >= 0
            || !string.Equals(value, version.ToString(3), StringComparison.Ordinal))
        {
            throw new ArgumentException("An exact stable major.minor.patch version is required.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
