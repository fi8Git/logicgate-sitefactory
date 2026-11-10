using System.Text.RegularExpressions;

namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>An exact SemVer 2.0 version, not a range or a version precedence comparer.</summary>
public sealed partial record ModuleVersion
{
    public ModuleVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!ExactVersion().IsMatch(value))
            throw new ArgumentException("An exact SemVer 2.0 version is required.", nameof(value));

        Value = value;
    }

    /// <summary>Exact identity, including any prerelease and build metadata; equality is ordinal.</summary>
    public string Value { get; }

    public override string ToString() => Value;

    // ASCII digits only; numeric prerelease identifiers cannot have leading zeroes.
    // NonBacktracking keeps validation linear without imposing an integer-size limit on SemVer numbers.
    private const string Number = "(?:0|[1-9][0-9]*)";
    private const string PrereleaseIdentifier = "(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)";

    [GeneratedRegex(@"\A" + Number + @"\." + Number + @"\." + Number
        + "(?:-" + PrereleaseIdentifier + @"(?:\." + PrereleaseIdentifier + ")*)?"
        + @"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExactVersion();
}
