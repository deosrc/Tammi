using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The git state of one of a workspace's repositories, as shown in the Workspace View's side panel.
/// </summary>
/// <param name="Repository">The repository in the workspace's metadata.</param>
/// <param name="WorktreePath">The full path of the repository's Workspace Worktree.</param>
public sealed record WorkspaceRepositoryStatus(WorkspaceRepository Repository, string WorktreePath)
{
    /// <summary>
    /// Whether the worktree is still connected to its Local Repository. Until the status is first read, it's assumed
    /// to be healthy.
    /// </summary>
    public WorktreeHealth Health { get; init; } = WorktreeHealth.Healthy;

    /// <summary>
    /// The worktree's git status, or <see langword="null"/> when it hasn't been read yet or couldn't be read.
    /// </summary>
    public RepositoryStatus? Status { get; init; }

    /// <summary>
    /// Why the status couldn't be read, or <see langword="null"/> if it could (or hasn't been read yet). A broken
    /// worktree isn't read, so it has no problem here: see <see cref="Health"/>.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// Whether the status has been read, or found to be unreadable.
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Whether the worktree needs repairing before git can use it.
    /// </summary>
    public bool IsBroken => Health != WorktreeHealth.Healthy;
}
