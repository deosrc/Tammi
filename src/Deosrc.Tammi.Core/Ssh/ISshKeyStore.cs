namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// The SSH keys in the user's SSH folder (<c>~/.ssh</c>), which git uses for SSH remotes. Changes apply to every SSH
/// tool the user runs, not only Tammi.
/// </summary>
public interface ISshKeyStore
{
    /// <summary>
    /// The SSH folder. It might not exist yet.
    /// </summary>
    string KeyDirectory { get; }

    /// <summary>
    /// Whether <c>ssh-keygen</c> was found, so keys can be generated.
    /// </summary>
    bool CanGenerate { get; }

    /// <summary>
    /// Lists the key pairs: each readable <c>.pub</c> file with a private key file next to it, by name.
    /// </summary>
    /// <exception cref="IOException">The folder couldn't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder couldn't be read.</exception>
    Task<IReadOnlyList<SshKey>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests a name for a new key that isn't taken: the usual name for its type (e.g. <c>id_ed25519</c>), with a
    /// number after it if needed.
    /// </summary>
    string SuggestName(SshKeyType type);

    /// <summary>
    /// Checks a name for a new key.
    /// </summary>
    /// <returns>Why the name can't be used, or <c>null</c> if it can.</returns>
    string? CheckName(string? name);

    /// <summary>
    /// Generates a key pair with <c>ssh-keygen</c>, creating the SSH folder if it's missing.
    /// </summary>
    /// <returns>The new key.</returns>
    /// <exception cref="ArgumentException">The name can't be used (see <see cref="CheckName"/>).</exception>
    /// <exception cref="SshKeygenNotFoundException"><c>ssh-keygen</c> can't be found.</exception>
    /// <exception cref="SshKeyException"><c>ssh-keygen</c> failed.</exception>
    Task<SshKey> GenerateAsync(SshKeyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a key pair's files. It can't be undone.
    /// </summary>
    /// <exception cref="IOException">A file couldn't be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">A file couldn't be deleted.</exception>
    Task RemoveAsync(SshKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// The SSH client config, <c>config</c> in the SSH folder. It might not exist yet.
    /// </summary>
    string ConfigPath { get; }

    /// <summary>
    /// Whether SSH would try the key on its own: it has a default file name (see <see cref="SshKey.HasDefaultName"/>)
    /// or an <c>IdentityFile</c> in the SSH config names it. If not, the remote never sees the key.
    /// </summary>
    /// <exception cref="IOException">The config couldn't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The config couldn't be read.</exception>
    Task<bool> IsLoadedBySshAsync(SshKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters hosts to those without a config entry that names the key.
    /// </summary>
    /// <exception cref="IOException">The config couldn't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The config couldn't be read.</exception>
    Task<IReadOnlyList<string>> GetHostsWithoutEntryAsync(
        SshKey key, IEnumerable<string> hosts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a <c>Host</c> entry to the SSH config for each host that has none naming the key, creating the config
    /// if it's missing. Existing content is kept, and the entries go before any <c>Host *</c> entry.
    /// </summary>
    /// <returns>The hosts an entry was added for.</returns>
    /// <exception cref="IOException">The config couldn't be read or written.</exception>
    /// <exception cref="UnauthorizedAccessException">The config couldn't be read or written.</exception>
    Task<IReadOnlyList<string>> AddHostEntriesAsync(
        SshKey key, IEnumerable<string> hosts, CancellationToken cancellationToken = default);
}
