using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>
/// Exact-composition ownership state, not an upgrade format or an adoption mechanism.
/// Previous content hashes prove ownership independently of the new candidate contents.
/// </summary>
internal sealed class GenerationState
{
    internal const string FileName = ".sitefactory/generation-state.json";
    internal const string FileId = "generation-state";
    internal const int MaximumLength = 256 * 1024;

    private readonly GenerationExecutionRequest request;
    private readonly SiteFactoryLock composition;
    private readonly string configurationHash;

    private GenerationState(GenerationExecutionRequest request, SiteFactoryLock composition,
        string configurationHash, IReadOnlyList<Entry> files)
    {
        this.request = request;
        this.composition = composition;
        this.configurationHash = configurationHash;
        Files = files;
    }

    internal IReadOnlyList<Entry> Files { get; }

    internal static GenerationState Create(GenerationExecutionRequest request, ClientConfigurationV1 configuration,
        TemplateManifest template, IReadOnlyList<(PlannedFile File, string? Content)> files, SiteFactoryLock composition)
    {
        List<Entry> entries = files.Select(item => new Entry(item.File,
            template.Files.Single(file => file.Id == item.File.Id).Source, template.TemplateVersion.Value,
            item.File.Strategy == FileGenerationStrategy.Generated ? new GeneratedFileContent(item.Content!).Hash : null)).ToList();
        entries.Add(new(new(new("sitefactory-lock"), SiteFactoryLockWriter.FileName,
            new(PlannedFileOriginKind.Generator, "sitefactory"), FileGenerationStrategy.Generated, PlannedFileAction.Create),
            "composition", composition.SiteFactoryVersion.Value, Hash(SiteFactoryLockJsonSerializer.Serialize(composition))));
        return new(request, composition, ConfigurationHash(configuration), entries.AsReadOnly());
    }

    internal byte[] Serialize(GeneratedContentHash reportHash) => WriteJson(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("generatorVersion", composition.SiteFactoryVersion.Value);
        writer.WriteString("configurationSha256", configurationHash);
        writer.WriteString("planId", request.Plan.Id.Value);
        writer.WriteString("client", request.Plan.ClientSlug.Value);
        writer.WriteString("product", composition.ProductId.Value);
        writer.WriteStartObject("template");
        writer.WriteString("id", composition.TemplateId.Value);
        writer.WriteString("version", composition.TemplateVersion.Value);
        writer.WriteEndObject();
        writer.WriteStartArray("files");
        foreach (Entry entry in Files)
        {
            writer.WriteStartObject();
            writer.WriteString("id", entry.File.Id.Value);
            writer.WriteString("path", entry.File.RelativePath);
            writer.WriteString("strategy", entry.File.Strategy switch
            {
                FileGenerationStrategy.Generated => "generated",
                FileGenerationStrategy.CreateOnce => "create-once",
                FileGenerationStrategy.Custom => "custom",
                FileGenerationStrategy.Protected => "protected",
                _ => throw new InvalidOperationException("Unsupported generation state strategy."),
            });
            writer.WriteString("sourceComponent", entry.File.Origin.ComponentId);
            writer.WriteString("source", entry.Source);
            writer.WriteString("sourceVersion", entry.SourceVersion);
            writer.WriteString("lastGeneratedSha256", entry.Hash?.Value);
            writer.WriteString("encoding", entry.Hash is null ? null : "utf-8");
            writer.WriteString("lineEndings", entry.Hash is null ? null : "lf");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("reportSha256", reportHash.Value);
        writer.WriteEndObject();
    });

    // Only previous content hashes may differ from the candidate. All executable intentions
    // (including paths) come from the trusted plan, never from the control document.
    // Canonical byte comparison rejects unknown/duplicate fields, reordering and structural changes.
    internal Reference? ReadMatchingState(byte[] bytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            GeneratedContentHash? reportHash;
            List<Entry> previous = [];
            try
            {
                // Parsing JSON does not materialize names/strings: invalid Unicode may fail only here.
                // Element kinds are checked and their document is owned by this scope.
                reportHash = ReadHash(document.RootElement, "reportSha256");
                if (reportHash is null
                    || !document.RootElement.TryGetProperty("files", out JsonElement files)
                    || files.ValueKind != JsonValueKind.Array || files.GetArrayLength() != Files.Count) return null;
                for (int index = 0; index < Files.Count; index++)
                {
                    Entry expected = Files[index];
                    // The final lock is structural: its hash must still equal the candidate lock.
                    if (expected.File.Strategy == FileGenerationStrategy.Generated && index < Files.Count - 1)
                    {
                        GeneratedContentHash? hash = ReadHash(files[index], "lastGeneratedSha256");
                        if (hash is null) return null;
                        previous.Add(expected with { Hash = hash });
                    }
                    else previous.Add(expected);
                }
            }
            catch (InvalidOperationException exception) when (exception is not ObjectDisposedException)
            {
                return null;
            }

            GenerationState prior = new(request, composition, configurationHash, previous.AsReadOnly());
            // Internal canonical serialization errors must propagate, not become invalid user state.
            return bytes.AsSpan().SequenceEqual(prior.Serialize(reportHash)) ? new(prior, reportHash) : null;
        }
    }

    private static GeneratedContentHash? ReadHash(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String) return null;
        string? hash = value.GetString();
        return hash is { Length: 64 } && hash.All(static character => char.IsAsciiHexDigit(character)) ? new(hash) : null;
    }

    internal sealed record Reference(GenerationState State, GeneratedContentHash ReportHash);

    internal Task<FileGenerationResult> WriteAsync(GeneratedFileApplier applier, GenerationRoot root,
        GeneratedContentHash reportHash, GeneratedContentHash? previousHash, CancellationToken cancellationToken)
    {
        PlannedFile file = new(new(FileId), FileName, new(PlannedFileOriginKind.Generator, "sitefactory"),
            FileGenerationStrategy.Generated, previousHash is null ? PlannedFileAction.Create : PlannedFileAction.Update);
        return applier.ApplyAsync(root, file, new(Encoding.UTF8.GetString(Serialize(reportHash))), previousHash, cancellationToken);
    }

    internal static GeneratedContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(SHA256.HashData(bytes)));

    private static string ConfigurationHash(ClientConfigurationV1 configuration) => Hash(WriteJson(writer =>
    {
        // Fixed, typed field order; no culture, machine paths, reflection dump or plaintext identity in state.
        writer.WriteStartArray();
        writer.WriteNumberValue(configuration.SchemaVersion);
        writer.WriteStringValue(configuration.Client.Slug);
        writer.WriteStringValue(configuration.Client.DisplayName);
        writer.WriteStringValue(configuration.Site.Product);
        writer.WriteStringValue(configuration.Site.Template);
        writer.WriteStringValue(configuration.Site.DefaultLocale);
        writer.WriteStringValue(configuration.Site.PublicBaseUrl);
        writer.WriteStartArray();
        foreach (string module in configuration.Modules.Enabled) writer.WriteStringValue(module);
        writer.WriteEndArray();
        writer.WriteStringValue(configuration.Deployment.Profile);
        writer.WriteBooleanValue(configuration.Git.InitializeLocalRepository);
        writer.WriteBooleanValue(configuration.Git.PublishRemote);
        writer.WriteEndArray();
    })).Value;

    private static byte[] WriteJson(Action<Utf8JsonWriter> write)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
        {
            Indented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
        }))
        {
            write(writer);
            writer.Flush();
        }
        buffer.GetSpan(1)[0] = (byte)'\n';
        buffer.Advance(1);
        return buffer.WrittenSpan.ToArray();
    }

    internal sealed record Entry(PlannedFile File, string Source, string SourceVersion, GeneratedContentHash? Hash);
}
