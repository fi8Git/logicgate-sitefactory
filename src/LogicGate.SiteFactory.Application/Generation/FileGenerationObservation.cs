namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>
/// Logical metadata and observations captured by an applicator, never file content or a physical root.
/// CurrentHash describes the initial inspection, not the state after promotion. Null means unobserved/unavailable;
/// DestinationExistedAtInspection distinguishes a known absence from an uninspected destination.
/// </summary>
public sealed record FileGenerationObservation
{
    public FileGenerationObservation(
        PlannedFile file,
        bool? destinationExistedAtInspection = null,
        GeneratedContentHash? previousHash = null,
        GeneratedContentHash? currentHash = null,
        GeneratedContentHash? proposedHash = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (currentHash is not null && destinationExistedAtInspection != true)
        {
            throw new ArgumentException("A current hash requires an observed existing destination.", nameof(currentHash));
        }

        File = file;
        DestinationExistedAtInspection = destinationExistedAtInspection;
        PreviousHash = previousHash;
        CurrentHash = currentHash;
        ProposedHash = proposedHash;
    }

    public PlannedFile File { get; }

    public bool? DestinationExistedAtInspection { get; }

    /// <summary>Caller-supplied trusted last-generated reference, not inferred from the destination.</summary>
    public GeneratedContentHash? PreviousHash { get; }

    public GeneratedContentHash? CurrentHash { get; }

    public GeneratedContentHash? ProposedHash { get; }
}
