namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// An integration's credentials work, but don't allow a request, e.g. a token without write access to SSH keys.
/// The user can give the credentials the permission and authenticate again, or do it on the service's website.
/// </summary>
public sealed class IntegrationPermissionException(
    string integrationId,
    string message,
    Exception? innerException = null)
    : IntegrationException(message, innerException)
{
    /// <summary>
    /// The <see cref="IIntegration.Id"/> of the integration.
    /// </summary>
    public string IntegrationId { get; } = integrationId;
}
