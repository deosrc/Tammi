namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A repository in a workspace: a Workspace Worktree of a Local Repository, on the workspace's branch.
/// </summary>
/// <param name="FolderName">
/// The worktree's folder in the Workspace Folder, e.g. <c>api</c> or <c>acme-api</c>. It is a single folder name, not
/// a path.
/// </param>
/// <param name="LocalRepositoryPath">The full path of the Local Repository the worktree was created from.</param>
/// <param name="Branch">The worktree's branch, created for the workspace.</param>
/// <param name="MergeTarget">
/// What <paramref name="Branch"/> was created from, e.g. <c>origin/main</c>. The branch counts as merged when its tip
/// is reachable from it.
/// </param>
public sealed record WorkspaceRepository(string FolderName, string LocalRepositoryPath, string Branch, string MergeTarget);
