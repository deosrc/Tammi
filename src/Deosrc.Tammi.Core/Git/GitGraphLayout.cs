namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Lays out the git graph: which lane each commit is drawn in, and the lines that join commits to their parents.
/// </summary>
/// <remarks>
/// <para>
/// Commits are given in topological order (children before parents), a page at a time: each page carries on from where
/// the last one ended, so the result is the same however the history is split.
/// </para>
/// <para>
/// Each lane waits for a commit. A commit takes the leftmost lane waiting for it, and the other lanes waiting for it
/// join its dot and end. A commit nobody waits for (a branch tip) takes the leftmost free lane, in a new colour. The
/// commit's lane then waits for its first parent. Each other parent joins a lane already waiting for it, or takes the
/// leftmost free lane in a new colour. Lanes don't move, and lanes that end are free to reuse, so the graph stays
/// narrow. Lanes waiting for commits that haven't been given yet run off the bottom of the last row.
/// </para>
/// </remarks>
public sealed class GitGraphLayout
{
    private readonly List<Lane?> _lanes = [];
    private int _nextColour;

    /// <summary>
    /// Lays out the next commits.
    /// </summary>
    /// <param name="commits">The commits after the ones already laid out, in topological order.</param>
    /// <returns>A row for each commit, in the same order.</returns>
    public IReadOnlyList<GitGraphRow> Append(IEnumerable<CommitInfo> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        return commits.Select(Append).ToArray();
    }

    private GitGraphRow Append(CommitInfo commit)
    {
        var lines = new List<GitGraphLine>();

        // The top half: the lanes waiting for this commit join its dot, and the others pass by.
        int lane = -1;
        for (int i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i] is { } waiting && waiting.Sha == commit.Sha)
            {
                lane = lane < 0 ? i : lane;
            }
        }

        int colour;
        if (lane < 0)
        {
            lane = FreeLane();
            colour = _nextColour++;
        }
        else
        {
            colour = _lanes[lane]!.Colour;
        }

        for (int i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i] is { } waiting)
            {
                lines.Add(new GitGraphLine(i, waiting.Sha == commit.Sha ? lane : i, waiting.Colour, GitGraphLineHalf.Top));
                if (waiting.Sha == commit.Sha)
                {
                    _lanes[i] = null;
                }
            }
        }

        // The bottom half: the commit's lane carries on to its first parent, and each other parent joins its lane.
        var fromCommit = new HashSet<int>();
        IReadOnlyList<string> parents = commit.ParentShas.Distinct().ToArray();
        if (parents.Count > 0)
        {
            SetLane(lane, new Lane(parents[0], colour));
            fromCommit.Add(lane);
        }

        foreach (string parent in parents.Skip(1))
        {
            int existing = _lanes.FindIndex(waiting => waiting?.Sha == parent);
            if (existing >= 0)
            {
                lines.Add(new GitGraphLine(lane, existing, _lanes[existing]!.Colour, GitGraphLineHalf.Bottom));
            }
            else
            {
                int free = FreeLane();
                SetLane(free, new Lane(parent, _nextColour++));
                fromCommit.Add(free);
            }
        }

        for (int i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i] is { } waiting)
            {
                lines.Add(new GitGraphLine(fromCommit.Contains(i) ? lane : i, i, waiting.Colour, GitGraphLineHalf.Bottom));
            }
        }

        TrimFreeLanes();
        return new GitGraphRow(commit, lane, colour, lines, LaneCount(lane, lines));
    }

    // One more than the rightmost lane the dot or any line is in.
    private static int LaneCount(int lane, List<GitGraphLine> lines) =>
        lines.Aggregate(lane + 1, (count, line) => Math.Max(count, Math.Max(line.FromLane, line.ToLane) + 1));

    private int FreeLane()
    {
        int free = _lanes.IndexOf(null);
        return free >= 0 ? free : _lanes.Count;
    }

    private void SetLane(int index, Lane lane)
    {
        while (_lanes.Count <= index)
        {
            _lanes.Add(null);
        }

        _lanes[index] = lane;
    }

    private void TrimFreeLanes()
    {
        while (_lanes.Count > 0 && _lanes[^1] is null)
        {
            _lanes.RemoveAt(_lanes.Count - 1);
        }
    }

    private sealed record Lane(string Sha, int Colour);
}
