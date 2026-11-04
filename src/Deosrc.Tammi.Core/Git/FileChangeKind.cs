namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// How a file changed between two states (HEAD, index or working directory).
/// </summary>
public enum FileChangeKind
{
    /// <summary>No change.</summary>
    None,

    /// <summary>A new file. In the index: staged for the first time.</summary>
    Added,

    /// <summary>The file's content changed.</summary>
    Modified,

    /// <summary>The file was deleted.</summary>
    Deleted,

    /// <summary>The file was renamed.</summary>
    Renamed,

    /// <summary>The file's type changed, e.g. from a regular file to a symlink.</summary>
    TypeChanged,

    /// <summary>A file in the working directory that git doesn't track. Only used for unstaged changes.</summary>
    Untracked,

    /// <summary>The file has unresolved merge conflicts. Used for both staged and unstaged changes.</summary>
    Conflicted,
}
