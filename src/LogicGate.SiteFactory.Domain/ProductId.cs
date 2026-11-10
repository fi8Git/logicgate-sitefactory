namespace LogicGate.SiteFactory.Domain;

/// <summary>Identifies a product without resolving a catalogue entry.</summary>
public sealed record ProductId
{
    public ProductId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
