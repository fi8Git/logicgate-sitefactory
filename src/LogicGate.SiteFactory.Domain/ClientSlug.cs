namespace LogicGate.SiteFactory.Domain;

/// <summary>Identifies a client without resolving or normalizing its configuration.</summary>
public sealed record ClientSlug
{
    public ClientSlug(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
