namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads and changes the user's global git config (<c>git config --global</c>). The changes apply to every git tool
/// the user runs, not only Tammi.
/// </summary>
public interface IGitUserConfig
{
    /// <summary>
    /// Reads the author git records on new commits.
    /// </summary>
    /// <exception cref="GitNotInstalledException">Git can't be found.</exception>
    /// <exception cref="GitCommandException">Git couldn't read the config.</exception>
    Task<GitIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the author's name (<c>user.name</c>).
    /// </summary>
    /// <param name="name">The name. Surrounding spaces are removed, and a blank name unsets it.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="GitNotInstalledException">Git can't be found.</exception>
    /// <exception cref="GitCommandException">Git couldn't change the config.</exception>
    Task SetNameAsync(string? name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the author's email address (<c>user.email</c>).
    /// </summary>
    /// <param name="email">The address. Surrounding spaces are removed, and a blank address unsets it.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <exception cref="GitNotInstalledException">Git can't be found.</exception>
    /// <exception cref="GitCommandException">Git couldn't change the config.</exception>
    Task SetEmailAsync(string? email, CancellationToken cancellationToken = default);
}
