namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Fetches, pulls and pushes with the <c>git</c> CLI, so the user's git config, credential helpers and hooks apply.
/// Works in submodules too, by giving the submodule's folder as the path.
/// </summary>
public interface IGitSync
{
    /// <summary>
    /// Fetches every remote, pruning branches the remote has deleted, and the checked-out submodules' remotes.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="progress">Told what git reports as it goes, e.g. <c>Receiving objects: 40% (4/10)</c>.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitSyncException">The fetch failed.</exception>
    Task FetchAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls the checked-out branch's upstream. Combining diverged branches follows the user's git config
    /// (<c>pull.rebase</c> and <c>pull.ff</c>), and merges when it says nothing.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="progress">Told what git reports as it goes.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitSyncException">
    /// The pull failed, e.g. with <see cref="GitSyncFailure.Conflict"/>. A conflicted merge is left in the working
    /// directory to resolve.
    /// </exception>
    Task PullAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the checked-out branch. A branch with no upstream is pushed to a remote (<c>origin</c>, or the only
    /// one) and starts tracking it.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="progress">Told what git reports as it goes.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitSyncException">The push failed, e.g. with <see cref="GitSyncFailure.PushRejected"/>.</exception>
    Task PushAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
