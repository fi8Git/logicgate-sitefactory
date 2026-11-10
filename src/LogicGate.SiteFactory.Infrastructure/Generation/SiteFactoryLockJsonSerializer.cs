using System.Buffers;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Explicit canonical lock V1 JSON mapping, independent of culture, paths, clocks and collection discovery.</summary>
public static class SiteFactoryLockJsonSerializer
{
    public static byte[] Serialize(SiteFactoryLock candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
        {
            Indented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", candidate.SchemaVersion);
            writer.WriteString("siteFactoryVersion", candidate.SiteFactoryVersion.Value);
            writer.WriteNumber("configurationSchemaVersion", candidate.ConfigurationSchemaVersion);
            writer.WriteString("product", candidate.ProductId.Value);
            writer.WriteStartObject("template");
            writer.WriteString("id", candidate.TemplateId.Value);
            writer.WriteString("version", candidate.TemplateVersion.Value);
            writer.WriteNumber("manifestSchemaVersion", candidate.TemplateManifestSchemaVersion);
            writer.WriteEndObject();
            // This bounded contract rejects non-empty module selections before construction.
            // Do not export requested module IDs as if their versions had been resolved.
            writer.WriteStartArray("modules");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        buffer.GetSpan(1)[0] = (byte)'\n';
        buffer.Advance(1);
        return buffer.WrittenSpan.ToArray();
    }
}
