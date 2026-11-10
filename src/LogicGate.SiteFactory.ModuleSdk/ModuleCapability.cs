namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A declared capability in module-id.capability form; not a permission or availability decision.</summary>
public sealed record ModuleCapability
{
    public ModuleCapability(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        int separator = value.IndexOf('.', StringComparison.Ordinal);
        if (separator < 1 || !ModuleId.IsIdentifier(value[..separator]) || !ModuleId.IsIdentifier(value[(separator + 1)..]))
            throw new ArgumentException("A capability must contain a module identifier and a capability name, separated by one dot; both use 1 to 80 ASCII kebab-case characters.", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
