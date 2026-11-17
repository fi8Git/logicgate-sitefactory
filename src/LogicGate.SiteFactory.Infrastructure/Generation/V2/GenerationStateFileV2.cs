using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation.V2;

/// <summary>Control metadata only, with no content, provider execution or destination access.</summary>
internal sealed record GenerationStateFileV2
{
    private GenerationStateFileV2(PlannedFileId id, string path, FileGenerationStrategy strategy,
        GenerationContentSourceV2 source, GeneratedContentHash? hash)
    {
        ArgumentNullException.ThrowIfNull(id);
        GenerationV2Json.RequireText(id.Value, GenerationV2Json.MaximumStateLength);
        GenerationV2Json.RequireText(path, GenerationV2Json.MaximumStateLength);
        // Reuse the existing lexical guard, without creating an executable plan or resolving a physical root.
        _ = new PlannedFile(id, path, new(PlannedFileOriginKind.Template, "sdk"), strategy, PlannedFileAction.Create);
        Id = id;
        Path = path;
        Strategy = strategy;
        Source = source;
        Hash = hash;
    }

    internal static GenerationStateFileV2 Sdk(PlannedFileId id, string path, FileGenerationStrategy strategy,
        string contentKey, GeneratedContentHash? hash)
    {
        if (strategy is not (FileGenerationStrategy.Generated or FileGenerationStrategy.CreateOnce))
            throw new ArgumentException("V2 SDK controls support generated and create-once files only.");
        if ((strategy == FileGenerationStrategy.Generated) != (hash is not null))
            throw new ArgumentException("Only generated SDK files require an ownership hash.");
        return new(id, path, strategy, GenerationContentSourceV2.Sdk(contentKey), hash);
    }

    internal static GenerationStateFileV2 Lock(SiteFactoryLockV2 composition) =>
        new(new("sitefactory-lock"), SiteFactoryLockWriter.FileName, FileGenerationStrategy.Generated,
            GenerationContentSourceV2.Composition, GenerationV2Json.ComputeHash(composition.Serialize()));

    internal PlannedFileId Id { get; }
    internal string Path { get; }
    internal FileGenerationStrategy Strategy { get; }
    internal GenerationContentSourceV2 Source { get; }
    internal GeneratedContentHash? Hash { get; }
    internal string? ContentProfile => Hash is null ? null : Source.Kind == "sdk" ? "exact-bytes" : "utf8-lf";

    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("id", Id.Value);
        writer.WriteString("path", Path);
        writer.WriteString("strategy", Strategy == FileGenerationStrategy.Generated ? "generated" : "create-once");
        writer.WriteStartObject("source");
        writer.WriteString("kind", Source.Kind);
        writer.WriteString(Source.Kind == "sdk" ? "contentKey" : "source", Source.Value);
        writer.WriteEndObject();
        writer.WriteString("contentProfile", ContentProfile);
        writer.WriteString("lastGeneratedSha256", Hash?.Value);
        writer.WriteEndObject();
    }
}
