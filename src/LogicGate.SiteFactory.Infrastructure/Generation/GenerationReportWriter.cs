using System.Reflection;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Writes an operational report separately from business files and the composition lock.</summary>
public sealed class GenerationReportWriter
{
    public const string FileName = "generation-report.json";

    private static readonly SiteFactoryVersion RunningVersion = new(
        typeof(GenerationReportWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("The generator informational version is unavailable."));

    private readonly GeneratedFileApplier applier;

    public GenerationReportWriter() : this(new GeneratedFileApplier())
    {
    }

    internal GenerationReportWriter(GeneratedFileApplier applier) => this.applier = applier;

    public static GenerationReport CreateReport(GenerationExecutionResult execution) => new(RunningVersion, execution);

    /// <remarks>
    /// Report publication is a separate operation: its own outcome must be checked by the caller and is not
    /// recursively included in the report. Publication failure must not be mistaken for generation success.
    /// The explicit action, trusted reference and exclusive-root preconditions of GeneratedFileApplier apply.
    /// </remarks>
    public Task<FileGenerationResult> WriteAsync(
        GenerationRoot root,
        GenerationReport report,
        PlannedFileAction action,
        GeneratedContentHash? lastGenerated = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        if (report.SiteFactoryVersion != RunningVersion)
        {
            throw new ArgumentException("The report must identify the executing SiteFactory version.", nameof(report));
        }

        PlannedFile file = new(new("generation-report"), FileName, new(PlannedFileOriginKind.Generator, "sitefactory"),
            FileGenerationStrategy.Generated, action);
        GeneratedFileContent content = new(Encoding.UTF8.GetString(GenerationReportJsonSerializer.Serialize(report)));
        return applier.ApplyAsync(root, file, content, lastGenerated, cancellationToken);
    }
}
