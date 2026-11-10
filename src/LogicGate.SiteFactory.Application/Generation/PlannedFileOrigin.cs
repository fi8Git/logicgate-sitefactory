namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A logical component reference, never a physical source path or a resolved catalogue entry.</summary>
public sealed record PlannedFileOrigin
{
    public PlannedFileOrigin(PlannedFileOriginKind kind, string componentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (componentId.Any(static character => !char.IsAsciiLetterOrDigit(character)
                && character is not '.' and not '_' and not '-')
            || componentId is "." or "..")
        {
            throw new ArgumentException("A logical component identifier is required, not a source path.", nameof(componentId));
        }

        Kind = kind;
        ComponentId = componentId;
    }

    public PlannedFileOriginKind Kind { get; }

    public string ComponentId { get; }
}
