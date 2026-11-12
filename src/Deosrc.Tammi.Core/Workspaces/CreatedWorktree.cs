namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A Workspace Worktree that's been started, in case it has to be rolled back.
/// </summary>
/// <param name="RepositoryPath">The Local Repository's working directory.</param>
/// <param name="WorktreePath">The worktree's folder.</param>
/// <param name="BranchName">The worktree's branch.</param>
/// <param name="CreatedBranch">
/// Whether the branch is new, so rolling back deletes it. An existing branch is kept.
/// </param>
internal sealed record CreatedWorktree(string RepositoryPath, string WorktreePath, string BranchName, bool CreatedBranch);
