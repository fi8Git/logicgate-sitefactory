using System.Text.Json;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Infrastructure.Generation.V2;

/// <summary>One explicit SDK provider/template binding. No module activation, package resolution or V1 conversion.</summary>
internal sealed record SiteFactoryLockV2
{
    private static readonly string[] Properties = ["schemaVersion", "generationStateSchemaVersion", "siteFactoryVersion",
        "configurationSchemaVersion", "product", "template", "provider"];
    private static readonly string[] TemplateProperties = ["id", "version"];
    private static readonly string[] ProviderProperties = ["moduleId", "moduleVersion", "type", "artifact"];
    private static readonly string[] ArtifactProperties = ["kind", "sha256"];

    internal SiteFactoryLockV2(SiteFactoryVersion generatorVersion, ProductId productId,
        TemplateId templateId, TemplateVersion templateVersion, TemplateProviderIdentity provider)
    {
        ArgumentNullException.ThrowIfNull(generatorVersion);
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentNullException.ThrowIfNull(templateVersion);
        ArgumentNullException.ThrowIfNull(provider);
        GenerationV2Json.RequireText(generatorVersion.Value, GenerationV2Json.MaximumLockLength);
        GenerationV2Json.RequireText(productId.Value, GenerationV2Json.MaximumLockLength);
        GenerationV2Json.RequireText(templateId.Value, GenerationV2Json.MaximumLockLength);
        GeneratorVersion = generatorVersion;
        ProductId = productId;
        TemplateId = templateId;
        TemplateVersion = templateVersion;
        Provider = provider;
    }

    internal SiteFactoryVersion GeneratorVersion { get; }
    internal ProductId ProductId { get; }
    internal TemplateId TemplateId { get; }
    internal TemplateVersion TemplateVersion { get; }
    internal TemplateProviderIdentity Provider { get; }

    internal byte[] Serialize() => GenerationV2Json.Write(GenerationV2Json.MaximumLockLength, writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 2);
        writer.WriteNumber("generationStateSchemaVersion", 2);
        writer.WriteString("siteFactoryVersion", GeneratorVersion.Value);
        writer.WriteNumber("configurationSchemaVersion", 1);
        writer.WriteString("product", ProductId.Value);
        writer.WriteStartObject("template");
        writer.WriteString("id", TemplateId.Value);
        writer.WriteString("version", TemplateVersion.Value);
        writer.WriteEndObject();
        writer.WriteStartObject("provider");
        writer.WriteString("moduleId", Provider.ModuleId.Value);
        writer.WriteString("moduleVersion", Provider.ModuleVersion.Value);
        writer.WriteString("type", Provider.Type);
        writer.WriteStartObject("artifact");
        writer.WriteString("kind", "managed-assembly");
        writer.WriteString("sha256", Provider.ArtifactHash.Value);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    });

    /// <summary>Decodes canonical metadata only; it does not authorize a provider or an operation.</summary>
    internal static SiteFactoryLockV2? Read(byte[] bytes)
    {
        using JsonDocument? document = GenerationV2Json.Parse(bytes, GenerationV2Json.MaximumLockLength);
        if (document is null) return null;
        SiteFactoryLockV2 parsed;
        try
        {
            JsonElement root = document.RootElement;
            GenerationV2Json.RequireProperties(root, Properties);
            GenerationV2Json.RequireVersion(root, "schemaVersion", 2);
            GenerationV2Json.RequireVersion(root, "generationStateSchemaVersion", 2);
            GenerationV2Json.RequireVersion(root, "configurationSchemaVersion", 1);
            JsonElement template = root.GetProperty("template");
            JsonElement provider = root.GetProperty("provider");
            GenerationV2Json.RequireProperties(template, TemplateProperties);
            GenerationV2Json.RequireProperties(provider, ProviderProperties);
            JsonElement artifact = provider.GetProperty("artifact");
            GenerationV2Json.RequireProperties(artifact, ArtifactProperties);
            if (GenerationV2Json.Text(artifact, "kind") != "managed-assembly") return null;
            parsed = new(new(GenerationV2Json.Text(root, "siteFactoryVersion")),
                new(GenerationV2Json.Text(root, "product")), new(GenerationV2Json.Text(template, "id")),
                new(GenerationV2Json.Text(template, "version")),
                new(new(GenerationV2Json.Text(provider, "moduleId")), new(GenerationV2Json.Text(provider, "moduleVersion")),
                    GenerationV2Json.Text(provider, "type"), GenerationV2Json.Hash(artifact, "sha256")));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
        // A canonical expansion can exceed the encoded budget. Other serializer defects must propagate.
        byte[] canonical;
        try { canonical = parsed.Serialize(); }
        catch (ArgumentException) { return null; }
        return bytes.AsSpan().SequenceEqual(canonical) ? parsed : null;
    }

    /// <summary>Requires the exact expected host binding, rather than adopting a valid foreign lock.</summary>
    internal SiteFactoryLockV2? ReadMatching(byte[] bytes)
    {
        SiteFactoryLockV2? parsed = Read(bytes);
        return parsed == this ? parsed : null;
    }
}
