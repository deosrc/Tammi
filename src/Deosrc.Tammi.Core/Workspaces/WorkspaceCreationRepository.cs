namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A repository for <see cref="WorkspaceCreator"/> to add to a new workspace, as a Workspace Worktree.
/// </summary>
/// <param name="FolderName">The worktree's folder in the Workspace Folder. It is a single folder name, not a path.</param>
/// <param name="LocalRepositoryPath">The Local Repository's full path.</param>
/// <param name="MergeTarget">
/// What a new branch starts from, e.g. <c>origin/main</c>. For an existing branch it's what the branch is compared to
/// to tell when it's merged.
/// </param>
/// <param name="Remote">
/// The remote to fetch before creating the branch, or <see langword="null"/> if the repository has none.
/// </param>
public sealed record WorkspaceCreationRepository(
    string FolderName,
    string LocalRepositoryPath,
    string MergeTarget,
    string? Remote,
    string? BranchName = null)
{
    /// <summary>
    /// The repository's branch, or <see langword="null"/> to use the workspace's branch name. A branch that already
    /// exists (a local branch, or one on <see cref="Remote"/>) is checked out as it is, and any other is created.
    /// </summary>
    public string? BranchName { get; init; } = BranchName;

    /// <summary>
    /// The branch to use: <see cref="BranchName"/> if it's set, otherwise <paramref name="defaultBranch"/>.
    /// </summary>
    public string GetBranch(string defaultBranch) =>
        BranchName is null ? defaultBranch : BranchName.Trim();
}
