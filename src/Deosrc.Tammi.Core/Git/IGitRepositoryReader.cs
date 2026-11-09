namespace Deosrc.Tammi.Core.Git;

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
    /// Opens the history of the repository at <paramref name="path"/> to read a page at a time, newest first in
    /// topological order (children before parents). Use it for long histories, e.g. the git graph.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <param name="options">Which commits to return. <see cref="HistoryOptions.MaxCount"/> is ignored.</param>
    /// <returns>The cursor, which keeps the repository open until it's disposed.</returns>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    IGitHistoryCursor OpenHistory(string path, HistoryOptions? options = null);

    /// <summary>
    /// Reads the HEAD, branches and tags of the repository at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    GitRefs ReadRefs(string path);

    /// <summary>
    /// Reads the files a commit changed, compared with its first parent (or with nothing, for a commit with no
    /// parents), in path order. Renames are detected.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <param name="sha">The commit's full id.</param>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    /// <exception cref="GitException">The repository has no commit <paramref name="sha"/>.</exception>
    IReadOnlyList<CommitFileChange> ReadCommitChanges(string path, string sha);

    /// <summary>
    /// Reads the remotes of the repository at <paramref name="path"/>, in name order.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    IReadOnlyList<GitRemote> ReadRemotes(string path);

    /// <summary>
    /// Reads the changes to one file as hunks of lines.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <param name="source">Which two states to compare.</param>
    /// <param name="file">The file's path relative to the repository root, with <c>/</c> separators.</param>
    /// <param name="oldPath">For a rename, the path the file was renamed from.</param>
    /// <param name="sha">For <see cref="GitDiffSource.Commit"/>, the commit's full id.</param>
    /// <returns>The diff, which has no hunks if the file hasn't changed.</returns>
    /// <exception cref="GitRepositoryNotFoundException">There's no repository at <paramref name="path"/>.</exception>
    /// <exception cref="GitException">The repository has no commit <paramref name="sha"/>.</exception>
    FileDiff ReadDiff(string path, GitDiffSource source, string file, string? oldPath = null, string? sha = null);
}
