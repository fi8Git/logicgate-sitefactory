using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Infrastructure.Generation.V2;

/// <summary>Memory-only exact-composition V2 candidate. A matching reference imports only prior SDK content/report hashes.</summary>
internal sealed class GenerationStateV2
{
    internal const int MaximumFiles = 256; // Includes the final generator-owned lock entry.
    private readonly SiteFactoryLockV2 composition;
    private readonly GeneratedContentHash configurationHash;
    private readonly GenerationPlanId planId;
    private readonly ClientSlug client;
    private static readonly string[] Properties = ["schemaVersion", "generatorVersion", "configurationSha256", "planId",
        "client", "lockSchemaVersion", "files", "reportSha256"];

    internal GenerationStateV2(SiteFactoryLockV2 composition, GeneratedContentHash configurationHash,
        GenerationPlanId planId, ClientSlug client, IEnumerable<GenerationStateFileV2> files)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(configurationHash);
        ArgumentNullException.ThrowIfNull(planId);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(files);
        GenerationV2Json.RequireText(planId.Value, GenerationV2Json.MaximumStateLength);
        GenerationStateFileV2[] snapshot = files.Take(MaximumFiles).ToArray();
        if (snapshot.Length is 0 or >= MaximumFiles || snapshot.Any(static file => file is null || file.Source.Kind != "sdk"))
            throw new ArgumentException("V2 requires bounded SDK declarations and reserves one lock entry.", nameof(files));
        if (snapshot.Select(static file => file.Id).Distinct().Count() != snapshot.Length
            || snapshot.Any(static file => file.Id.Value is "sitefactory-lock" or "generation-report" or "generation-state"
                || file.Path.Split('/')[0].Equals(".sitefactory", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("V2 file identities must be unique and cannot claim control artifacts.", nameof(files));

        PlannedFile[] targets = snapshot.Select(static file => new PlannedFile(file.Id, file.Path,
            new(PlannedFileOriginKind.Template, "sdk"), file.Strategy, PlannedFileAction.Create)).Concat([
                new(new("sitefactory-lock"), SiteFactoryLockWriter.FileName, new(PlannedFileOriginKind.Generator, "sitefactory"), FileGenerationStrategy.Generated, PlannedFileAction.Create),
                new(new("generation-report"), GenerationReportWriter.FileName, new(PlannedFileOriginKind.Generator, "sitefactory"), FileGenerationStrategy.Generated, PlannedFileAction.Create),
                new(new("generation-state"), GenerationState.FileName, new(PlannedFileOriginKind.Generator, "sitefactory"), FileGenerationStrategy.Generated, PlannedFileAction.Create),
            ]).ToArray();
        if (GenerationPlanPathValidator.Validate(targets).Count != 0)
            throw new ArgumentException("V2 file paths must be portable, distinct and outside reserved controls.", nameof(files));

        this.composition = composition;
        this.configurationHash = configurationHash;
        this.planId = planId;
        this.client = client;
        Files = Array.AsReadOnly(snapshot.OrderBy(static file => file.Path, StringComparer.Ordinal)
            .ThenBy(static file => file.Id.Value, StringComparer.Ordinal).Append(GenerationStateFileV2.Lock(composition)).ToArray());
    }

    internal IReadOnlyList<GenerationStateFileV2> Files { get; }

    internal byte[] Serialize(GeneratedContentHash reportHash)
    {
        ArgumentNullException.ThrowIfNull(reportHash);
        return GenerationV2Json.Write(GenerationV2Json.MaximumStateLength, writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 2);
            writer.WriteString("generatorVersion", composition.GeneratorVersion.Value);
            writer.WriteString("configurationSha256", configurationHash.Value);
            writer.WriteString("planId", planId.Value);
            writer.WriteString("client", client.Value);
            writer.WriteNumber("lockSchemaVersion", 2);
            writer.WriteStartArray("files");
            foreach (GenerationStateFileV2 file in Files) file.Write(writer);
            writer.WriteEndArray();
            writer.WriteString("reportSha256", reportHash.Value);
            writer.WriteEndObject();
        });
    }

    /// <summary>A reference requires both canonical documents and the exact expected host binding, entirely in memory.</summary>
    internal Reference? ReadMatchingState(byte[] bytes, byte[] lockBytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (composition.ReadMatching(lockBytes) is null) return null;
        using JsonDocument? document = GenerationV2Json.Parse(bytes, GenerationV2Json.MaximumStateLength);
        if (document is null) return null;
        List<GenerationStateFileV2> previous = [];
        GeneratedContentHash reportHash;
        try
        {
            JsonElement root = document.RootElement;
            GenerationV2Json.RequireProperties(root, Properties);
            GenerationV2Json.RequireVersion(root, "schemaVersion", 2);
            GenerationV2Json.RequireVersion(root, "lockSchemaVersion", 2);
            reportHash = GenerationV2Json.Hash(root, "reportSha256");
            JsonElement files = root.GetProperty("files");
            if (files.ValueKind != JsonValueKind.Array || files.GetArrayLength() != Files.Count) return null;
            for (int index = 0; index < Files.Count - 1; index++)
            {
                GenerationStateFileV2 expected = Files[index];
                GeneratedContentHash? hash = expected.Strategy == FileGenerationStrategy.Generated
                    ? GenerationV2Json.Hash(files[index], "lastGeneratedSha256") : null;
                previous.Add(GenerationStateFileV2.Sdk(expected.Id, expected.Path, expected.Strategy, expected.Source.Value, hash));
            }
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return null;
        }

        GenerationStateV2 prior = new(composition, configurationHash, planId, client, previous);
        // Expected structure and the lock digest are never imported from the incoming document.
        // Errors in trusted candidate construction/serialization remain programming errors.
        return bytes.AsSpan().SequenceEqual(prior.Serialize(reportHash)) ? new(prior, reportHash) : null;
    }

    internal sealed record Reference(GenerationStateV2 State, GeneratedContentHash ReportHash);
}
