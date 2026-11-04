using Deosrc.Tammi.Core.Credentials;

namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// One integration's secret in the <see cref="ICredentialStore"/>, e.g. an access token.
/// </summary>
/// <remarks>
/// Each integration has one secret, so removing an integration only has one secret to delete. An integration that
/// needs several values (e.g. an access token and a refresh token) stores them together, e.g. as JSON. Values that
/// aren't secret, such as a user name or site URL, belong in settings.
/// </remarks>
public sealed class IntegrationCredentials
{
    private readonly ICredentialStore _store;

    /// <param name="store">The secret store.</param>
    /// <param name="integrationId">The <see cref="IIntegration.Id"/> of the integration the secret belongs to.</param>
    public IntegrationCredentials(ICredentialStore store, string integrationId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(integrationId);
        _store = store;
        IntegrationId = integrationId;
    }

    /// <summary>
    /// The <see cref="IIntegration.Id"/> of the integration the secret belongs to.
    /// </summary>
    public string IntegrationId { get; }

    /// <summary>
    /// The key the secret is stored under.
    /// </summary>
    public string Key => KeyFor(IntegrationId);

    /// <summary>
    /// The key an integration's secret is stored under.
    /// </summary>
    public static string KeyFor(string integrationId) => $"integration:{integrationId}";

    /// <inheritdoc cref="ICredentialStore.GetAsync"/>
    public Task<string?> GetAsync(CancellationToken cancellationToken = default) =>
        _store.GetAsync(Key, cancellationToken);

    /// <summary>
    /// Reads the secret, for an integration about to call its service.
    /// </summary>
    /// <exception cref="IntegrationAuthenticationException">No secret is stored.</exception>
    /// <exception cref="CredentialStoreException">The secret store failed.</exception>
    public async Task<string> GetRequiredAsync(CancellationToken cancellationToken = default) =>
        await GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new IntegrationAuthenticationException(
                IntegrationId,
                $"Integration '{IntegrationId}' isn't authenticated. Authenticate it in Settings → Integrations.");

    /// <inheritdoc cref="ICredentialStore.SaveAsync"/>
    public Task SaveAsync(string secret, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(Key, secret, cancellationToken);

    /// <inheritdoc cref="ICredentialStore.DeleteAsync"/>
    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(Key, cancellationToken);
}
