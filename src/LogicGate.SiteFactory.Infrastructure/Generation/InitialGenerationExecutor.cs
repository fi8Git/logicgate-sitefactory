using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>
/// Applies one prepared minimal plan to an absent root or a recognized exact-composition output.
/// No adoption, upgrade or project certification. The historic type name is retained for compatibility.
/// The caller must have exclusive application access to the root throughout preparation and execution.
/// Atomicity is per file, not a transaction over the entire directory.
/// </summary>
public sealed class InitialGenerationExecutor : IGenerationExecutor
{
    private readonly GenerationExecutionRequest preparedRequest;
    private readonly GenerationRoot root;
    private readonly IReadOnlyList<(PlannedFile File, string? Content)> files;
    private readonly SiteFactoryLock candidate;
    private readonly AtomicFileWriter writer;
    private readonly GenerationState state;
    private readonly RegenerationInspection? regeneration;
    private int executed;

    private InitialGenerationExecutor(GenerationExecutionRequest request, GenerationRoot root,
        IReadOnlyList<(PlannedFile File, string? Content)> files, SiteFactoryLock candidate, AtomicFileWriter writer,
        GenerationState state, RegenerationInspection? regeneration)
    {
        preparedRequest = request;
        this.root = root;
        this.files = files;
        this.candidate = candidate;
        this.writer = writer;
        this.state = state;
        this.regeneration = regeneration;
    }

    public static Task<InitialGenerationPreparation> PrepareAsync(
        GenerationExecutionRequest request, ClientConfigurationV1 configuration, TemplateManifest template,
        string outputPath, ITemplateContentProvider contentProvider, CancellationToken cancellationToken = default) =>
        PrepareAsync(request, configuration, template, outputPath, contentProvider, new AtomicFileWriter(), cancellationToken);

    internal static async Task<InitialGenerationPreparation> PrepareAsync(
        GenerationExecutionRequest request, ClientConfigurationV1 configuration, TemplateManifest template,
        string outputPath, ITemplateContentProvider contentProvider, AtomicFileWriter writer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(contentProvider);
        cancellationToken.ThrowIfCancellationRequested();

        GenerationPlan plan = request.Plan;
        if (!ClientConfigurationV1Validator.Validate(configuration).IsValid
            || plan.ClientSlug.Value != configuration.Client.Slug
            || plan.ProductId.Value != configuration.Site.Product
            || plan.TemplateId.Value != configuration.Site.Template
            || request.TargetRoot != plan.ClientSlug.Value
            || template.TemplateId != plan.TemplateId
            || !template.SupportedProductIds.Contains(plan.ProductId)
            || !MatchesManifest(plan, template))
        {
            return Reject(InitialGenerationPreparationStatus.InvalidPlan, "SF-GEN-FILE-014",
                "The initial plan must match the validated configuration, resolved manifest and logical client root.");
        }

        if (configuration.Modules.Enabled.Count != 0)
            return Reject(InitialGenerationPreparationStatus.InvalidPlan, "SF-GEN-INITIAL-001",
                "Initial generation does not yet support module resolution.", "modules.enabled");

        // Reserve all publication paths before opening any destination, including ancestor/case collisions.
        GenerationPlanDiagnostic[] collisions = GenerationPlanPathValidator.Validate(plan.Files.Concat([
            new(new("sitefactory-lock"), SiteFactoryLockWriter.FileName, new(PlannedFileOriginKind.Generator, "sitefactory"),
                FileGenerationStrategy.Generated, PlannedFileAction.Create),
            new(new("generation-report"), GenerationReportWriter.FileName, new(PlannedFileOriginKind.Generator, "sitefactory"),
                FileGenerationStrategy.Generated, PlannedFileAction.Create),
            new(new(GenerationState.FileId), GenerationState.FileName, new(PlannedFileOriginKind.Generator, "sitefactory"),
                FileGenerationStrategy.Generated, PlannedFileAction.Create),
        ])).ToArray();
        if (collisions.Length != 0)
            return new(InitialGenerationPreparationStatus.InvalidPlan, collisions);

        if (plan.Files.Any(static file => file.Id.Value is "sitefactory-lock" or "generation-report" or GenerationState.FileId
            || file.RelativePath.Split('/')[0].Equals(".sitefactory", StringComparison.OrdinalIgnoreCase)))
            return Reject(InitialGenerationPreparationStatus.InvalidPlan, "SF-GEN-FILE-003",
                "Template file identities must not collide with generator-owned artifacts.");

        List<(PlannedFile File, string? Content)> prepared = [];
        foreach (PlannedFile file in plan.Files)
        {
            TemplateFile declaration = template.Files.Single(entry => entry.Id == file.Id);
            string? content = null;
            if (file.Action == PlannedFileAction.Create)
            {
                content = await contentProvider.ReadAsync(template, configuration, declaration.Source, cancellationToken).ConfigureAwait(false);
                if (content is null)
                    return Reject(InitialGenerationPreparationStatus.InvalidTemplate, "SF-GEN-FILE-005",
                        "No content is available for the exact resolved template version and logical source.");

                // Validate encoding for every candidate before the first write, not lazily per file.
                _ = new GeneratedFileContent(content);
            }
            prepared.Add((file, content));
        }

        SiteFactoryLock candidate = SiteFactoryLockWriter.CreateCandidate(configuration, template);
        _ = SiteFactoryLockJsonSerializer.Serialize(candidate);
        GenerationState state = GenerationState.Create(request, configuration, template, prepared, candidate);
        RegenerationInspection? regeneration = null;

        GenerationRoot root;
        try
        {
            // Permit the documented ./ prefix, but do not erase '..' or other unsafe segments via GetFullPath.
            string path = outputPath.StartsWith("./", StringComparison.Ordinal)
                || (OperatingSystem.IsWindows() && outputPath.StartsWith(".\\", StringComparison.Ordinal))
                ? outputPath[2..] : outputPath;
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An output root is required.");
            root = new(Path.IsPathFullyQualified(path) ? path : Path.Combine(Environment.CurrentDirectory, path));
            List<GenerationPlanDiagnostic> destinationDiagnostics = [];
            foreach ((string logicalPath, string propertyPath) in plan.Files.Select(static (file, index) =>
                (file.RelativePath, FormattableString.Invariant($"plannedFiles[{index}].relativePath")))
                .Concat([(SiteFactoryLockWriter.FileName, SiteFactoryLockWriter.FileName),
                    (GenerationReportWriter.FileName, GenerationReportWriter.FileName), (GenerationState.FileName, GenerationState.FileName)]))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    root.Resolve(logicalPath);
                }
                catch (ArgumentException)
                {
                    destinationDiagnostics.Add(new(new("SF-GEN-FILE-001"), DiagnosticSeverity.Error,
                        "The destination is not a supported portable physical path.", propertyPath));
                }
            }
            if (destinationDiagnostics.Count != 0)
                return new(InitialGenerationPreparationStatus.InvalidRoot, destinationDiagnostics);

            if (RootExists(root, cancellationToken))
            {
                (RegenerationInspection? inspection, IReadOnlyList<GenerationPlanDiagnostic> diagnostics) =
                    await RegenerationInspection.InspectAsync(root, state, cancellationToken).ConfigureAwait(false);
                if (inspection is null) return new(InitialGenerationPreparationStatus.Conflict, diagnostics);
                regeneration = inspection;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return Reject(InitialGenerationPreparationStatus.InvalidRoot, "SF-GEN-FILE-001",
                "The output root is invalid, inaccessible or cannot be inspected without following a link.", "output");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new(new InitialGenerationExecutor(request, root, prepared.AsReadOnly(), candidate, writer, state, regeneration));
    }

    public async Task<GenerationExecutionResult> ExecuteAsync(GenerationExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        // Apply exactly the immutable request prepared above, never silently rebuild or substitute a plan.
        if (!ReferenceEquals(request, preparedRequest))
            throw new ArgumentException("The executor is bound to its prepared request.", nameof(request));
        if (Interlocked.Exchange(ref executed, 1) != 0) return ChangedBeforeApplication();

        try
        {
            if (regeneration is null)
            {
                if (RootExists(root, cancellationToken)) return ChangedBeforeApplication();
            }
            else
            {
                // Recheck the entire preflight, including late conflicts, before any creation or publication.
                (RegenerationInspection? current, _) = await RegenerationInspection.InspectAsync(root, state, cancellationToken).ConfigureAwait(false);
                if (current is null || current.StateHash != regeneration.StateHash) return ChangedBeforeApplication();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(request.Plan.Id, [], [new(new("SF-GEN-FILE-015"), "The output root could not be inspected safely.")]);
        }

        GeneratedFileApplier generated = new(writer);
        ClientFileApplier client = new(writer);
        List<FileGenerationResult> results = [];
        foreach ((PlannedFile file, string? content) in files)
        {
            // Only the completed exact-composition preflight authorizes a content update.
            // The prepared candidate remains immutable; never rebuild it from a changed source here.
            PlannedFile application = regeneration is not null && file.Strategy == FileGenerationStrategy.Generated
                ? new(file.Id, file.RelativePath, file.Origin, file.Strategy, PlannedFileAction.Update) : file;
            FileGenerationResult result = file.Strategy == FileGenerationStrategy.Generated
                ? await generated.ApplyAsync(root, application, new(content!),
                    regeneration?.PreviousState.Files.Single(entry => entry.File.Id == file.Id).Hash, cancellationToken).ConfigureAwait(false)
                : await client.ApplyAsync(root, file, content, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (result.Status is FileGenerationStatus.Failed or FileGenerationStatus.Conflict)
                break;
        }

        if (results.All(static result => result.Status is not (FileGenerationStatus.Failed or FileGenerationStatus.Conflict)))
            results.Add(await new SiteFactoryLockWriter(generated).WriteAsync(root, candidate, PlannedFileAction.Create,
                regeneration is null ? null : state.Files[^1].Hash, cancellationToken).ConfigureAwait(false));

        // The report describes only completed/attempted operations, not files skipped after failure.
        GenerationExecutionResult execution = new(request.Plan.Id, results, []);
        GenerationReport report = GenerationReportWriter.CreateReport(execution);
        FileGenerationResult publication = await new GenerationReportWriter(generated).WriteAsync(root, report,
            regeneration is null ? PlannedFileAction.Create : PlannedFileAction.Update,
            regeneration?.ReportHash, cancellationToken).ConfigureAwait(false);
        results.Add(publication);
        // Report and control-state publication are separate outcomes in the CLI result. Keeping them out
        // of the persisted report avoids a report->state->report hash cycle. Never advance state on failure.
        if (execution.Status == GenerationExecutionStatus.Succeeded
            && publication.Status is FileGenerationStatus.Created or FileGenerationStatus.Updated or FileGenerationStatus.Unchanged)
        {
            results.Add(await state.WriteAsync(generated, root,
                GenerationState.Hash(GenerationReportJsonSerializer.Serialize(report)), regeneration?.StateHash, cancellationToken).ConfigureAwait(false));
        }
        return new(request.Plan.Id, results, []);
    }

    private GenerationExecutionResult ChangedBeforeApplication()
    {
        PlannedFile first = files[0].File;
        return new(preparedRequest.Plan.Id, [new(first.Id, FileGenerationStatus.Conflict,
            new(new("SF-GEN-FILE-013"), "The output changed or this prepared execution was already used; no files were changed."), new(first))], []);
    }

    private static bool RootExists(GenerationRoot root, CancellationToken cancellationToken)
    {
        // A root-level probe checks all existing ancestors with the writer's native no-follow protections.
        // No directory is created; null means an ancestor (including the root) is absent, not an access failure.
        using AtomicWriteSession? session = AtomicWriteSession.OpenExisting(root, SiteFactoryLockWriter.FileName, cancellationToken);
        return session is not null;
    }

    private static bool MatchesManifest(GenerationPlan plan, TemplateManifest template) =>
        plan.Files.Count != 0 && plan.Files.Count == template.Files.Count
        && template.SupportedProductIds.Distinct().Count() == template.SupportedProductIds.Count
        && template.Files.Select(static entry => entry.Id).Distinct().Count() == template.Files.Count
        && plan.Files.All(file => template.Files.Any(entry => entry.Id == file.Id
            && entry.RelativePath == file.RelativePath && entry.Strategy == file.Strategy)
            && file.Origin == new PlannedFileOrigin(PlannedFileOriginKind.Template, template.TemplateId.Value)
            && (file.Strategy switch
            {
                FileGenerationStrategy.Generated or FileGenerationStrategy.CreateOnce => file.Action == PlannedFileAction.Create,
                FileGenerationStrategy.Custom or FileGenerationStrategy.Protected => file.Action == PlannedFileAction.Preserve,
                _ => false,
            }));

    private static InitialGenerationPreparation Reject(InitialGenerationPreparationStatus status,
        string code, string message, string? path = null) =>
        new(status, [new(new(code), DiagnosticSeverity.Error, message, path)]);
}
