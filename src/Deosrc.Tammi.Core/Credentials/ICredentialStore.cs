namespace Deosrc.Tammi.Core.Credentials;

/// <summary>
/// Keeps secrets, such as integration tokens, in the platform's secret store: Windows Credential Manager on Windows,
/// and the Secret Service (through libsecret) on Linux.
/// </summary>
/// <remarks>
/// Secrets are never written to Tammi's settings or data files. Each secret is stored under a key chosen by the
/// caller, e.g. <c>integration:github</c>. Integrations reach their secret through
/// <see cref="Integrations.IntegrationCredentials"/>.
/// </remarks>
public interface ICredentialStore
{
    /// <summary>
    /// Reads a secret.
    /// </summary>
    /// <returns>The secret, or <see langword="null"/> if none is stored under <paramref name="key"/>.</returns>
    /// <exception cref="CredentialStoreUnavailableException">There is no secret store to read from.</exception>
    /// <exception cref="CredentialStoreException">The secret store failed.</exception>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a secret, replacing any secret already stored under <paramref name="key"/>.
    /// </summary>
    /// <exception cref="CredentialStoreUnavailableException">There is no secret store to save to.</exception>
    /// <exception cref="CredentialStoreException">The secret store failed, or the secret is too large.</exception>
    Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    /// <returns>Whether a secret was stored under <paramref name="key"/>.</returns>
    /// <exception cref="CredentialStoreUnavailableException">There is no secret store to delete from.</exception>
    /// <exception cref="CredentialStoreException">The secret store failed.</exception>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
