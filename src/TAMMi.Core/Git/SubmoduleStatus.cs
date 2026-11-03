namespace TAMMi.Core.Git;

/// <summary>
/// A submodule of a repository.
/// </summary>
/// <param name="Name">The submodule's name in <c>.gitmodules</c>.</param>
/// <param name="Path">The path relative to the parent repository root, with <c>/</c> separators.</param>
/// <param name="CommitChange">
/// How the commit the parent repository records for the submodule has changed. A changed commit is an uncommitted
/// change in the parent repository.
/// </param>
/// <param name="Status">
/// The submodule's own status, or <see langword="null"/> when it isn't checked out.
/// </param>
public sealed record SubmoduleStatus(
    string Name,
    string Path,
    SubmoduleCommitChange CommitChange,
    RepositoryStatus? Status)
{
    /// <summary>
    /// Whether the submodule is checked out (<c>git submodule update --init</c> has run).
    /// </summary>
    public bool IsInitialized => Status is not null;
}
