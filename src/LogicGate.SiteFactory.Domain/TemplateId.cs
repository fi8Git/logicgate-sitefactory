namespace LogicGate.SiteFactory.Domain;

/// <summary>Identifies a template independently of its physical representation.</summary>
public sealed record TemplateId
{
    public TemplateId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
