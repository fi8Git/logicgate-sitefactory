namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A canonical module identifier, matching the client configuration's ASCII kebab-case convention.</summary>
public sealed record ModuleId
{
    public ModuleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!IsIdentifier(value))
            throw new ArgumentException("A module identifier must be 1 to 80 ASCII lowercase letters, digits or single separating hyphens.", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    internal static bool IsIdentifier(string value)
    {
        if (value.Length is < 1 or > 80) return false;

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9') continue;
            if (character != '-' || index == 0 || index == value.Length - 1 || value[index - 1] == '-') return false;
        }

        return true;
    }
}
