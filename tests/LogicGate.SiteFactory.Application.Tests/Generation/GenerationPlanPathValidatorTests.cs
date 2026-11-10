using System.Globalization;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class GenerationPlanPathValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../secret")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\temp\\file")]
    [InlineData("C:/temp/file")]
    [InlineData("C:temp/file")]
    [InlineData("\\")]
    [InlineData("\\\\server\\share\\file")]
    [InlineData("//server/share/file")]
    [InlineData("\\\\?\\C:\\temp\\file")]
    [InlineData("src\\mixed/file.cs")]
    [InlineData("src/mixed\\file.cs")]
    [InlineData("src/../secret")]
    [InlineData("src/./file.cs")]
    [InlineData("src//file.cs")]
    [InlineData("src/")]
    [InlineData("file.cs.")]
    [InlineData("file.cs ")]
    [InlineData("file:stream")]
    [InlineData("file\0name")]
    public void UnsafeOrNonCanonicalPathsCannotEnterEitherFileContract(string path)
    {
        Assert.Throws<ArgumentException>("relativePath", () => File(path));
        Assert.Throws<ArgumentException>("relativePath", () => new TemplateFile(new("file-id"), path, "logical/source", FileGenerationStrategy.Generated));
    }

    [Fact]
    public void NormalPathsSharedDirectoriesAndSimilarPrefixesRemainUnchanged()
    {
        PlannedFile[] files = [File("src/Exemple fictif/Équipe.cs"), File("src/Exemple fictif/Other.cs"), File("assets"), File("assets-other/site.css")];
        PlannedFile[] before = files.ToArray();

        Assert.Empty(GenerationPlanPathValidator.Validate(files));
        Assert.Equal(before, files);
        Assert.Empty(GenerationPlanPathValidator.Validate([]));
    }

    [Fact]
    public void ExactDuplicatesAreRejectedEvenForIdenticalObjectsOrDifferentActionsAndOrigins()
    {
        PlannedFile first = File("README.md");
        PlannedFile second = new(new("different-id"), first.RelativePath,
            new(PlannedFileOriginKind.Module, "other-component"), FileGenerationStrategy.Protected, PlannedFileAction.Preserve);

        foreach (PlannedFile[] pair in new[] { new[] { first, first }, new[] { first, second }, new[] { second, first } })
        {
            GenerationPlanDiagnostic diagnostic = Assert.Single(GenerationPlanPathValidator.Validate(pair));
            Assert.Equal("SF-GEN-002", diagnostic.Code.Value);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("README.md", diagnostic.PropertyPath);
            Assert.Equal("Plusieurs fichiers planifiés désignent la même cible logique.", diagnostic.Message);
        }
    }

    [Theory]
    [InlineData("README.md", "readme.md", "README.md")]
    [InlineData("src/File.cs", "src/file.cs", "src/File.cs")]
    [InlineData("Docs/one.md", "docs/two.md", "Docs")]
    [InlineData("src/Équipe.cs", "src/équipe.cs", "src/Équipe.cs")]
    public void CasingAmbiguitiesAreReportedForFilesAndImplicitDirectories(string first, string second, string diagnosticPath)
    {
        GenerationPlanDiagnostic diagnostic = Assert.Single(GenerationPlanPathValidator.Validate([File(first), File(second)]));

        Assert.Equal("SF-GEN-FILE-012", diagnostic.Code.Value);
        Assert.Equal(diagnosticPath, diagnostic.PropertyPath);
        Assert.Equal("Des chemins logiques ne se distinguent que par la casse.", diagnostic.Message);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void MultipleErrorsHaveTheSameOrderAndRepresentativePathsAcrossInputOrdersAndCultures(string cultureName)
    {
        PlannedFile[] files = [File("readme.md"), File("README.md"), File("README.md"), File("src"), File("src/file.cs"), File("NUL.txt")];
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            IReadOnlyList<GenerationPlanDiagnostic> first = GenerationPlanPathValidator.Validate(files);
            IReadOnlyList<GenerationPlanDiagnostic> reversed = GenerationPlanPathValidator.Validate(files.Reverse());
            IReadOnlyList<GenerationPlanDiagnostic> rotated = GenerationPlanPathValidator.Validate(files.Skip(2).Concat(files.Take(2)));

            Assert.Equal(first, reversed);
            Assert.Equal(first, rotated);
            Assert.Equal(["NUL.txt", "README.md", "README.md", "src"], first.Select(static diagnostic => diagnostic.PropertyPath));
            Assert.Equal(["SF-GEN-FILE-025", "SF-GEN-002", "SF-GEN-FILE-012", "SF-GEN-002"], first.Select(static diagnostic => diagnostic.Code.Value));
            Assert.All(first, static diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
            Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<GenerationPlanDiagnostic>>(first).Clear());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [MemberData(nameof(ReservedWindowsNames))]
    public void ReservedDeviceNamesAreRejectedInAnySegmentWithOrWithoutAnExtension(string name)
    {
        foreach (string path in new[] { name, $"src/{name.ToLowerInvariant()}.tar.gz", $"src/{name}/file.cs", $"{name} .txt" })
        {
            GenerationPlanDiagnostic diagnostic = Assert.Single(GenerationPlanPathValidator.Validate([File(path)]));
            Assert.Equal("SF-GEN-FILE-025", diagnostic.Code.Value);
            Assert.Equal(path, diagnostic.PropertyPath);
        }
    }

    public static IEnumerable<object[]> ReservedWindowsNames()
    {
        foreach (string name in new[] { "CON", "PRN", "AUX", "NUL", "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³" })
        {
            yield return [name];
        }

        foreach (int number in Enumerable.Range(1, 9))
        {
            yield return ["COM" + number.ToString(CultureInfo.InvariantCulture)];
            yield return ["LPT" + number.ToString(CultureInfo.InvariantCulture)];
        }
    }

    [Theory]
    [InlineData("CONSOLE.txt")]
    [InlineData("COM0")]
    [InlineData("COM10")]
    [InlineData("LPT0")]
    [InlineData("LPT10")]
    [InlineData("auxiliary/file.md")]
    [InlineData(".config/settings.json")]
    [InlineData(".gitignore")]
    public void OrdinaryNamesAreNotConfusedWithReservedDevices(string path)
    {
        Assert.Empty(GenerationPlanPathValidator.Validate([File(path)]));
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".git/config")]
    [InlineData(".git/hooks/post-checkout")]
    [InlineData(".git/objects/example")]
    [InlineData(".GIT/config")]
    [InlineData("foo/.git")]
    [InlineData("foo/.git/config")]
    [InlineData("foo/.GIT/hooks/pre-commit")]
    [InlineData("foo/.Git/hooks/pre-commit")]
    [InlineData("foo/.Git/index")]
    [InlineData("foo/bar/.gIt/config")]
    [InlineData("foo/.git/bar/.git/config")]
    public void GitInternalSegmentsAreRejectedAnywhereWithTheirOriginalLogicalPath(string path)
    {
        GenerationPlanDiagnostic diagnostic = Assert.Single(GenerationPlanPathValidator.Validate([File(path)]));

        Assert.Equal("SF-GEN-FILE-026", diagnostic.Code.Value);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(path, diagnostic.PropertyPath);
        Assert.Equal("Un segment du chemin cible est réservé aux données internes Git.", diagnostic.Message);
    }

    [Theory]
    [InlineData(".gitignore")]
    [InlineData(".gitattributes")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData("github.txt")]
    [InlineData("some.git.txt")]
    [InlineData("foo/.gitignore")]
    [InlineData("foo/.GITHUB/workflows/ci.yml")]
    [InlineData(".git-backup/config")]
    public void GitRelatedNamesWithoutAnExactGitSegmentRemainAllowed(string path)
    {
        Assert.Empty(GenerationPlanPathValidator.Validate([File(path)]));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Generated, PlannedFileAction.Create)]
    [InlineData(FileGenerationStrategy.CreateOnce, PlannedFileAction.Create)]
    [InlineData(FileGenerationStrategy.Merge, PlannedFileAction.Conflict)]
    [InlineData(FileGenerationStrategy.Custom, PlannedFileAction.Preserve)]
    [InlineData(FileGenerationStrategy.Protected, PlannedFileAction.Preserve)]
    public void NoStrategyExemptsGitInternalSegments(FileGenerationStrategy strategy, PlannedFileAction action)
    {
        PlannedFile file = new(new("git-file"), ".git/config", new(PlannedFileOriginKind.Template, "test-template"), strategy, action);

        GenerationPlanDiagnostic diagnostic = Assert.Single(GenerationPlanPathValidator.Validate([file]));

        Assert.Equal("SF-GEN-FILE-026", diagnostic.Code.Value);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void GitInternalDiagnosticsAreDeterministicAcrossInputOrdersAndCultures(string cultureName)
    {
        PlannedFile[] files = [File("z/.git/config"), File(".GIT/config"), File("a/.Git/index")];
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            IReadOnlyList<GenerationPlanDiagnostic> first = GenerationPlanPathValidator.Validate(files);

            Assert.Equal([".GIT/config", "a/.Git/index", "z/.git/config"], first.Select(static diagnostic => diagnostic.PropertyPath));
            Assert.All(first, static diagnostic => Assert.Equal("SF-GEN-FILE-026", diagnostic.Code.Value));
            Assert.Equal(first, GenerationPlanPathValidator.Validate(files.Reverse()));
            Assert.Equal(first, GenerationPlanPathValidator.Validate(files));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void NullCollectionAndNullEntriesRemainProgrammingErrors()
    {
        Assert.Throws<ArgumentNullException>("files", () => GenerationPlanPathValidator.Validate(null!));
        Assert.Throws<ArgumentException>("files", () => GenerationPlanPathValidator.Validate([null!]));
    }

    private static PlannedFile File(string path) => new(new("supplied-file-id"), path,
        new(PlannedFileOriginKind.Template, "test-template"), FileGenerationStrategy.Generated, PlannedFileAction.Create);
}
