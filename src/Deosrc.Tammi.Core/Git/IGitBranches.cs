namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Creates, checks out, renames and deletes branches with the <c>git</c> CLI, so the user's git config, credential
/// helpers and hooks apply. Works in submodules too, by giving the submodule's folder as the path.
/// </summary>
public interface IGitBranches
{
    /// <summary>
    /// Creates a local branch, and optionally checks it out.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="name">The new branch's name.</param>
    /// <param name="startPoint">The commit (or branch) it starts from.</param>
    /// <param name="checkout">Whether to check it out after creating it.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed, e.g. the branch exists or the name isn't valid.</exception>
    Task CreateAsync(string path, string name, string startPoint, bool checkout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks out a branch. A remote-tracking branch such as <c>origin/feature</c> is checked out as a new local
    /// branch of the same name that tracks it.
    /// </summary>
    /// <exception cref="GitCommandException">
    /// Git failed, e.g. local changes would be overwritten or the branch is checked out in another worktree.
    /// </exception>
    Task CheckoutAsync(string path, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a local branch. Its remote branch isn't renamed.
    /// </summary>
    /// <exception cref="GitCommandException">Git failed, e.g. the new name is taken or isn't valid.</exception>
    Task RenameAsync(string path, string name, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the remote branch a local branch tracks.
    /// </summary>
    /// <returns>The remote and the branch's name there, or <see langword="null"/> if it has no upstream.</returns>
    Task<GitUpstream?> GetUpstreamAsync(string path, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a local branch (even if it isn't merged), a remote branch, or both.
    /// </summary>
    /// <param name="path">The repository's (or submodule's) working directory.</param>
    /// <param name="name">A local branch's name, e.g. <c>feature</c>.</param>
    /// <param name="deleteLocal">Delete the local branch.</param>
    /// <param name="deleteRemote">Delete the branch <paramref name="name"/> tracks on its remote, if it has one.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitCommandException">Git failed, e.g. the branch is checked out.</exception>
    Task DeleteAsync(string path, string name, bool deleteLocal, bool deleteRemote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a remote branch given as a remote-tracking name such as <c>origin/feature</c>.
    /// </summary>
    /// <exception cref="GitCommandException">Git failed.</exception>
    Task DeleteRemoteAsync(string path, string remoteBranch, CancellationToken cancellationToken = default);
}
