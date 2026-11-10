using System.Globalization;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GenerationPlanJsonSerializerTests
{
    [Fact]
    public async Task RealMinimalPlanMatchesGoldenBytesOnEverySerialization()
    {
        ClientConfigurationV1 configuration = Configuration();
        Assert.True(ClientConfigurationV1Validator.Validate(configuration).IsValid);
        TemplateResolver resolver = new(new DocumentationModule(new TemplateManifestJsonReader()));
        TemplateResolutionResult resolution = await resolver.ResolveAsync(configuration, TestContext.Current.CancellationToken);
        Assert.True(resolution.IsSuccess);
        Assert.NotNull(resolution.Template);
        GenerationPlan first = GenerationPlanBuilder.Build(new("sample-plan"), configuration, resolution.Template);
        GenerationPlan second = GenerationPlanBuilder.Build(new("sample-plan"), configuration, resolution.Template);
        Assert.False(first.HasErrors);
        Assert.Empty(first.Diagnostics);

        string goldenPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "generation-plan", "valid-minimal", "generation-plan.json");
        byte[] expected = await System.IO.File.ReadAllBytesAsync(goldenPath, TestContext.Current.CancellationToken);
        byte[] actual = GenerationPlanJsonSerializer.Serialize(first);

        Assert.Equal(expected, actual);
        Assert.Equal(actual, GenerationPlanJsonSerializer.Serialize(first));
        Assert.Equal(actual, GenerationPlanJsonSerializer.Serialize(second));
        using JsonDocument document = JsonDocument.Parse(actual);
        Assert.Equal(["schemaVersion", "id", "client", "product", "template", "plannedFiles", "diagnostics"],
            document.RootElement.EnumerateObject().Select(static property => property.Name));
        string json = new UTF8Encoding(false, true).GetString(actual);
        Assert.DoesNotContain(configuration.Client.DisplayName, json, StringComparison.Ordinal);
        Assert.DoesNotContain(configuration.Site.PublicBaseUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain("initializeLocalRepository", json, StringComparison.Ordinal);
        Assert.DoesNotContain("publishRemote", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyPlanHasExplicitEmptyArraysAndNoOperationalMetadata()
    {
        using JsonDocument document = JsonDocument.Parse(GenerationPlanJsonSerializer.Serialize(Plan()));
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("sample-plan", root.GetProperty("id").GetString());
        Assert.Equal("fictitious-documentation", root.GetProperty("client").GetString());
        Assert.Equal("documentation", root.GetProperty("product").GetString());
        Assert.Equal("documentation-minimal", root.GetProperty("template").GetString());
        Assert.Empty(root.GetProperty("plannedFiles").EnumerateArray());
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(7, root.EnumerateObject().Count());
    }

    [Fact]
    public void OutputIsUtf8WithoutBomWithTwoSpaceIndentationAndExactlyOneFinalLf()
    {
        byte[] bytes = GenerationPlanJsonSerializer.Serialize(Plan([File("readme", "README.md")]));
        string json = new UTF8Encoding(false, true).GetString(bytes);

        Assert.Equal((byte)'{', bytes[0]);
        Assert.Equal((byte)'}', bytes[^2]);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain('\r', json);
        Assert.DoesNotContain('\t', json);
        Assert.Contains("\n  \"schemaVersion\": 1,\n", json, StringComparison.Ordinal);
        Assert.Contains("\n    {\n      \"id\": \"readme\",\n", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void EquivalentInputsRemainByteIdenticalAcrossCulturesAndInputOrders(string cultureName)
    {
        PlannedFile[] files = [File("z", "z.md"), File("upper-i", "I.md"), File("accent", "é.md"), File("lower-i", "i.md")];
        GenerationPlanDiagnostic[] diagnostics =
        [
            new(new("SF-TEST-002"), DiagnosticSeverity.Warning, "Éléments préservés.", "z.md"),
            new(new("SF-TEST-001"), DiagnosticSeverity.Information, "Résumé fictif."),
        ];
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            byte[] expected = GenerationPlanJsonSerializer.Serialize(Plan(files, diagnostics));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

            byte[] actual = GenerationPlanJsonSerializer.Serialize(Plan(files.Reverse(), diagnostics.Reverse()));

            Assert.Equal(expected, actual);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Generated, "generated")]
    [InlineData(FileGenerationStrategy.CreateOnce, "create-once")]
    [InlineData(FileGenerationStrategy.Merge, "merge")]
    [InlineData(FileGenerationStrategy.Custom, "custom")]
    [InlineData(FileGenerationStrategy.Protected, "protected")]
    public void StrategiesUseExactlyTheAcceptedAdrNames(FileGenerationStrategy strategy, string expected)
    {
        JsonElement file = SerializedFile(File("readme", "README.md", strategy: strategy));

        Assert.Equal(expected, file.GetProperty("strategy").GetString());
    }

    [Theory]
    [InlineData(PlannedFileAction.Create, "create")]
    [InlineData(PlannedFileAction.Preserve, "preserve")]
    [InlineData(PlannedFileAction.Update, "update")]
    [InlineData(PlannedFileAction.Conflict, "conflict")]
    public void ActionsAreSerializedWithoutReinterpretingTheIntention(PlannedFileAction action, string expected)
    {
        JsonElement file = SerializedFile(File("readme", "README.md", action: action));

        Assert.Equal(expected, file.GetProperty("action").GetString());
    }

    [Theory]
    [InlineData(PlannedFileOriginKind.Generator, "generator")]
    [InlineData(PlannedFileOriginKind.Template, "template")]
    [InlineData(PlannedFileOriginKind.Module, "module")]
    public void FilesKeepStableIdsLogicalPathsAndExplicitOrigins(PlannedFileOriginKind kind, string expected)
    {
        JsonElement file = SerializedFile(File("stable-readme-id", "docs/README.md", kind: kind));

        Assert.Equal(["id", "relativePath", "origin", "strategy", "action"],
            file.EnumerateObject().Select(static property => property.Name));
        Assert.Equal("stable-readme-id", file.GetProperty("id").GetString());
        Assert.Equal("docs/README.md", file.GetProperty("relativePath").GetString());
        JsonElement origin = file.GetProperty("origin");
        Assert.Equal(["kind", "componentId"], origin.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(expected, origin.GetProperty("kind").GetString());
        Assert.Equal("sample-component", origin.GetProperty("componentId").GetString());
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Information, "information")]
    [InlineData(DiagnosticSeverity.Warning, "warning")]
    [InlineData(DiagnosticSeverity.Error, "error")]
    public void DiagnosticsKeepStableCodesAndSeverityIndependentlyOfHumanMessages(DiagnosticSeverity severity, string expected)
    {
        GenerationPlan plan = Plan(diagnostics: [new(new("SF-TEST-001"), severity, "Message fictif.", "docs/readme.md")]);
        using JsonDocument document = JsonDocument.Parse(GenerationPlanJsonSerializer.Serialize(plan));
        JsonElement diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());

        Assert.Equal(["code", "severity", "message", "propertyPath"],
            diagnostic.EnumerateObject().Select(static property => property.Name));
        Assert.Equal("SF-TEST-001", diagnostic.GetProperty("code").GetString());
        Assert.Equal(expected, diagnostic.GetProperty("severity").GetString());
        Assert.Equal("Message fictif.", diagnostic.GetProperty("message").GetString());
        Assert.Equal("docs/readme.md", diagnostic.GetProperty("propertyPath").GetString());
    }

    [Theory]
    [InlineData("Résumé fictif : été, 東京, 😀.")]
    [InlineData("Texte avec \"guillemets\", \\ et\nretour\tligne.")]
    [InlineData("<script> & </script>")]
    public void StringEscapingPreservesValuesWithoutInjectingJson(string message)
    {
        GenerationPlan plan = Plan(diagnostics: [new(new("SF-TEST-001"), DiagnosticSeverity.Warning, message)]);
        byte[] bytes = GenerationPlanJsonSerializer.Serialize(plan);
        string json = new UTF8Encoding(false, true).GetString(bytes);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());

        Assert.Equal(message, diagnostic.GetProperty("message").GetString());
        Assert.Equal(["code", "severity", "message"], diagnostic.EnumerateObject().Select(static property => property.Name));
        Assert.DoesNotContain("<script>", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', json);
        Assert.Equal(bytes, GenerationPlanJsonSerializer.Serialize(plan));
    }

    [Fact]
    public void SerializerPreservesPlanOrderDuplicatesAndDiagnosticsWithoutValidatingOrMutatingThem()
    {
        PlannedFile repeated = File("readme", "README.md");
        GenerationPlan plan = Plan([File("z", "z.md"), repeated, repeated],
        [
            new(new("SF-TEST-002"), DiagnosticSeverity.Error, "Second diagnostic.", "z.md"),
            new(new("SF-TEST-001"), DiagnosticSeverity.Information, "Premier diagnostic."),
            new(new("SF-TEST-003"), DiagnosticSeverity.Warning, "Autre diagnostic.", "README.md"),
        ]);
        PlannedFile[] originalFiles = plan.Files.ToArray();
        GenerationPlanDiagnostic[] originalDiagnostics = plan.Diagnostics.ToArray();
        using JsonDocument document = JsonDocument.Parse(GenerationPlanJsonSerializer.Serialize(plan));

        Assert.True(plan.HasErrors);
        Assert.Equal(plan.Files.Select(static file => file.RelativePath), document.RootElement.GetProperty("plannedFiles")
            .EnumerateArray().Select(static file => file.GetProperty("relativePath").GetString()));
        Assert.Equal(plan.Diagnostics.Select(static diagnostic => diagnostic.Code.Value), document.RootElement.GetProperty("diagnostics")
            .EnumerateArray().Select(static diagnostic => diagnostic.GetProperty("code").GetString()));
        Assert.Equal(originalFiles, plan.Files);
        Assert.Equal(originalDiagnostics, plan.Diagnostics);
    }

    [Fact]
    public async Task BlockedPlanFromRealPipelineRetainsTheErrorAndEmptyFileList()
    {
        TemplateResolutionResult resolution = await new TemplateResolver(new DocumentationModule(new TemplateManifestJsonReader()))
            .ResolveAsync(Configuration(), TestContext.Current.CancellationToken);
        Assert.NotNull(resolution.Template);
        TemplateManifest conflicting = new(resolution.Template.TemplateId, resolution.Template.TemplateVersion,
            resolution.Template.SupportedProductIds,
            [new(new("a"), "docs/a.md", "logical/a", FileGenerationStrategy.Generated),
                new(new("b"), "Docs/b.md", "logical/b", FileGenerationStrategy.Generated)]);
        GenerationPlan plan = GenerationPlanBuilder.Build(new("sample-plan"), Configuration(), conflicting);
        Assert.True(plan.HasErrors);
        Assert.Empty(plan.Files);
        GenerationPlanDiagnostic expected = Assert.Single(plan.Diagnostics);
        Assert.Equal("SF-GEN-FILE-012", expected.Code.Value);

        using JsonDocument document = JsonDocument.Parse(GenerationPlanJsonSerializer.Serialize(plan));

        Assert.Empty(document.RootElement.GetProperty("plannedFiles").EnumerateArray());
        JsonElement actual = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(expected.Code.Value, actual.GetProperty("code").GetString());
        Assert.Equal("error", actual.GetProperty("severity").GetString());
        Assert.Equal(expected.Message, actual.GetProperty("message").GetString());
        Assert.Equal(expected.PropertyPath, actual.GetProperty("propertyPath").GetString());
    }

    [Fact]
    public void EachCallReturnsIndependentBytesAndKeepsTheExplicitPlanId()
    {
        GenerationPlan plan = Plan();
        byte[] first = GenerationPlanJsonSerializer.Serialize(plan);
        byte[] second = GenerationPlanJsonSerializer.Serialize(plan);
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        first[0] = 0;

        Assert.Equal(second, GenerationPlanJsonSerializer.Serialize(plan));
        using JsonDocument document = JsonDocument.Parse(GenerationPlanJsonSerializer.Serialize(Plan(id: "another-explicit-id")));
        Assert.Equal("another-explicit-id", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("sample-plan", plan.Id.Value);
    }

    [Fact]
    public void NullPlanIsAProgrammingError()
    {
        Assert.Throws<ArgumentNullException>("plan", () => GenerationPlanJsonSerializer.Serialize(null!));
    }

    private static JsonElement SerializedFile(PlannedFile file)
    {
        using JsonDocument document = JsonDocument.Parse(GenerationPlanJsonSerializer.Serialize(Plan([file])));
        return Assert.Single(document.RootElement.GetProperty("plannedFiles").EnumerateArray()).Clone();
    }

    private static GenerationPlan Plan(IEnumerable<PlannedFile>? files = null, IEnumerable<GenerationPlanDiagnostic>? diagnostics = null,
        string id = "sample-plan") =>
        new(new(id), new("fictitious-documentation"), new("documentation"), new("documentation-minimal"), files ?? [], diagnostics ?? []);

    private static PlannedFile File(string id, string path, FileGenerationStrategy strategy = FileGenerationStrategy.Generated,
        PlannedFileAction action = PlannedFileAction.Create, PlannedFileOriginKind kind = PlannedFileOriginKind.Template) =>
        new(new(id), path, new(kind, "sample-component"), strategy, action);

    private static ClientConfigurationV1 Configuration() =>
        new(1, new("fictitious-documentation", "Acme Services"), new("documentation", "documentation-minimal", "fr-FR", "https://www.example.test"),
            new([]), new("docker-compose-vps"), new(true, false));
}
