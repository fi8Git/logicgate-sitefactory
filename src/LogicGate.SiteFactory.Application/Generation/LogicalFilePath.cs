namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Shared lexical guards for logical paths, independent of the operating system and filesystem.</summary>
internal static class LogicalFilePath
{
    internal static void Validate(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        if (path.StartsWith('~')
            || path.Any(static character => char.IsControl(character)
                || "<>:\"\\|?*$%".Contains(character, StringComparison.Ordinal))
            || path.Split('/').Any(static segment =>
                segment.Length == 0 || segment is "." or ".."
                || segment.EndsWith(' ') || segment.EndsWith('.')))
        {
            throw new ArgumentException("A canonical relative logical path is required.", parameterName);
        }
    }
}
