using System.Globalization;
using System.Reflection;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Domain;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationPlanTests
{
    [Fact]
    public void MinimalPlanDescribesItsIdentityAndSchemaWithoutRequiringFiles()
    {
        GenerationPlan plan = CreatePlan();

        Assert.Equal(1, plan.SchemaVersion);
        Assert.Equal(new GenerationPlanId("test-plan-001"), plan.Id);
        Assert.Equal(new ClientSlug("acme-services"), plan.ClientSlug);
        Assert.Equal(new ProductId("fixture-product"), plan.ProductId);
        Assert.Equal(new TemplateId("fixture-template"), plan.TemplateId);
        Assert.Empty(plan.Files);
        Assert.Empty(plan.Diagnostics);
        Assert.False(plan.HasErrors);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void FilesAndDiagnosticsHaveTheSameOrdinalOrderRegardlessOfInputOrderOrCulture(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            PlannedFile[] expectedFiles =
            [
                CreateFile("Zeta.md", FileGenerationStrategy.Generated),
                CreateFile("alpha.md", FileGenerationStrategy.Generated),
                CreateFile("alpha.md", FileGenerationStrategy.CreateOnce),
                CreateFile("équipe.md", FileGenerationStrategy.Protected),
            ];
            GenerationPlanDiagnostic[] expectedDiagnostics =
            [
                CreateDiagnostic(),
                CreateDiagnostic(propertyPath: "Zeta"),
                CreateDiagnostic(propertyPath: "alpha", message: "A test message."),
                CreateDiagnostic(propertyPath: "alpha", severity: DiagnosticSeverity.Warning),
                CreateDiagnostic(propertyPath: "alpha"),
                CreateDiagnostic(propertyPath: "alpha", code: "TEST-PLAN-002"),
                CreateDiagnostic(propertyPath: "équipe"),
            ];

            GenerationPlan plan = CreatePlan(expectedFiles.Reverse(), expectedDiagnostics.Reverse());
            GenerationPlan repeated = CreatePlan(expectedFiles, expectedDiagnostics);

            Assert.Equal(expectedFiles, plan.Files);
            Assert.Equal(expectedDiagnostics, plan.Diagnostics);
            Assert.Equal(repeated.Files, plan.Files);
            Assert.Equal(repeated.Diagnostics, plan.Diagnostics);
            Assert.Equal(repeated.Id, plan.Id);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void NewMetadataBreaksPathAndStrategyTiesDeterministically(string cultureName)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            PlannedFile[] expected =
            [
                CreateTiedFile("Zeta", "Zeta"),
                CreateTiedFile("Zeta", "alpha"),
                CreateTiedFile("alpha", "alpha"),
                CreateTiedFile("équipe", "alpha"),
                CreateTiedFile("file", "zeta"),
                CreateTiedFile("file", "alpha", kind: PlannedFileOriginKind.Template),
                CreateTiedFile("file", "alpha", kind: PlannedFileOriginKind.Module),
                CreateTiedFile("file", "alpha", action: PlannedFileAction.Preserve),
                CreateTiedFile("file", "alpha", action: PlannedFileAction.Update),
                CreateTiedFile("file", "alpha", action: PlannedFileAction.Conflict),
            ];

            GenerationPlan reversed = CreatePlan(expected.Reverse());
            GenerationPlan rotated = CreatePlan(expected.Skip(4).Concat(expected.Take(4)));

            Assert.Equal(expected, reversed.Files);
            Assert.Equal(expected, rotated.Files);
            Assert.Empty(reversed.Diagnostics);
            Assert.False(reversed.HasErrors);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void CollectionsAreSnapshotsAndCannotBeMutatedThroughTheExposedLists()
    {
        PlannedFile file = CreateFile("README.md", FileGenerationStrategy.CreateOnce);
        GenerationPlanDiagnostic diagnostic = CreateDiagnostic();
        List<PlannedFile> files = [file];
        List<GenerationPlanDiagnostic> diagnostics = [diagnostic];
        GenerationPlan plan = CreatePlan(files, diagnostics);

        files.Clear();
        diagnostics.Clear();
        IList<PlannedFile> exposedFiles = Assert.IsAssignableFrom<IList<PlannedFile>>(plan.Files);
        IList<GenerationPlanDiagnostic> exposedDiagnostics = Assert.IsAssignableFrom<IList<GenerationPlanDiagnostic>>(plan.Diagnostics);

        Assert.True(exposedFiles.IsReadOnly);
        Assert.True(exposedDiagnostics.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposedFiles.Clear());
        Assert.Throws<NotSupportedException>(() => exposedFiles[0] = CreateFile("other.md", FileGenerationStrategy.Generated));
        Assert.Throws<NotSupportedException>(() => exposedDiagnostics.Clear());
        Assert.Throws<NotSupportedException>(() => exposedDiagnostics[0] = CreateDiagnostic(DiagnosticSeverity.Warning));
        Assert.Same(file, Assert.Single(plan.Files));
        Assert.Same(diagnostic, Assert.Single(plan.Diagnostics));
        Assert.True(plan.HasErrors);
    }

    [Fact]
    public void DuplicateFilesArePreservedWithoutCollisionDetection()
    {
        PlannedFile file = CreateFile("README.md", FileGenerationStrategy.Generated);
        GenerationPlan plan = CreatePlan([file, file, CreateFile("readme.md", FileGenerationStrategy.CreateOnce)]);

        Assert.Equal(3, plan.Files.Count);
        Assert.Same(file, plan.Files[0]);
        Assert.Same(file, plan.Files[1]);
        Assert.Empty(plan.Diagnostics);
        Assert.False(plan.HasErrors);
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Information, false)]
    [InlineData(DiagnosticSeverity.Warning, false)]
    [InlineData(DiagnosticSeverity.Error, true)]
    public void OnlySuppliedErrorDiagnosticsSetHasErrors(DiagnosticSeverity severity, bool expected)
    {
        GenerationPlan plan = CreatePlan(diagnostics: [CreateDiagnostic(DiagnosticSeverity.Information), CreateDiagnostic(severity)]);

        Assert.Equal(expected, plan.HasErrors);
        Assert.Equal(2, plan.Diagnostics.Count);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("clientSlug")]
    [InlineData("productId")]
    [InlineData("templateId")]
    [InlineData("files")]
    [InlineData("diagnostics")]
    public void RequiredInputsCannotBeNull(string parameter)
    {
        Assert.Throws<ArgumentNullException>(parameter, () => new GenerationPlan(
            parameter == "id" ? null! : new("test-plan-001"),
            parameter == "clientSlug" ? null! : new("acme-services"),
            parameter == "productId" ? null! : new("fixture-product"),
            parameter == "templateId" ? null! : new("fixture-template"),
            parameter == "files" ? null! : [],
            parameter == "diagnostics" ? null! : []));
    }

    [Fact]
    public void NullEntriesAreRejected()
    {
        Assert.Throws<ArgumentException>("files", () => CreatePlan(files: [null!]));
        Assert.Throws<ArgumentException>("diagnostics", () => CreatePlan(diagnostics: [null!]));
    }

    [Fact]
    public void ContractsExposeNoPublicSettersOrMutableFields()
    {
        Type[] contracts =
        [
            typeof(GenerationPlan), typeof(GenerationPlanId), typeof(ClientSlug),
            typeof(ProductId), typeof(TemplateId), typeof(PlannedFile), typeof(GenerationPlanDiagnostic),
            typeof(PlannedFileId), typeof(PlannedFileOrigin),
        ];

        foreach (Type contract in contracts)
        {
            Assert.True(contract.IsSealed);
            Assert.All(contract.GetProperties(), static property => Assert.Null(property.SetMethod));
            Assert.Empty(contract.GetFields(BindingFlags.Instance | BindingFlags.Public));
        }
    }

    [Fact]
    public void ContractAssembliesReferenceOnlyTheRuntimeDomainAndModuleSdk()
    {
        Assembly domain = typeof(ClientSlug).Assembly;
        Assembly application = typeof(GenerationPlan).Assembly;

        Assert.Equal("LogicGate.SiteFactory.Domain", domain.GetName().Name);
        Assert.Equal("LogicGate.SiteFactory.Application", application.GetName().Name);
        Assert.All(domain.GetReferencedAssemblies(), static reference =>
            Assert.StartsWith("System.", reference.Name, StringComparison.Ordinal));
        Assert.All(application.GetReferencedAssemblies(), reference =>
            Assert.True(reference.Name == domain.GetName().Name
                || reference.Name == "LogicGate.SiteFactory.ModuleSdk"
                || reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
                $"Unexpected contract dependency: {reference.Name}"));
    }

    private static PlannedFile CreateFile(string relativePath, FileGenerationStrategy strategy)
    {
        return new PlannedFile(new("test-file-001"), relativePath,
            new(PlannedFileOriginKind.Generator, "sitefactory"), strategy, PlannedFileAction.Create);
    }

    private static PlannedFile CreateTiedFile(
        string id,
        string componentId,
        PlannedFileOriginKind kind = PlannedFileOriginKind.Generator,
        PlannedFileAction action = PlannedFileAction.Create)
    {
        return new PlannedFile(new(id), "README.md", new(kind, componentId), FileGenerationStrategy.Generated, action);
    }

    private static GenerationPlan CreatePlan(
        IEnumerable<PlannedFile>? files = null,
        IEnumerable<GenerationPlanDiagnostic>? diagnostics = null)
    {
        return new GenerationPlan(new("test-plan-001"), new("acme-services"), new("fixture-product"), new("fixture-template"), files ?? [], diagnostics ?? []);
    }

    private static GenerationPlanDiagnostic CreateDiagnostic(
        DiagnosticSeverity severity = DiagnosticSeverity.Error,
        string? propertyPath = null,
        string code = "TEST-PLAN-001",
        string message = "Test diagnostic.")
    {
        return new GenerationPlanDiagnostic(new(code), severity, message, propertyPath);
    }
}
