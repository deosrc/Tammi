using TAMMi.Core.Git;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// A Workspace Worktree that deleting a workspace removes, with any work that would be lost.
/// </summary>
/// <param name="Path">The worktree folder's full path, in the Workspace Folder.</param>
/// <param name="Health">Whether the worktree is connected to its Local Repository.</param>
/// <param name="LocalRepositoryPath">
/// The Local Repository the worktree belongs to: where the worktree points, or the metadata's path when the folder is
/// missing.
/// </param>
public sealed record WorktreeToDelete(string Path, WorktreeHealth Health, string LocalRepositoryPath)
{
    /// <summary>
    /// The workspace's repository the worktree is for, or <see langword="null"/> for a worktree found in the Workspace
    /// Folder that the metadata doesn't list (e.g. when the metadata can't be read).
    /// </summary>
    public WorkspaceRepository? Repository { get; init; }

    /// <summary>
    /// The worktree's status, or <see langword="null"/> when it couldn't be read (see <see cref="Problem"/>) or the
    /// folder is missing.
    /// </summary>
    public RepositoryStatus? Status { get; init; }

    /// <summary>
    /// Why the worktree couldn't be checked for work that would be lost, or <see langword="null"/> if it was checked.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>
    /// The worktree folder's name.
    /// </summary>
    public string FolderName => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// Whether removing the worktree could lose work, so the user must confirm it: it has uncommitted changes or
    /// unpushed commits (in it or its submodules), or it couldn't be checked.
    /// </summary>
    public bool NeedsConfirmation => Status?.IsChanged == true || Problem is not null;
}
