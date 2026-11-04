namespace TAMMi.Core.Workspaces;

/// <summary>
/// A repository for <see cref="WorkspaceCreator"/> to add to a new workspace, as a Workspace Worktree.
/// </summary>
/// <param name="FolderName">The worktree's folder in the Workspace Folder. It is a single folder name, not a path.</param>
/// <param name="LocalRepositoryPath">The Local Repository's full path.</param>
/// <param name="BaseBranch">What the workspace's branch starts from, e.g. <c>origin/main</c>.</param>
/// <param name="Remote">
/// The remote to fetch before creating the branch, or <see langword="null"/> if the repository has none.
/// </param>
public sealed record WorkspaceCreationRepository(
    string FolderName,
    string LocalRepositoryPath,
    string BaseBranch,
    string? Remote);
