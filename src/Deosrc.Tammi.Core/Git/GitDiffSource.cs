namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Which two states of a repository a file diff compares.
/// </summary>
public enum GitDiffSource
{
    /// <summary>The index and the working directory: a file's unstaged changes. Untracked files show as added.</summary>
    Unstaged,

    /// <summary>HEAD and the index: a file's staged changes.</summary>
    Staged,

    /// <summary>A commit and its first parent (or nothing, for a commit with no parents).</summary>
    Commit,
}
