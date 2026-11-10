using System.Text;
using LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>
/// Resolves canonical logical file paths strictly below an explicit physical root, without creating anything.
/// Confinement is lexical only: symlinks, junctions and filesystem races must be handled before any future write.
/// </summary>
public sealed class GenerationRoot
{
    // A portable policy, independent of the current volume's potentially more permissive limits.
    // Unix names count UTF-8 bytes; Windows names count UTF-16 code units. No truncation or normalization.
    private const int MaximumComponentLength = 255;
    private const int MaximumPathUtf8Bytes = 4095; // Linux PATH_MAX includes the trailing NUL.
    private const int MaximumPathUtf16Units = 32766;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string directoryPrefix;

    public GenerationRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Path.IsPathFullyQualified(rootPath)
            || (rootPath.Length >= 2 && rootPath[0] is '/' or '\\' && rootPath[1] is '/' or '\\'))
        {
            throw new ArgumentException("An absolute local generation root is required.", nameof(rootPath));
        }

        // Strip only the native volume prefix and one optional final separator, not arbitrary path segments.
        string volume = Path.GetPathRoot(rootPath)!;
        string relativeRoot = Path.TrimEndingDirectorySeparator(rootPath)[volume.Length..]
            .Replace(Path.DirectorySeparatorChar, '/');
        ValidateLogicalPath(relativeRoot, nameof(rootPath));

        try
        {
            FullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The generation root is not a supported physical path.", nameof(rootPath));
        }

        ValidatePhysicalLength(FullPath, nameof(rootPath));

        if (File.Exists(FullPath))
        {
            throw new ArgumentException("The generation root cannot be an existing file.", nameof(rootPath));
        }

        directoryPrefix = FullPath + Path.DirectorySeparatorChar;
    }

    /// <summary>Canonical absolute root, without a trailing separator. Existence and write access are not required.</summary>
    public string FullPath { get; }

    /// <summary>
    /// Revalidates the raw logical path independently of the plan and returns an absolute, lexically confined path.
    /// This is not a permission to open or write the returned path.
    /// </summary>
    public string Resolve(string logicalPath)
    {
        ValidateLogicalPath(logicalPath, nameof(logicalPath));

        string resolvedPath;
        try
        {
            // An explicit base prevents resolution against a mutable process working directory.
            resolvedPath = Path.GetFullPath(logicalPath.Replace('/', Path.DirectorySeparatorChar), FullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The logical path cannot be resolved to a supported physical path.", nameof(logicalPath));
        }

        if (!ContainsCanonicalPath(resolvedPath))
        {
            throw new ArgumentException("The resolved file must remain strictly below the generation root.", nameof(logicalPath));
        }

        ValidatePhysicalLength(resolvedPath, nameof(logicalPath));
        // The atomic writer also needs a sibling temporary file, which may have a longer name.
        string parentPrefix = resolvedPath[..(resolvedPath.LastIndexOf(Path.DirectorySeparatorChar) + 1)];
        ValidatePhysicalLength(parentPrefix, nameof(logicalPath), AtomicWriteSession.TemporaryNameLength);

        return resolvedPath;
    }

    // The input must already be canonical. Keep a directory boundary: root-other is not a child of root.
    internal bool ContainsCanonicalPath(string canonicalPath) =>
        canonicalPath.Length > directoryPrefix.Length
        && canonicalPath.StartsWith(directoryPrefix, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);

    private static void ValidateLogicalPath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        // Deliberate boundary checks, not reliance on the caller having constructed a valid PlannedFile.
        // Windows syntax is rejected on Linux too, before any native normalization can erase evidence.
        if (path.StartsWith('~')
            || path.Any(static character => char.IsControl(character) || "<>:\"\\|?*$%".Contains(character, StringComparison.Ordinal))
            || path.Split('/').Any(static segment => segment.Length == 0 || segment is "." or ".."
                || segment.EndsWith(' ') || segment.EndsWith('.')
                || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || IsReservedWindowsName(segment)))
        {
            throw new ArgumentException("A canonical, portable and unreserved relative path is required.", parameterName);
        }

        foreach (string segment in path.Split('/'))
        {
            if (segment.Length > MaximumComponentLength || Utf8Length(segment, parameterName) > MaximumComponentLength)
                throw new ArgumentException("A path component exceeds the portable physical name limit.", parameterName);
        }
    }

    private static void ValidatePhysicalLength(string path, string parameterName, int suffixLength = 0)
    {
        if (path.Length > MaximumPathUtf16Units - suffixLength
            || Utf8Length(path, parameterName) > MaximumPathUtf8Bytes - suffixLength)
            throw new ArgumentException("The physical path exceeds the portable path limit.", parameterName);
    }

    private static int Utf8Length(string value, string parameterName)
    {
        try
        {
            return StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("A path must contain well-formed Unicode.", parameterName);
        }
    }

    private static bool IsReservedWindowsName(string segment)
    {
        int extension = segment.IndexOf('.', StringComparison.Ordinal);
        string name = (extension < 0 ? segment : segment[..extension]).TrimEnd(' ');
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (name.Length == 4
                && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && (name[3] is >= '1' and <= '9' or '¹' or '²' or '³'));
    }
}
