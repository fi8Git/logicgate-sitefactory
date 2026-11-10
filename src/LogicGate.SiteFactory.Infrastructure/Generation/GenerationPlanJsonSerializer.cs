using System.Buffers;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Serializes a plan in memory; does not validate it or authorize any file operation.</summary>
public static class GenerationPlanJsonSerializer
{
    /// <summary>Returns JSON V1 as UTF-8 without BOM, with two-space indentation and a final LF.</summary>
    public static byte[] Serialize(GenerationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
        {
            Indented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
        }))
        {
            // Explicit mapping fixes the schema and prevents accidental exposure of future properties.
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", plan.SchemaVersion);
            writer.WriteString("id", plan.Id.Value);
            writer.WriteString("client", plan.ClientSlug.Value);
            writer.WriteString("product", plan.ProductId.Value);
            writer.WriteString("template", plan.TemplateId.Value);
            writer.WriteStartArray("plannedFiles");
            foreach (PlannedFile file in plan.Files)
            {
                WriteFile(writer, file);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("diagnostics");
            foreach (GenerationPlanDiagnostic diagnostic in plan.Diagnostics)
            {
                WriteDiagnostic(writer, diagnostic);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        buffer.GetSpan(1)[0] = (byte)'\n';
        buffer.Advance(1);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteFile(Utf8JsonWriter writer, PlannedFile file)
    {
        writer.WriteStartObject();
        writer.WriteString("id", file.Id.Value);
        writer.WriteString("relativePath", file.RelativePath);
        writer.WriteStartObject("origin");
        writer.WriteString("kind", file.Origin.Kind switch
        {
            PlannedFileOriginKind.Generator => "generator",
            PlannedFileOriginKind.Template => "template",
            PlannedFileOriginKind.Module => "module",
            _ => throw new InvalidOperationException("Unsupported planned file origin kind."),
        });
        writer.WriteString("componentId", file.Origin.ComponentId);
        writer.WriteEndObject();
        writer.WriteString("strategy", file.Strategy switch
        {
            FileGenerationStrategy.Generated => "generated",
            FileGenerationStrategy.CreateOnce => "create-once",
            FileGenerationStrategy.Merge => "merge",
            FileGenerationStrategy.Custom => "custom",
            FileGenerationStrategy.Protected => "protected",
            _ => throw new InvalidOperationException("Unsupported file generation strategy."),
        });
        writer.WriteString("action", file.Action switch
        {
            PlannedFileAction.Create => "create",
            PlannedFileAction.Preserve => "preserve",
            PlannedFileAction.Update => "update",
            PlannedFileAction.Conflict => "conflict",
            _ => throw new InvalidOperationException("Unsupported planned file action."),
        });
        writer.WriteEndObject();
    }

    private static void WriteDiagnostic(Utf8JsonWriter writer, GenerationPlanDiagnostic diagnostic)
    {
        writer.WriteStartObject();
        writer.WriteString("code", diagnostic.Code.Value);
        writer.WriteString("severity", diagnostic.Severity switch
        {
            DiagnosticSeverity.Information => "information",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Error => "error",
            _ => throw new InvalidOperationException("Unsupported diagnostic severity."),
        });
        writer.WriteString("message", diagnostic.Message);
        if (diagnostic.PropertyPath is not null)
        {
            writer.WriteString("propertyPath", diagnostic.PropertyPath);
        }

        writer.WriteEndObject();
    }
}
