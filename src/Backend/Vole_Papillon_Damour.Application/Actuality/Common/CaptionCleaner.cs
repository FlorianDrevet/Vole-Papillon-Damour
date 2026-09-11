using System.Text.RegularExpressions;

namespace Vole_Papillon_Damour.Application.Actuality.Common;

public static partial class CaptionCleaner
{
    [GeneratedRegex(@"^#[\p{L}\p{N}_]+(?:\s+#[\p{L}\p{N}_]+)*\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HashtagOnlyLineRegex();

    [GeneratedRegex(@"(?:\s+#[\p{L}\p{N}_]+)+\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TerminalHashtagsRegex();

    public static string Clean(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return string.Empty;
        }

        var lines = caption
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var cleanedLines = new List<string>(lines.Length);
        var previousWasBlank = false;

        foreach (var sourceLine in lines)
        {
            var line = sourceLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || HashtagOnlyLineRegex().IsMatch(line.Trim()))
            {
                if (!previousWasBlank && cleanedLines.Count > 0)
                {
                    cleanedLines.Add(string.Empty);
                }

                previousWasBlank = true;
                continue;
            }

            line = TerminalHashtagsRegex().Replace(line, string.Empty).TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            cleanedLines.Add(line);
            previousWasBlank = false;
        }

        while (cleanedLines.Count > 0 && cleanedLines[^1].Length == 0)
        {
            cleanedLines.RemoveAt(cleanedLines.Count - 1);
        }

        return string.Join('\n', cleanedLines);
    }
}
