namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// What <see cref="IGitWorktreeService.CreateWorktreeAsync"/> creates: a Workspace Worktree of a Local Repository, on a
/// new branch.
/// </summary>
public sealed record CreateWorktreeRequest
{
    /// <summary>
    /// The Local Repository's working directory.
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// The folder to create the worktree in, e.g. <c>&lt;Workspace Folder&gt;/api</c>. It must not exist, or be empty.
    /// </summary>
    public required string WorktreePath { get; init; }

    /// <summary>
    /// The branch to check out. It's created unless <see cref="UseExistingBranch"/> is set, and then it must not exist
    /// yet.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Whether <see cref="BranchName"/> already exists, so it's checked out rather than created. A branch that exists
    /// only on <see cref="Remote"/> gets a local branch that tracks it.
    /// </summary>
    public bool UseExistingBranch { get; init; }

    /// <summary>
    /// The commit-ish to branch from (e.g. <c>origin/develop</c>), or <see langword="null"/> for
    /// <see cref="Remote"/>'s default branch.
    /// </summary>
    public string? BaseBranch { get; init; }

    /// <summary>
    /// The remote to fetch, and whose default branch is the base when <see cref="BaseBranch"/> isn't set.
    /// </summary>
    public string Remote { get; init; } = GitWorktreeService.DefaultRemote;

    /// <summary>
    /// Whether to fetch <see cref="Remote"/> before creating the branch, so it starts from the remote's latest commit.
    /// </summary>
    public bool Fetch { get; init; } = true;

    /// <summary>
    /// Whether to check out the worktree's submodules (<c>git submodule update --init --recursive</c>).
    /// </summary>
    public bool InitSubmodules { get; init; } = true;
}
