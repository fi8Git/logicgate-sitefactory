using System.Reflection;
using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Writes only a lock candidate, using the generated-file policy and its explicit action/reference preconditions.</summary>
public sealed class SiteFactoryLockWriter
{
    public const string FileName = "sitefactory.lock.json";

    private static readonly SiteFactoryVersion RunningVersion = new(
        typeof(SiteFactoryLockWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("The generator informational version is unavailable."));

    private readonly GeneratedFileApplier applier;

    public SiteFactoryLockWriter() : this(new GeneratedFileApplier())
    {
    }

    internal SiteFactoryLockWriter(GeneratedFileApplier applier) => this.applier = applier;

    /// <summary>Snapshots resolved metadata with the exact executing generator build version; no environment or Git lookup.</summary>
    public static SiteFactoryLock CreateCandidate(ClientConfigurationV1 configuration, TemplateManifest resolvedTemplate) =>
        new(RunningVersion, configuration, resolvedTemplate);

    /// <remarks>
    /// A successful write does not validate a generated project (ADR-0014 §53).
    /// The caller supplies the planned action and a trusted prior hash for replacements, with exclusive application access
    /// to the root as required by GeneratedFileApplier. No ownership state, upgrade or lock comparison is implemented here.
    /// </remarks>
    public Task<FileGenerationResult> WriteAsync(
        GenerationRoot root,
        SiteFactoryLock candidate,
        PlannedFileAction action,
        GeneratedContentHash? lastGenerated = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        if (candidate.SiteFactoryVersion != RunningVersion)
        {
            throw new ArgumentException("The candidate must identify the executing SiteFactory version.", nameof(candidate));
        }

        PlannedFile file = new(new("sitefactory-lock"), FileName, new(PlannedFileOriginKind.Generator, "sitefactory"),
            FileGenerationStrategy.Generated, action);
        GeneratedFileContent content = new(Encoding.UTF8.GetString(SiteFactoryLockJsonSerializer.Serialize(candidate)));
        return applier.ApplyAsync(root, file, content, lastGenerated, cancellationToken);
    }
}
