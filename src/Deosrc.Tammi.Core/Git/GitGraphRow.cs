namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A commit's row in the git graph, from <see cref="GitGraphLayout"/>.
/// </summary>
/// <param name="Commit">The commit.</param>
/// <param name="Lane">The lane the commit's dot is in, counting from 0 at the left.</param>
/// <param name="Colour">The dot's colour, as an index into the graph's palette (wrap it to the palette's size).</param>
/// <param name="Lines">The lines drawn in the row, besides the dot.</param>
/// <param name="LaneCount">How many lanes the row uses: one more than the rightmost lane it draws in.</param>
public sealed record GitGraphRow(
    CommitInfo Commit,
    int Lane,
    int Colour,
    IReadOnlyList<GitGraphLine> Lines,
    int LaneCount);
