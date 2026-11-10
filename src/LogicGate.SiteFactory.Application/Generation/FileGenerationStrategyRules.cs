namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Declarative ADR-0005 requirements, not a file-state evaluator or permission to execute an action.</summary>
public sealed record FileGenerationStrategyRules
{
    public FileGenerationStrategyRules(FileGenerationStrategy strategy)
    {
        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(strategy));
        }

        Strategy = strategy;
    }

    public FileGenerationStrategy Strategy { get; }

    /// <summary>Replacement requires proof that the file still matches the last generated content.</summary>
    public bool RequiresUnmodifiedGeneratedContent => Strategy == FileGenerationStrategy.Generated;

    /// <summary>Create the initial file only if absent; preserve an existing file without replacing it.</summary>
    public bool CreatesOnlyWhenAbsent => Strategy == FileGenerationStrategy.CreateOnce;

    /// <summary>Requires a structured, deterministic, versioned and tested format-specific handler.</summary>
    public bool RequiresStructuredMerge => Strategy == FileGenerationStrategy.Merge;

    /// <summary>The content belongs exclusively to client customizations, never to the generator.</summary>
    public bool BelongsToClientCustomizations => Strategy == FileGenerationStrategy.Custom;

    /// <summary>Forbids automatic creation, modification, merging, renaming and deletion in the normal pipeline.</summary>
    public bool ForbidsAutomaticWrites => Strategy is FileGenerationStrategy.Custom or FileGenerationStrategy.Protected;
}
