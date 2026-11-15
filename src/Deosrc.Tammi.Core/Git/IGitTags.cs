namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Creates, pushes and deletes tags with the <c>git</c> CLI, so the user's git config, credential helpers
/// and hooks apply. Works in submodules too, by giving the submodule's folder as the path.
/// </summary>
public interface IGitTags
{
    /// <summary>
    /// Creates a lightweight tag on a commit.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="name">The tag's name, e.g. <c>v1.0</c>.</param>
    /// <param name="sha">The commit to tag.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed, e.g. the tag exists or the name isn't valid.</exception>
    Task CreateAsync(string path, string name, string sha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds out whether a tag exists on the remote that tags are pushed to.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="name">The tag's name.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <returns>
    /// Whether the remote has a tag of that name. <see langword="false"/> if there's no remote or it can't be reached.
    /// </returns>
    Task<bool> ExistsOnRemoteAsync(string path, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a tag to a remote (<c>origin</c>, or the only one).
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="name">The tag's name.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitException">Git failed, e.g. there is no remote.</exception>
    Task PushAsync(string path, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tag locally and, if the remote has it, there too.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="name">The tag's name.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task DeleteAsync(string path, string name, CancellationToken cancellationToken = default);
}
