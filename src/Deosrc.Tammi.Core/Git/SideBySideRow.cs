namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// One row of a side-by-side diff: the old line on the left and the new line on the right.
/// </summary>
/// <param name="Left">The old side's line, or <see langword="null"/> for a blank.</param>
/// <param name="Right">The new side's line, or <see langword="null"/> for a blank.</param>
public sealed record SideBySideRow(DiffLine? Left, DiffLine? Right)
{
    /// <summary>
    /// Whether the row is a note (such as "No newline at end of file"), which spans both sides.
    /// </summary>
    public bool IsNote => Left?.Kind == DiffLineKind.Note || Right?.Kind == DiffLineKind.Note;
}
