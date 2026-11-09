namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// What a line in a <see cref="DiffHunk"/> is.
/// </summary>
public enum DiffLineKind
{
    /// <summary>A line both sides have.</summary>
    Context,

    /// <summary>A line only the new side has.</summary>
    Added,

    /// <summary>A line only the old side has.</summary>
    Removed,

    /// <summary>A note such as "No newline at end of file".</summary>
    Note,
}
