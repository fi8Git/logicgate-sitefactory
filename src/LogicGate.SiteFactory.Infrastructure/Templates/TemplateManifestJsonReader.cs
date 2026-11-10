using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;

namespace LogicGate.SiteFactory.Infrastructure.Templates;

/// <summary>Strict mapping of the minimal manifest schema, with no external source resolution.</summary>
public sealed class TemplateManifestJsonReader : ITemplateManifestReader
{
    public TemplateCatalogReadResult Read(string document)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(document, new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement root = parsed.RootElement;
            RequireProperties(root, "schemaVersion", "templateId", "templateVersion", "supportedProductIds", "files");

            if (root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number
                || !root.GetProperty("schemaVersion").TryGetInt32(out int schemaVersion) || schemaVersion != 2)
            {
                throw new FormatException("Unsupported manifest schema.");
            }

            JsonElement products = root.GetProperty("supportedProductIds");
            JsonElement files = root.GetProperty("files");

            if (products.ValueKind != JsonValueKind.Array || files.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("Manifest collections must be arrays.");
            }

            TemplateManifest manifest = new(
                new TemplateId(ReadString(root.GetProperty("templateId"))),
                new TemplateVersion(ReadString(root.GetProperty("templateVersion"))),
                products.EnumerateArray().Select(static product => new ProductId(ReadString(product))),
                files.EnumerateArray().Select(ReadFile));
            return TemplateCatalogReadResult.Success([manifest]);
        }
        catch (JsonException)
        {
            return TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest);
        }
        catch (FormatException)
        {
            return TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest);
        }
        catch (ArgumentException)
        {
            // Only input-to-contract construction occurs in this boundary; catalog/resolver faults are not caught.
            return TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest);
        }
    }

    private static TemplateFile ReadFile(JsonElement file)
    {
        RequireProperties(file, "id", "relativePath", "source", "strategy");
        FileGenerationStrategy strategy = ReadString(file.GetProperty("strategy")) switch
        {
            "generated" => FileGenerationStrategy.Generated,
            "create-once" => FileGenerationStrategy.CreateOnce,
            "merge" => FileGenerationStrategy.Merge,
            "custom" => FileGenerationStrategy.Custom,
            "protected" => FileGenerationStrategy.Protected,
            _ => throw new FormatException("Unknown manifest file strategy."),
        };
        return new TemplateFile(new PlannedFileId(ReadString(file.GetProperty("id"))),
            ReadString(file.GetProperty("relativePath")), ReadString(file.GetProperty("source")), strategy);
    }

    private static string ReadString(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new FormatException("A manifest string is required.");
        }

        try
        {
            return element.GetString()!;
        }
        catch (InvalidOperationException exception)
        {
            // GetString can reject malformed escaped UTF-16 after the JSON structure was parsed.
            throw new FormatException("Invalid manifest string encoding.", exception);
        }
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("A manifest object is required.");
        }

        HashSet<string> remaining = new(names, StringComparer.Ordinal);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            string name;

            try
            {
                name = property.Name;
            }
            catch (InvalidOperationException exception)
            {
                throw new FormatException("Invalid manifest property encoding.", exception);
            }

            if (!remaining.Remove(name))
            {
                throw new FormatException("Unknown or duplicate manifest property.");
            }
        }

        if (remaining.Count != 0)
        {
            throw new FormatException("Missing manifest property.");
        }
    }
}
