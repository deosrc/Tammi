namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Changes a repository's index and history with the <c>git</c> CLI: staging, unstaging and committing. Works in
/// submodules too, by giving the submodule's folder as the path.
/// </summary>
public interface IGitWorkingTree
{
    /// <summary>
    /// Stages the changes to files, including deletions.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="files">The files to stage. For a rename, include both paths.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task StageAsync(string path, IReadOnlyCollection<string> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unstages the changes to files, leaving the working directory as it is.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="files">The files to unstage. For a rename, include both paths.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task UnstageAsync(string path, IReadOnlyCollection<string> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits what's staged. Git's hooks run, and can reject the commit.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="message">The full commit message.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed, e.g. nothing is staged or a hook rejected the commit.</exception>
    Task CommitAsync(string path, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the saved draft of the next commit's message, which git keeps in <c>COMMIT_EDITMSG</c> in the
    /// repository's git directory.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <returns>The message, or an empty string if there's none.</returns>
    Task<string> ReadMessageAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the draft of the next commit's message.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="message">The message. An empty message clears the draft.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    Task WriteMessageAsync(string path, string message, CancellationToken cancellationToken = default);
}
