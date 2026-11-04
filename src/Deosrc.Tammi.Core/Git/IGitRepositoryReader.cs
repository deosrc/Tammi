namespace TAMMi.Core.Git;

/// <summary>
/// Fast, in-process reads of git repositories using LibGit2Sharp. Writes go through <see cref="IGitCli"/>.
/// </summary>
/// <remarks>
/// The methods are synchronous and do file I/O, so run them off the UI thread.
/// </remarks>
public interface IGitRepositoryReader
{
    /// <summary>
    /// The versions of LibGit2Sharp and the native libgit2 library.
    /// </summary>
    GitLibraryVersion LibraryVersion { get; }

    /// <summary>
    /// Reads the status of the repository at <paramref name="path"/>, including its submodules.
    /// </summary>
    /// <param name="path">The repository's working directory. Workspace Worktrees are supported.</param>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    RepositoryStatus ReadStatus(string path);

    /// <summary>
    /// Reads commits from the repository at <paramref name="path"/>, newest first in topological order (children
    /// before parents).
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <param name="options">Which commits to return.</param>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    IReadOnlyList<CommitInfo> ReadHistory(string path, HistoryOptions? options = null);

    /// <summary>
    /// Reads the remotes of the repository at <paramref name="path"/>, in name order.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    IReadOnlyList<GitRemote> ReadRemotes(string path);
}
