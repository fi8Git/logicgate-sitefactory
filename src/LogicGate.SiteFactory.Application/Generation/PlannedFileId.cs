namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A stable logical file identifier supplied by the caller, independent of its target path.</summary>
public sealed record PlannedFileId
{
    public PlannedFileId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
