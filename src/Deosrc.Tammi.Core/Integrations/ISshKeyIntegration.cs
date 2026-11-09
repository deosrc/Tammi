namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// An integration whose service accepts SSH keys for git, such as GitHub.
/// </summary>
/// <remarks>
/// Adding a key is the one change Tammi makes through an integration, and only when the user asks. If the credentials
/// don't allow it, the user can add the key on <see cref="NewSshKeyUrl"/> instead.
/// </remarks>
public interface ISshKeyIntegration : IIntegration
{
    /// <summary>
    /// The service's page for adding an SSH key by hand.
    /// </summary>
    Uri NewSshKeyUrl { get; }

    /// <summary>
    /// The host name SSH connects to for git on this service, e.g. <c>github.com</c> or an Enterprise Server's host.
    /// It's what a <c>Host</c> entry in the SSH config names.
    /// </summary>
    string SshHost { get; }

    /// <summary>
    /// Lists the public keys on the user's account, without their comments.
    /// </summary>
    /// <returns>The keys, or <see langword="null"/> if the credentials aren't allowed to list them.</returns>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<string>?> GetSshKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a public key to the user's account. A key that's already on the account counts as added.
    /// </summary>
    /// <param name="title">The name the service shows for the key, e.g. the computer's name.</param>
    /// <param name="publicKey">The public key line, e.g. <c>ssh-ed25519 AAAA… me@example.com</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IntegrationPermissionException">The credentials aren't allowed to add keys.</exception>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">
    /// The service couldn't be reached, or refused the key, e.g. because another account already has it.
    /// </exception>
    Task AddSshKeyAsync(string title, string publicKey, CancellationToken cancellationToken = default);
}
