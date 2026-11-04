namespace TAMMi.Core.Git;

/// <summary>
/// Creates, checks, repairs and removes Workspace Worktrees, and manages their branches in the Local Repository. Writes
/// go through the <c>git</c> CLI; status reads go through <see cref="IGitRepositoryReader"/>.
/// </summary>
public interface IGitWorktreeService
{
    /// <summary>
    /// Fetches <paramref name="remote"/> in the repository.
    /// </summary>
    /// <exception cref="GitCommandException">The fetch failed, e.g. the remote doesn't exist or can't be reached.</exception>
    Task FetchAsync(string repositoryPath, string remote = GitWorktreeService.DefaultRemote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the remote's default branch as a remote-tracking branch name, e.g. <c>origin/main</c>. Uses the
    /// repository's <c>refs/remotes/&lt;remote&gt;/HEAD</c> when it's set (as it is after a clone), and otherwise asks
    /// the remote.
    /// </summary>
    /// <exception cref="GitException">The default branch can't be found.</exception>
    Task<string> GetDefaultBranchAsync(string repositoryPath, string remote = GitWorktreeService.DefaultRemote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the branches a new branch can start from: the remote-tracking branches (e.g. <c>origin/main</c>), then the
    /// local branches, each in name order. Symbolic references such as <c>origin/HEAD</c> are left out.
    /// </summary>
    Task<IReadOnlyList<string>> ListBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="commitish"/> (e.g. <c>origin/main</c>) names a commit in the repository.
    /// </summary>
    Task<bool> CommitExistsAsync(string repositoryPath, string commitish, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="branchName"/> is a valid name for a new branch.
    /// </summary>
    Task<bool> IsValidBranchNameAsync(string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches, creates a new branch from the base and adds a worktree for it (<c>git worktree add</c>), then checks
    /// out its submodules. The new branch doesn't track the base, so pushing it publishes it under its own name.
    /// </summary>
    /// <returns>The new worktree's full path.</returns>
    /// <exception cref="GitCommandException">
    /// A git step failed, e.g. the branch already exists or the folder isn't empty.
    /// </exception>
    Task<string> CreateWorktreeAsync(CreateWorktreeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks out a worktree's submodules, recursively (<c>git submodule update --init --recursive</c>).
    /// </summary>
    Task InitSubmodulesAsync(string worktreePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a worktree from its Local Repository and deletes its folder (<c>git worktree remove</c>). Its branch is
    /// kept. A worktree whose folder is already gone is just unregistered.
    /// </summary>
    /// <param name="repositoryPath">The Local Repository's working directory.</param>
    /// <param name="worktreePath">The worktree folder.</param>
    /// <param name="force">
    /// Remove the worktree even if it has uncommitted changes or unpushed commits (in it or its submodules). Use it
    /// once the user has confirmed.
    /// </param>
    /// <exception cref="WorktreeChangedException">
    /// The worktree holds work that would be lost, and <paramref name="force"/> isn't set.
    /// </exception>
    Task RemoveWorktreeAsync(string repositoryPath, string worktreePath, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a branch exists, is merged into <paramref name="mergeTarget"/> and is pushed.
    /// </summary>
    /// <param name="repositoryPath">The Local Repository's working directory.</param>
    /// <param name="branchName">The local branch's name.</param>
    /// <param name="mergeTarget">
    /// What the branch counts as merged into (e.g. the workspace's base branch), or <see langword="null"/> for the
    /// <c>origin</c> remote's default branch.
    /// </param>
    Task<BranchState> GetBranchStateAsync(string repositoryPath, string branchName, string? mergeTarget = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a local branch, whether or not it's merged. Check <see cref="GetBranchStateAsync"/> first.
    /// </summary>
    /// <exception cref="GitCommandException">
    /// The branch doesn't exist or is checked out in a worktree.
    /// </exception>
    Task DeleteBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a worktree is still connected to its Local Repository, e.g. to detect a repository that was moved
    /// or deleted. Reads files only, so it works when git can't open the worktree.
    /// </summary>
    WorktreeCheck CheckWorktree(string worktreePath);

    /// <summary>
    /// Reconnects a worktree with its Local Repository (<c>git worktree repair</c>), and the worktree's submodules with
    /// their git directories.
    /// </summary>
    /// <param name="worktreePath">The worktree folder.</param>
    /// <param name="repositoryPath">
    /// The Local Repository's new location, when it was moved. <see langword="null"/> when only the worktree moved.
    /// </param>
    /// <returns>The worktree's health after repairing.</returns>
    /// <exception cref="GitException">The worktree can't be repaired, e.g. it's not from that repository.</exception>
    Task<WorktreeCheck> RepairWorktreeAsync(string worktreePath, string? repositoryPath = null, CancellationToken cancellationToken = default);
}
