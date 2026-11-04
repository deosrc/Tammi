using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A branch of a deleted workspace, offered for deletion from its Local Repository.
/// </summary>
/// <param name="LocalRepositoryPath">The Local Repository the branch is in.</param>
/// <param name="Branch">The branch's name.</param>
/// <param name="State">Whether the branch is merged or pushed.</param>
/// <param name="IsWorkspaceBranch">
/// Whether the workspace's metadata lists the branch, so Tammi created it for the workspace. Branches of worktrees
/// found only by looking in the Workspace Folder could be used elsewhere.
/// </param>
public sealed record WorkspaceBranch(
    string LocalRepositoryPath,
    string Branch,
    BranchState State,
    bool IsWorkspaceBranch)
{
    /// <summary>
    /// Whether the branch is selected for deletion by default: it's the workspace's own branch, and deleting it loses
    /// no work because it's merged or pushed.
    /// </summary>
    public bool SelectedByDefault => IsWorkspaceBranch && State.IsSafeToDelete;
}
