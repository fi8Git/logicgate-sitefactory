using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Read-only, no-follow preflight of a recognized, exact-composition output. Never adopts existing files.</summary>
internal sealed record RegenerationInspection(GeneratedContentHash StateHash, GeneratedContentHash ReportHash, GenerationState PreviousState)
{
    internal static async Task<(RegenerationInspection? Inspection, IReadOnlyList<GenerationPlanDiagnostic> Diagnostics)> InspectAsync(
        GenerationRoot root, GenerationState expected, CancellationToken cancellationToken)
    {
        byte[]? bytes;
        try
        {
            using AtomicWriteSession? session = AtomicWriteSession.OpenExisting(root, GenerationState.FileName, cancellationToken);
            bytes = session is null ? null
                : await session.ReadControlDocumentAsync(GenerationState.MaximumLength, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, [Diagnostic("SF-GEN-STATE-001", GenerationState.FileName, "The generation state cannot be read safely; no files were changed.")]);
        }

        if (bytes is null)
            return (null, [Diagnostic("SF-GEN-INITIAL-003", "output", "An existing output requires matching generation state; no files were changed.")]);
        GenerationState.Reference? reference = expected.ReadMatchingState(bytes);
        if (reference is null)
            return (null, [Diagnostic("SF-GEN-STATE-001", GenerationState.FileName,
                "The generation state is invalid or does not match this configuration and composition; no files were changed.")]);

        List<GenerationPlanDiagnostic> diagnostics = [];
        foreach (GenerationState.Entry entry in reference.State.Files)
        {
            // Protected paths are never opened. Client content is not read/hashed or treated as owned.
            if (entry.File.Strategy == FileGenerationStrategy.Protected) continue;
            // This bounded regeneration does not repair incomplete outputs or recreate renamed/deleted files.
            // In particular it must not create a portable-case duplicate of a client-renamed path on Linux.
            await CheckAsync(entry.File.RelativePath, entry.Hash,
                required: entry.File.Strategy is FileGenerationStrategy.Generated or FileGenerationStrategy.CreateOnce).ConfigureAwait(false);
        }
        await CheckAsync(GenerationReportWriter.FileName, reference.ReportHash, required: true).ConfigureAwait(false);
        return (diagnostics.Count == 0 ? new(GenerationState.Hash(bytes), reference.ReportHash, reference.State) : null, diagnostics.AsReadOnly());

        async Task CheckAsync(string path, GeneratedContentHash? reference, bool required)
        {
            try
            {
                using AtomicWriteSession? session = AtomicWriteSession.OpenExisting(root, path, cancellationToken);
                session?.VerifyDirectories();
                if (reference is null)
                {
                    if (!(session?.DestinationExists() ?? false) && required)
                        diagnostics.Add(Diagnostic("SF-GEN-FILE-014", path,
                            "A declared client file is missing; incomplete outputs are not repaired automatically."));
                    return;
                }
                using AtomicFileSnapshot? snapshot = session is null ? null
                    : await session.ReadDestinationAsync(cancellationToken).ConfigureAwait(false);
                if ((snapshot is null && required) || (snapshot is not null && snapshot.Hash != reference))
                    diagnostics.Add(Diagnostic("SF-GEN-FILE-004", path,
                        "A managed file is missing or differs from its last generated content; no files were changed."));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(Diagnostic("SF-GEN-FILE-015", path,
                    "A planned destination cannot be inspected safely; no files were changed."));
            }
        }
    }

    private static GenerationPlanDiagnostic Diagnostic(string code, string path, string message) =>
        new(new(code), Application.Configuration.DiagnosticSeverity.Error, message, path);
}
