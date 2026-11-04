namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// How the submodule commit recorded in the parent repository has changed.
/// </summary>
[Flags]
public enum SubmoduleCommitChange
{
    /// <summary>The parent repository records the commit the submodule has checked out.</summary>
    None = 0,

    /// <summary>
    /// The index records a different commit from HEAD, or the submodule was added or removed in the index.
    /// </summary>
    Staged = 1,

    /// <summary>
    /// The submodule has a different commit checked out from the one in the index, or its folder was removed.
    /// </summary>
    Unstaged = 2,
}
