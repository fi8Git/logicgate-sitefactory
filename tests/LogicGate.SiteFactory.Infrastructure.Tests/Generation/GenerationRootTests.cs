using System.Globalization;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GenerationRootTests
{
    [Theory]
    [InlineData("README.md")]
    [InlineData("src/Generated/SiteMetadata.cs")]
    [InlineData("contenus/équipe accueil.md")]
    [InlineData(".gitignore")]
    [InlineData(".gitattributes")]
    [InlineData(".github/workflows/build.yml")]
    [InlineData("nested/.GITIGNORE")]
    [InlineData("nested/.github/readme.md")]
    [InlineData("nested/.git-backup/readme.md")]
    public void CanonicalLogicalFilesResolveUnderAnExplicitRootWithoutCreatingAnything(string logicalPath)
    {
        string rootPath = NewRootPath();
        GenerationRoot root = new(rootPath);
        string expected = Path.Combine([rootPath, .. logicalPath.Split('/')]);

        string first = root.Resolve(logicalPath);
        string second = root.Resolve(logicalPath);

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
        Assert.True(Path.IsPathFullyQualified(first));
        Assert.True(root.ContainsCanonicalPath(first));
        Assert.Equal(logicalPath.Replace('/', Path.DirectorySeparatorChar), Path.GetRelativePath(root.FullPath, first));
        Assert.False(Directory.Exists(rootPath));
        Assert.False(File.Exists(first));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../secret")]
    [InlineData("nested/../../secret")]
    [InlineData("nested/../secret")]
    [InlineData("nested/./file")]
    [InlineData("/")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\temp\\file")]
    [InlineData("c:/temp/file")]
    [InlineData("C:relative.txt")]
    [InlineData("\\rooted.txt")]
    [InlineData("\\\\server\\share\\file")]
    [InlineData("//server/share/file")]
    [InlineData("\\\\?\\C:\\temp\\file")]
    [InlineData("\\\\.\\C:\\temp\\file")]
    [InlineData("nested\\file.txt")]
    [InlineData("nested/mixed\\file.txt")]
    [InlineData("nested//file.txt")]
    [InlineData("nested/file.txt/")]
    [InlineData("nested/file.txt.")]
    [InlineData("nested/file.txt ")]
    [InlineData("nested/file\0.txt")]
    [InlineData("nested/line\nfile")]
    [InlineData("nested/file:stream")]
    [InlineData("nested/*.txt")]
    [InlineData("nested/?.txt")]
    [InlineData("nested/<file>")]
    [InlineData("nested/file|name")]
    [InlineData("nested/\"file\"")]
    [InlineData("~/file")]
    [InlineData("$HOME/file")]
    [InlineData("%USERPROFILE%/file")]
    [InlineData("%2e%2e/secret")]
    [InlineData(".git")]
    [InlineData(".GIT/config")]
    [InlineData("nested/.Git/hooks/pre-commit")]
    [InlineData("nested/.git")]
    [InlineData("CON")]
    [InlineData("nested/nul.txt")]
    [InlineData("nested/CON .txt")]
    [InlineData("aux/file.txt")]
    [InlineData("nested/lpt1.log")]
    [InlineData("nested/COM¹.txt")]
    public void RawPathsAreRejectedAtTheFilesystemBoundaryOnEveryOperatingSystem(string logicalPath)
    {
        GenerationRoot root = new(NewRootPath());

        Assert.Throws<ArgumentException>(nameof(logicalPath), () => root.Resolve(logicalPath));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public void APlannedFileDoesNotBypassTheGitGuardEvenIfPlanValidationWasOmitted()
    {
        PlannedFile file = new(new("git-config"), "nested/.Git/config",
            new(PlannedFileOriginKind.Template, "fixture-template"), FileGenerationStrategy.Generated, PlannedFileAction.Create);
        GenerationRoot root = new(NewRootPath());

        Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve(file.RelativePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/root")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/")]
    [InlineData("C:relative")]
    [InlineData("\\rooted")]
    [InlineData("\\\\server\\share\\root")]
    [InlineData("//server/share/root")]
    [InlineData("\\/server/share/root")]
    [InlineData("/\\server/share/root")]
    [InlineData("\\\\?\\C:\\root")]
    [InlineData("\\\\.\\C:\\root")]
    [InlineData("\\\\?\\UNC\\server\\share\\root")]
    public void RootMustBeAnExplicitAbsoluteLocalDirectoryPath(string rootPath)
    {
        Assert.Throws<ArgumentException>(nameof(rootPath), () => new GenerationRoot(rootPath));
    }

    [Theory]
    [InlineData("../other")]
    [InlineData("nested/../other")]
    [InlineData("nested/./other")]
    [InlineData("nested//other")]
    [InlineData(".git")]
    [InlineData("nested/.GIT/output")]
    [InlineData("CON")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("file:stream")]
    [InlineData("nul\0name")]
    public void InvalidRootSegmentsAreNotSilentlyNormalizedAway(string suffix)
    {
        string rootPath = Path.Combine(NewRootPath(), suffix.Replace('/', Path.DirectorySeparatorChar));

        Assert.Throws<ArgumentException>(nameof(rootPath), () => new GenerationRoot(rootPath));
    }

    [Fact]
    public void VolumeRootAndExistingFilesCannotBeGenerationRoots()
    {
        string volume = Path.GetPathRoot(NewRootPath())!;
        string existingFile = typeof(GenerationRootTests).Assembly.Location;
        Assert.True(File.Exists(existingFile));

        Assert.Throws<ArgumentException>("rootPath", () => new GenerationRoot(volume));
        Assert.Throws<ArgumentException>("rootPath", () => new GenerationRoot(existingFile));
    }

    [Fact]
    public void AnExistingDirectoryAndOneTrailingSeparatorAreAcceptedWithoutChangingResolution()
    {
        string existingDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        GenerationRoot root = new(existingDirectory);
        GenerationRoot trailing = new(existingDirectory + Path.DirectorySeparatorChar);
        GenerationRoot forwardSlashes = new(existingDirectory.Replace(Path.DirectorySeparatorChar, '/'));

        Assert.Equal(existingDirectory, root.FullPath);
        Assert.Equal(root.FullPath, trailing.FullPath);
        Assert.Equal(root.FullPath, forwardSlashes.FullPath);
        Assert.Equal(root.Resolve("nested/file.txt"), trailing.Resolve("nested/file.txt"));
        Assert.Equal(root.Resolve("nested/file.txt"), forwardSlashes.Resolve("nested/file.txt"));
    }

    [Fact]
    public void FinalConfinementRejectsRootItselfParentsAndSiblingPrefixTraps()
    {
        GenerationRoot root = new(NewRootPath());
        string parentEscape = Path.GetFullPath(Path.Combine(root.FullPath, "..", "secret.txt"));
        string sibling = root.FullPath + "-other";
        string siblingFile = Path.Combine(sibling, "secret.txt");

        Assert.False(root.ContainsCanonicalPath(root.FullPath));
        Assert.False(root.ContainsCanonicalPath(root.FullPath + Path.DirectorySeparatorChar));
        Assert.False(root.ContainsCanonicalPath(parentEscape));
        Assert.False(root.ContainsCanonicalPath(sibling));
        Assert.False(root.ContainsCanonicalPath(siblingFile));
        Assert.True(root.ContainsCanonicalPath(Path.Combine(root.FullPath, "nested", "file.txt")));
        Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve("../" + Path.GetFileName(sibling) + "/secret.txt"));
        Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve(siblingFile));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void ConfinementUsesNativeOrdinalCasingRulesNotTheCurrentCulture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            string parent = NewRootPath();
            GenerationRoot root = new(Path.Combine(parent, "CLIENT"));
            string differentCase = Path.Combine(parent, "client", "file.txt");

            Assert.Equal(OperatingSystem.IsWindows(), root.ContainsCanonicalPath(differentCase));
            Assert.True(root.ContainsCanonicalPath(root.Resolve("contenus/équipe.md")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void RootAndLogicalPathCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>("rootPath", () => new GenerationRoot(null!));
        Assert.Throws<ArgumentNullException>("logicalPath", () => new GenerationRoot(NewRootPath()).Resolve(null!));
    }

    public static TheoryData<string, bool> PhysicalComponents => new()
    {
        { new string('a', 255), true },
        { new string('a', 256), false },
        { new string('é', 127) + "a", true }, // 255 UTF-8 bytes, 128 UTF-16 units.
        { new string('é', 128), false },
        { string.Concat(Enumerable.Repeat("😀", 63)) + "abc", true },
        { string.Concat(Enumerable.Repeat("😀", 64)), false },
    };

    [Theory]
    [MemberData(nameof(PhysicalComponents))]
    public void ComponentLimitsApplyToRootDirectoriesAndFileNamesOnEveryPlatform(string component, bool accepted)
    {
        string basePath = NewRootPath();
        string rootWithComponent = Path.Combine(basePath, component);
        GenerationRoot root = new(basePath);
        string[] paths = [component, "deep/nested/" + component + "/file.txt"];
        if (accepted)
        {
            Assert.Equal(rootWithComponent, new GenerationRoot(rootWithComponent).FullPath);
            foreach (string path in paths)
                Assert.True(root.ContainsCanonicalPath(root.Resolve(path)));
        }
        else
        {
            Assert.Throws<ArgumentException>("rootPath", () => new GenerationRoot(rootWithComponent));
            foreach (string path in paths)
                Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve(path));
        }
        Assert.False(Directory.Exists(basePath));
    }

    [Fact]
    public void UnpairedSurrogatesAreRejectedInsteadOfBeingReplacedByUtf8Encoding()
    {
        GenerationRoot root = new(NewRootPath());
        foreach (char surrogate in new[] { '\uD800', '\uDC00' })
        {
            string component = "file-" + surrogate;
            Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve(component));
            Assert.Throws<ArgumentException>("rootPath", () => new GenerationRoot(Path.Combine(root.FullPath, component)));
        }
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullPhysicalPathBudgetIncludesRootAndCountsUtf8Bytes(bool unicodeRoot)
    {
        GenerationRoot root = new(Path.Combine(NewRootPath(), unicodeRoot ? "équipe-😀" : "ascii"));
        int remaining = 4095 - Encoding.UTF8.GetByteCount(root.FullPath) - 1;
        string logicalPath = LongLogicalPath(remaining, 255);

        string resolved = root.Resolve(logicalPath);

        Assert.Equal(4095, Encoding.UTF8.GetByteCount(resolved));
        Assert.True(root.ContainsCanonicalPath(resolved));
        // Add a byte to an intermediate component, not to the already maximal filename.
        Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve("x" + logicalPath));
        Assert.Throws<ArgumentException>("rootPath", () => new GenerationRoot(
            Path.Combine(root.FullPath, ("x" + logicalPath).Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(Directory.Exists(root.FullPath));
    }

    [Fact]
    public void AShortFinalNameMustStillLeaveRoomForTheAtomicSiblingTemporary()
    {
        GenerationRoot root = new(NewRootPath());
        // The writer uses 49 ASCII characters for its random sibling temporary name.
        int parentBudget = 4095 - Encoding.UTF8.GetByteCount(root.FullPath) - 1 - 49;
        string atLimit = LongLogicalPath(parentBudget + 1, 1);
        Assert.True(root.ContainsCanonicalPath(root.Resolve(atLimit)));
        Assert.Throws<ArgumentException>("logicalPath", () => root.Resolve("x" + atLimit));
        Assert.False(Directory.Exists(root.FullPath));
    }

    private static string LongLogicalPath(int length, int fileLength)
    {
        // All components fit individually; only the final combined physical length varies.
        List<string> directories = [];
        int remaining = length - fileLength;
        while (remaining > 201)
        {
            int segmentLength = remaining == 202 ? 199 : 200;
            directories.Add(new string('d', segmentLength));
            remaining -= segmentLength + 1;
        }
        if (remaining > 0) directories.Add(new string('d', remaining - 1));
        return string.Join('/', directories.Append(new string('f', fileLength)));
    }

    // Reserve only a name: no fixture directory, file or cleanup is required for lexical resolution tests.
    private static string NewRootPath() => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sitefactory-path-tests-" + Guid.NewGuid().ToString("N")));
}
