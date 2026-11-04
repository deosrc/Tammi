namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Whether a Workspace Worktree is still connected to its Local Repository.
/// </summary>
public enum WorktreeHealth
{
    /// <summary>
    /// The worktree and its Local Repository point at each other.
    /// </summary>
    Healthy,

    /// <summary>
    /// The worktree folder doesn't exist.
    /// </summary>
    Missing,

    /// <summary>
    /// The folder exists but isn't a git worktree: it has no <c>.git</c> file, or it's a full clone.
    /// </summary>
    NotAWorktree,

    /// <summary>
    /// The Local Repository the worktree points at was moved or deleted. Repair it by pointing it at the
    /// repository's new location, or clone the repository again.
    /// </summary>
    RepositoryMissing,

    /// <summary>
    /// The Local Repository exists but no longer has a record of the worktree, e.g. after <c>git worktree prune</c>.
    /// </summary>
    Unregistered,

    /// <summary>
    /// The worktree folder was moved: the Local Repository still records its old location. Repairing fixes it.
    /// </summary>
    Moved,
}
