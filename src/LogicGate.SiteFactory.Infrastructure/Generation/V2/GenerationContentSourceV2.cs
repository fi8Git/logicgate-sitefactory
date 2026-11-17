namespace LogicGate.SiteFactory.Infrastructure.Generation.V2;

/// <summary>Closed source union. SDK keys are opaque metadata, never historical or physical paths.</summary>
internal sealed record GenerationContentSourceV2
{
    private GenerationContentSourceV2(string kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    internal static GenerationContentSourceV2 Sdk(string contentKey)
    {
        GenerationV2Json.RequireText(contentKey, GenerationV2Json.MaximumStateLength);
        return new("sdk", contentKey);
    }

    internal static GenerationContentSourceV2 Composition { get; } = new("generator", "composition");
    internal string Kind { get; }
    internal string Value { get; }
}
