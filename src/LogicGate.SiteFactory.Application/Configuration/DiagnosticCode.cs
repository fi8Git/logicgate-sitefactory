namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>A stable, case-sensitive machine identifier, independent of the human message.</summary>
public sealed record DiagnosticCode
{
    public DiagnosticCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
