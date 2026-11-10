using System.Buffers;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Explicit UTF-8/LF report mapping; no reflection dump, clock, environment, filesystem or provider text.</summary>
public static class GenerationReportJsonSerializer
{
    public static byte[] Serialize(GenerationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
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
            writer.WriteNumber("schemaVersion", report.SchemaVersion);
            writer.WriteString("scope", "reported-file-operations");
            writer.WriteString("status", report.Status switch
            {
                GenerationExecutionStatus.Succeeded => "success",
                GenerationExecutionStatus.Conflict => "conflict",
                GenerationExecutionStatus.Failed => "failed",
                _ => throw new InvalidOperationException("Unsupported generation result."),
            });
            writer.WriteString("siteFactoryVersion", report.SiteFactoryVersion.Value);
            WritePaths(writer, report, "createdFiles", FileGenerationStatus.Created);
            WritePaths(writer, report, "modifiedFiles", FileGenerationStatus.Updated);
            WritePaths(writer, report, "unchangedFiles", FileGenerationStatus.Unchanged);
            WritePaths(writer, report, "preservedFiles", FileGenerationStatus.Preserved);
            WritePaths(writer, report, "skippedFiles", FileGenerationStatus.Skipped);
            WritePaths(writer, report, "protectedFiles", FileGenerationStatus.Protected);
            WritePaths(writer, report, "conflicts", FileGenerationStatus.Conflict);
            WritePaths(writer, report, "failedFiles", FileGenerationStatus.Failed);
            writer.WriteStartArray("operations");
            foreach (GenerationReportOperation operation in report.Operations) WriteOperation(writer, operation);
            writer.WriteEndArray();
            writer.WriteStartArray("diagnostics");
            foreach (GenerationReportDiagnostic diagnostic in report.Diagnostics)
            {
                writer.WriteStartObject();
                if (diagnostic.OperationIndex is int index) writer.WriteNumber("operationIndex", index);
                else writer.WriteNull("operationIndex");
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("severity", "error");
                writer.WriteString("message", diagnostic.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        buffer.GetSpan(1)[0] = (byte)'\n';
        buffer.Advance(1);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WritePaths(Utf8JsonWriter writer, GenerationReport report, string name, FileGenerationStatus status)
    {
        writer.WriteStartArray(name);
        foreach (GenerationReportOperation operation in report.Operations.Where(operation => operation.Result == status))
            writer.WriteStringValue(operation.Path);
        writer.WriteEndArray();
    }

    private static void WriteOperation(Utf8JsonWriter writer, GenerationReportOperation operation)
    {
        writer.WriteStartObject();
        writer.WriteString("path", operation.Path);
        writer.WriteString("strategy", operation.Strategy switch
        {
            FileGenerationStrategy.Generated => "generated",
            FileGenerationStrategy.CreateOnce => "create-once",
            FileGenerationStrategy.Merge => "merge",
            FileGenerationStrategy.Custom => "custom",
            FileGenerationStrategy.Protected => "protected",
            _ => throw new InvalidOperationException("Unsupported file strategy."),
        });
        writer.WriteString("plannedAction", operation.PlannedAction switch
        {
            PlannedFileAction.Create => "create",
            PlannedFileAction.Update => "update",
            PlannedFileAction.Preserve => "preserve",
            PlannedFileAction.Conflict => "conflict",
            _ => throw new InvalidOperationException("Unsupported planned action."),
        });
        writer.WriteString("sourceKind", operation.SourceKind switch
        {
            PlannedFileOriginKind.Generator => "generator",
            PlannedFileOriginKind.Template => "template",
            PlannedFileOriginKind.Module => "module",
            _ => throw new InvalidOperationException("Unsupported source kind."),
        });
        writer.WriteString("sourceComponent", operation.SourceComponent);
        writer.WriteString("sourceVersion", operation.SourceVersion);
        if (operation.DestinationExistedAtInspection is bool existed) writer.WriteBoolean("destinationExistedAtInspection", existed);
        else writer.WriteNull("destinationExistedAtInspection");
        writer.WriteString("previousHash", operation.PreviousHash?.Value);
        writer.WriteString("currentHash", operation.CurrentHash?.Value);
        writer.WriteString("proposedHash", operation.ProposedHash?.Value);
        writer.WriteString("result", operation.Result switch
        {
            FileGenerationStatus.Created => "created",
            FileGenerationStatus.Updated => "updated",
            FileGenerationStatus.Unchanged => "unchanged",
            FileGenerationStatus.Preserved => "preserved",
            FileGenerationStatus.Skipped => "skipped",
            FileGenerationStatus.Protected => "protected",
            FileGenerationStatus.Conflict => "conflict",
            FileGenerationStatus.Failed => "failed",
            _ => throw new InvalidOperationException("Unsupported file result."),
        });
        writer.WriteString("conflictCode", operation.ConflictCode);
        writer.WriteEndObject();
    }
}
