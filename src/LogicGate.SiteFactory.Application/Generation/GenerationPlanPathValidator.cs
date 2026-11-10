using LogicGate.SiteFactory.Application.Configuration;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Portable target-name and collision checks, without resolving or inspecting a physical root.</summary>
public static class GenerationPlanPathValidator
{
    /// <summary>Validates canonical paths already guarded by PlannedFile, regardless of origin, strategy or action.</summary>
    public static IReadOnlyList<GenerationPlanDiagnostic> Validate(IEnumerable<PlannedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        PlannedFile[] snapshot = files.ToArray();
        if (snapshot.Any(static file => file is null))
        {
            throw new ArgumentException("Planned files cannot contain null entries.", nameof(files));
        }

        return ValidatePaths(snapshot.Select(static file => file.RelativePath));
    }

    // Only called with paths guarded by LogicalFilePath; no normalization or raw-path ingestion.
    internal static IReadOnlyList<GenerationPlanDiagnostic> ValidatePaths(IEnumerable<string> paths)
    {
        string[] targets = paths.Order(StringComparer.Ordinal).ToArray();
        HashSet<GenerationPlanDiagnostic> diagnostics = [];
        HashSet<string> directories = new(StringComparer.Ordinal);

        foreach (IGrouping<string, string> group in targets.GroupBy(static path => path, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                diagnostics.Add(Error("SF-GEN-002", "Plusieurs fichiers planifiés désignent la même cible logique.", group.Key));
            }
        }

        foreach (string target in targets)
        {
            string[] segments = target.Split('/');
            if (segments.Any(static segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(Error("SF-GEN-FILE-026", "Un segment du chemin cible est réservé aux données internes Git.", target));
            }

            if (segments.Any(IsReservedWindowsName))
            {
                diagnostics.Add(Error("SF-GEN-FILE-025", "Un segment du chemin cible utilise un nom réservé sous Windows.", target));
            }

            for (int separator = target.IndexOf('/', StringComparison.Ordinal); separator >= 0;
                separator = target.IndexOf('/', separator + 1))
            {
                directories.Add(target[..separator]);
            }
        }

        HashSet<string> portableTargets = new(targets, StringComparer.OrdinalIgnoreCase);
        foreach (string directory in directories.Order(StringComparer.Ordinal))
        {
            if (portableTargets.TryGetValue(directory, out string? fileTarget))
            {
                diagnostics.Add(Error("SF-GEN-002", "Une cible de fichier est également requise comme répertoire.", fileTarget));
            }
        }

        // Include implicit directories: Docs/a.md and docs/b.md must describe the same tree on both platforms.
        foreach (IGrouping<string, string> group in targets.Concat(directories)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .GroupBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                diagnostics.Add(Error("SF-GEN-FILE-012", "Des chemins logiques ne se distinguent que par la casse.", group.Key));
            }
        }

        return Array.AsReadOnly(diagnostics.OrderBy(static diagnostic => diagnostic.PropertyPath, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Code.Value, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal).ToArray());
    }

    private static GenerationPlanDiagnostic Error(string code, string message, string path) =>
        new(new(code), DiagnosticSeverity.Error, message, path);

    private static bool IsReservedWindowsName(string segment)
    {
        // Extensions do not make DOS device names ordinary files. Keep these rules identical on every OS.
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
