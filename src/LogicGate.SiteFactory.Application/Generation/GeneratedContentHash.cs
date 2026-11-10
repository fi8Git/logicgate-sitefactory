namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A SHA-256 value, not proof of ownership by itself. The caller supplies the trusted generation reference.</summary>
public sealed record GeneratedContentHash
{
    public GeneratedContentHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 64 || value.Any(static character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("A 64-digit hexadecimal SHA-256 value is required.", nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
