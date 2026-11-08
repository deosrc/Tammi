namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A line in half of a <see cref="GitGraphRow"/>: a lane passing the commit by, a lane joining the commit's dot, or
/// the commit's dot leading to a parent's lane.
/// </summary>
/// <param name="FromLane">The lane the line starts in: at the top edge for the top half, at the middle for the bottom.</param>
/// <param name="ToLane">The lane the line ends in: at the middle for the top half, at the bottom edge for the bottom.</param>
/// <param name="Colour">The line's colour, as an index into the graph's palette (wrap it to the palette's size).</param>
/// <param name="Half">Which half of the row the line is in.</param>
public sealed record GitGraphLine(int FromLane, int ToLane, int Colour, GitGraphLineHalf Half);
