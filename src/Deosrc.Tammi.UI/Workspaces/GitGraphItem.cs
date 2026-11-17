using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// One row of the git graph list: a commit, or a stash among the commits by date.
/// </summary>
/// <param name="Row">The commit's row, or <see langword="null"/> for a stash.</param>
/// <param name="Stash">The stash, or <see langword="null"/> for a commit.</param>
/// <param name="Lane">
/// For a stash, the lane its box is in: its base commit's lane when the stash is above the end of a branch, or else a
/// lane to the right of the graph.
/// </param>
/// <param name="HasLine">For a stash, whether a dotted line runs down from it to its base commit.</param>
/// <param name="PassLanes">The lanes of stash lines that run through the whole row.</param>
/// <param name="EndLines">The stash lines that end at this commit, drawn in the top half of the row.</param>
/// <param name="CommitLanes">For a stash, the commit lines that run through its row, so the graph isn't broken.</param>
/// <param name="IsSide">Whether the stash's box is in the lane to the right of the graph.</param>
public sealed record GitGraphItem(
    GitGraphRow? Row,
    GitStashEntry? Stash,
    int Lane,
    bool HasLine,
    IReadOnlyList<int> PassLanes,
    IReadOnlyList<(int From, int To)> EndLines,
    IReadOnlyList<(int Lane, int Colour)> CommitLanes,
    bool IsSide)
{
    /// <summary>
    /// Puts the stashes among the commits by date, each above the commit it was made on at the latest. A stash that's
    /// older than every commit read so far is left out until the rest of the history is read. Stashes are in date order
    /// with the commits, and never below the commit they were made on.
    /// </summary>
    /// <param name="rows">The commits, newest first.</param>
    /// <param name="stashes">The stashes.</param>
    /// <param name="isComplete">Whether every commit has been read.</param>
    /// <param name="laneCount">How many lanes the commits use. A stash that can't be above its commit goes in the next lane.</param>
    /// <param name="maxLane">The rightmost lane drawn.</param>
    /// <param name="busySha">
    /// The commit that has other changes on top of it (HEAD, while there are working changes), or <see langword="null"/>.
    /// </param>
    public static IReadOnlyList<GitGraphItem> Build(
        IReadOnlyList<GitGraphRow> rows,
        IReadOnlyList<GitStashEntry> stashes,
        bool isComplete,
        int laneCount,
        int maxLane,
        string? busySha = null)
    {
        if (stashes.Count == 0)
        {
            return [.. rows.Select(row => new GitGraphItem(row, null, 0, false, [], [], [], false))];
        }

        var indexOf = new Dictionary<string, int>();
        for (int i = 0; i < rows.Count; i++)
        {
            indexOf.TryAdd(rows[i].Commit.Sha, i);
        }

        // Each stash's position (the row it goes above) and the row of its base commit, or -1.
        var placed = new List<(GitStashEntry Stash, int Position, int Base, bool Tip)>();
        foreach (GitStashEntry stash in stashes)
        {
            string baseSha = stash.AnchorSha ?? stash.BaseSha;
            int baseIndex = indexOf.GetValueOrDefault(baseSha, -1);
            if (baseIndex >= 0 && IsTip(rows[baseIndex], busySha))
            {
                // At the end of a branch, so the stash goes straight above its commit, in its lane.
                placed.Add((stash, baseIndex, baseIndex, true));
                continue;
            }

            int position = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Commit.Committer.When <= stash.CreatedAt)
                {
                    position = i;
                    break;
                }
            }

            if (position < 0)
            {
                position = isComplete ? rows.Count : baseIndex;
            }

            if (position < 0)
            {
                continue;
            }

            if (baseIndex >= 0)
            {
                // Never below the commit it was made on, whatever the clocks say.
                position = Math.Min(position, baseIndex);
            }

            placed.Add((stash, position, baseIndex, false));
        }

        int sideLane = Math.Min(laneCount, maxLane);
        var items = new List<GitGraphItem>();
        var active = new List<(int Lane, int Base)>();
        for (int i = 0; i <= rows.Count; i++)
        {
            // The commit lines that carry on through the gap above this row.
            IReadOnlyList<(int Lane, int Colour)> carried = i == 0
                ? []
                : [.. rows[i - 1].Lines
                    .Where(line => line.Half == GitGraphLineHalf.Bottom)
                    .Select(line => (line.ToLane, line.Colour))
                    .Distinct()];
            foreach (var (stash, _, baseIndex, tip) in placed.Where(p => p.Position == i).OrderByDescending(p => p.Stash.CreatedAt))
            {
                int lane = baseIndex < 0 ? 0 : tip ? Math.Min(rows[baseIndex].Lane, maxLane) : sideLane;
                items.Add(new GitGraphItem(null, stash, lane, baseIndex >= 0, [.. active.Select(a => a.Lane)], [], carried, !tip && baseIndex >= 0));
                if (baseIndex >= 0)
                {
                    active.Add((lane, baseIndex));
                }
            }

            if (i == rows.Count)
            {
                break;
            }

            items.Add(new GitGraphItem(
                rows[i],
                null,
                0,
                false,
                [.. active.Where(a => a.Base > i).Select(a => a.Lane)],
                [.. active.Where(a => a.Base == i).Select(a => (a.Lane, Math.Min(rows[i].Lane, maxLane)))],
                [],
                false));
            active.RemoveAll(a => a.Base <= i);
        }

        return items;
    }

    // Whether nothing is above the commit on its lane: no line comes down into its dot, and no other changes are on it.
    private static bool IsTip(GitGraphRow row, string? busySha) =>
        row.Commit.Sha != busySha
        && !row.Lines.Any(line => line.Half == GitGraphLineHalf.Top && line.ToLane == row.Lane);
}
