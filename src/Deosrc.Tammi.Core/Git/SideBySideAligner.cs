namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Lays a <see cref="DiffHunk"/> out in two columns.
/// </summary>
public static class SideBySideAligner
{
    /// <summary>
    /// Aligns a hunk's lines. Context lines are on both sides. Each run of removed lines is shown beside the run of
    /// added lines that follows it, line by line, and a side that has fewer lines has blanks.
    /// </summary>
    public static IReadOnlyList<SideBySideRow> Align(DiffHunk hunk)
    {
        ArgumentNullException.ThrowIfNull(hunk);
        IReadOnlyList<DiffLine> lines = hunk.Lines;
        var rows = new List<SideBySideRow>(lines.Count);
        int i = 0;
        while (i < lines.Count)
        {
            DiffLine line = lines[i];
            if (line.Kind == DiffLineKind.Context)
            {
                rows.Add(new SideBySideRow(line, line));
                i++;
                continue;
            }

            if (line.Kind == DiffLineKind.Note)
            {
                rows.Add(new SideBySideRow(line, null));
                i++;
                continue;
            }

            int removedStart = i;
            while (i < lines.Count && lines[i].Kind == DiffLineKind.Removed)
            {
                i++;
            }

            int removedEnd = i;
            // A note after the removed lines ("No newline at end of file") belongs to the last of them.
            int addedStart = i;
            if (i < lines.Count && lines[i].Kind == DiffLineKind.Note && removedEnd > removedStart)
            {
                i++;
                addedStart = i;
            }

            while (i < lines.Count && lines[i].Kind == DiffLineKind.Added)
            {
                i++;
            }

            int removedCount = removedEnd - removedStart;
            int addedCount = i - addedStart;
            for (int k = 0; k < Math.Max(removedCount, addedCount); k++)
            {
                rows.Add(new SideBySideRow(
                    k < removedCount ? lines[removedStart + k] : null,
                    k < addedCount ? lines[addedStart + k] : null));
            }

            if (addedStart > removedEnd)
            {
                // The skipped note.
                rows.Add(new SideBySideRow(lines[removedEnd], null));
            }
        }

        return rows;
    }
}
