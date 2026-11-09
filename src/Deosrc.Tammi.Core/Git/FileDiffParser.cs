using System.Globalization;
using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Parses the text of a unified diff of one file into hunks.
/// </summary>
internal static partial class FileDiffParser
{
    /// <summary>
    /// The most lines a diff shows before it's cut short.
    /// </summary>
    public const int MaxLines = 20_000;

    [GeneratedRegex(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@")]
    private static partial Regex HunkHeader();

    public static (IReadOnlyList<DiffHunk> Hunks, bool Truncated) Parse(string patch)
    {
        var hunks = new List<DiffHunk>();
        List<DiffLine>? lines = null;
        string header = "";
        int oldNumber = 0;
        int newNumber = 0;
        int total = 0;
        bool truncated = false;

        void Finish()
        {
            if (lines is not null)
            {
                hunks.Add(new DiffHunk(header, lines));
            }
        }

        foreach (string line in patch.Split('\n'))
        {
            string text = line.TrimEnd('\r');
            Match match = HunkHeader().Match(text);
            if (match.Success)
            {
                Finish();
                lines = [];
                header = text;
                oldNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                newNumber = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                continue;
            }

            if (lines is null || text.Length == 0)
            {
                continue;
            }

            if (++total > MaxLines)
            {
                truncated = true;
                break;
            }

            switch (text[0])
            {
                case '+':
                    lines.Add(new DiffLine(DiffLineKind.Added, text[1..], null, newNumber++));
                    break;
                case '-':
                    lines.Add(new DiffLine(DiffLineKind.Removed, text[1..], oldNumber++, null));
                    break;
                case '\\':
                    lines.Add(new DiffLine(DiffLineKind.Note, text[1..].TrimStart(), null, null));
                    break;
                default:
                    lines.Add(new DiffLine(DiffLineKind.Context, text.Length > 1 ? text[1..] : "", oldNumber++, newNumber++));
                    break;
            }
        }

        Finish();
        return (hunks, truncated);
    }
}
