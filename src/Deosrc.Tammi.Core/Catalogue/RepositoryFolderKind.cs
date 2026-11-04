namespace Deosrc.Tammi.Core.Catalogue;

/// <summary>
/// What a folder holds, as far as the Repository Catalogue is concerned (see <see cref="RepositoryScanner.Inspect"/>).
/// </summary>
public enum RepositoryFolderKind
{
    /// <summary>
    /// The folder isn't the working directory of a git repository.
    /// </summary>
    NotRepository,

    /// <summary>
    /// The folder is a repository's working directory, so it can be a Local Repository.
    /// </summary>
    Repository,

    /// <summary>
    /// The folder is a linked worktree (made with <c>git worktree add</c>), e.g. a Workspace Worktree. Worktrees
    /// aren't catalogued: their main repository is.
    /// </summary>
    LinkedWorktree,
}
