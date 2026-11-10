namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A line of a <see cref="DiffHunk"/>.
/// </summary>
/// <param name="Kind">What the line is.</param>
/// <param name="Text">The line's text, without the diff prefix and line ending.</param>
/// <param name="OldNumber">The line's number on the old side, or <see langword="null"/> if it isn't there.</param>
/// <param name="NewNumber">The line's number on the new side, or <see langword="null"/> if it isn't there.</param>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldNumber, int? NewNumber);
