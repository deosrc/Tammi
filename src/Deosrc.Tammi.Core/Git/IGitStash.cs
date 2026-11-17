namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Stashes changes and brings them back with the <c>git</c> CLI. Works in submodules too, by giving the
/// submodule's folder as the path.
/// </summary>
public interface IGitStash
{
    /// <summary>
    /// Lists the stashes, newest first.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task<IReadOnlyList<GitStashEntry>> ListAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stashes changes, including new files, and puts the files back as they are in HEAD.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="files">The files to stash, or <see langword="null"/> or empty to stash every change. For a rename, include both paths.</param>
    /// <param name="message">What to call the stash, or <see langword="null"/> or blank for git's own.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <returns>Whether a stash was made: <see langword="false"/> if there was nothing to stash.</returns>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task<bool> PushAsync(
        string path,
        IReadOnlyCollection<string>? files = null,
        string? message = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a stash to the working directory, and keeps it.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="index">The stash's <see cref="GitStashEntry.Index"/>.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed, e.g. the changes conflict with the working directory.</exception>
    Task ApplyAsync(string path, int index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a stash to the working directory, then drops it. If it doesn't apply cleanly, it's kept.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="index">The stash's <see cref="GitStashEntry.Index"/>.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed, e.g. the changes conflict with the working directory.</exception>
    Task PopAsync(string path, int index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stash without applying it.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="index">The stash's <see cref="GitStashEntry.Index"/>.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task DropAsync(string path, int index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the commit that a commit and HEAD have in common, which is where a stash made on another branch joins
    /// the history of the checked-out one.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="sha">The commit.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <returns>The common ancestor's sha, or <see langword="null"/> if there's none.</returns>
    Task<string?> FindMergeBaseWithHeadAsync(string path, string sha, CancellationToken cancellationToken = default);
}
