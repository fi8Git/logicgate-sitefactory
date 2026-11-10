using System.Text.RegularExpressions;

namespace LogicGate.SiteFactory.Modules.Documentation.Model;

/// <summary>Small V1 outline grammar, not a Markdown renderer or a CommonMark implementation.</summary>
internal static partial class MarkdownOutline
{
    internal static (string? Title, List<string> Links)? Read(string markdown, CancellationToken cancellationToken)
    {
        string? title = null;
        string? paragraph = null;
        List<string> links = [];
        char fence = '\0';
        int fenceLength = 0;
        bool comment = false;
        using StringReader reader = new(markdown);
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string trimmed = line.TrimStart(' ');
            int indent = line.Length - trimmed.Length;
            if (fence == '\0' && (comment || trimmed.StartsWith("<!--", StringComparison.Ordinal)))
            {
                comment = !trimmed.Contains("-->", StringComparison.Ordinal);
                paragraph = null;
                continue;
            }
            int run = 0;
            if (trimmed.Length > 0 && trimmed[0] is '`' or '~')
                while (run < trimmed.Length && trimmed[run] == trimmed[0]) run++;
            if (fence != '\0')
            {
                if (indent <= 3 && run >= fenceLength && trimmed[0] == fence && string.IsNullOrWhiteSpace(trimmed[run..])) fence = '\0';
                continue;
            }
            if (indent <= 3 && run >= 3 && (trimmed[0] == '~' || !trimmed[run..].Contains('`', StringComparison.Ordinal)))
            {
                fence = trimmed[0];
                fenceLength = run;
                paragraph = null;
                continue;
            }
            if (indent > 3 || trimmed.StartsWith('\t')) { paragraph = null; continue; }

            Match heading = AtxHeading().Match(line);
            string? candidate = heading.Success ? ClosingHashes().Replace(heading.Groups[1].Value, "").Trim()
                : paragraph is not null && SetextUnderline().IsMatch(line) ? paragraph.Trim() : null;
            if (heading.Success && candidate is not null && candidate.All(static character => character == '#')) candidate = null;
            if (title is null && !string.IsNullOrWhiteSpace(candidate)) title = BoundTitle(candidate);
            paragraph = !string.IsNullOrWhiteSpace(line) && !heading.Success && !SetextUnderline().IsMatch(line)
                && !trimmed.StartsWith('>') && !trimmed.StartsWith('-') && !trimmed.StartsWith('*') ? line : null;

            string visible = MaskInlineCode(line);
            foreach (Match match in InlineLink().Matches(visible))
            {
                if (match.Groups["image"].Length != 0 || IsEscaped(visible, match.Index)) continue;
                string target = match.Groups["target"].Value;
                // Schemes (including mailto/javascript), network URLs and non-Markdown assets are not internal pages.
                if (target.StartsWith("//", StringComparison.Ordinal) || Scheme().IsMatch(target)) continue;
                string path = target.Split('#')[0];
                if (path.Length != 0 && !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                if (links.Count == DocumentationModelBuilder.MaximumLinksPerPage) return null;
                links.Add(target);
            }
        }
        return (title, links);
    }

    private static string MaskInlineCode(string line)
    {
        char[] visible = line.ToCharArray();
        int opening = -1;
        int openingLength = 0;
        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] != '`' || (opening < 0 && IsEscaped(line, index))) continue;
            int length = 1;
            while (index + length < line.Length && line[index + length] == '`') length++;
            if (opening < 0)
            {
                opening = index;
                openingLength = length;
            }
            else if (length == openingLength)
            {
                Array.Fill(visible, ' ', opening, index + length - opening);
                opening = -1;
            }
            index += length - 1;
        }
        return new(visible);
    }

    private static bool IsEscaped(string value, int index)
    {
        int count = 0;
        while (index > 0 && value[--index] == '\\') count++;
        return count % 2 != 0;
    }

    internal static string BoundTitle(string title)
    {
        int length = Math.Min(title.Length, 200);
        if (length < title.Length && char.IsHighSurrogate(title[length - 1])) length--;
        return title[..length];
    }

    [GeneratedRegex(@"^ {0,3}#{1,6}(?:[ \t]+(.*)|[ \t]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex AtxHeading();
    [GeneratedRegex(@"[ \t]+#+[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ClosingHashes();
    [GeneratedRegex(@"^ {0,3}(?:=+|-+)[ \t]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SetextUnderline();
    [GeneratedRegex("(?<image>!?)\\[[^\\]\\r\\n]*\\]\\([ \\t]*(?:<(?<target>[^<>\\r\\n]*)>|(?<target>[^\\s()]+))(?:[ \\t]+[\"'][^\"'\\r\\n]*[\"'])?[ \\t]*\\)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex InlineLink();
    [GeneratedRegex(@"\A[a-zA-Z][a-zA-Z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex Scheme();
}
