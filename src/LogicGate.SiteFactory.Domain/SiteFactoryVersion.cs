using System.Text.RegularExpressions;

namespace LogicGate.SiteFactory.Domain;

/// <summary>An exact generator SemVer, including explicit prerelease/build metadata; never a range or a local path.</summary>
public sealed partial record SiteFactoryVersion
{
    public SiteFactoryVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!ExactVersion().IsMatch(value))
        {
            throw new ArgumentException("An exact SiteFactory semantic version is required.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    // SemVer 2.0.0 syntax only (https://semver.org/spec/v2.0.0.html), no ordering, ranges or version resolution.
    // ASCII digits and absolute anchors also reject Unicode numerals and trailing line breaks.
    [GeneratedRegex(@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExactVersion();
}
