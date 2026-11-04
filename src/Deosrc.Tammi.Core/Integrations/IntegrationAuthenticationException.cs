namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// An integration's credentials are missing, have expired or were rejected. The user needs to authenticate again
/// in Settings.
/// </summary>
public sealed class IntegrationAuthenticationException(
    string integrationId,
    string message,
    Exception? innerException = null)
    : IntegrationException(message, innerException)
{
    /// <summary>
    /// The <see cref="IIntegration.Id"/> of the integration to authenticate again.
    /// </summary>
    public string IntegrationId { get; } = integrationId;
}

/// <summary>
/// An integration's service is limiting how often Tammi can call it. The request can be tried again from
/// <see cref="RetryAt"/>.
/// </summary>
public sealed class IntegrationRateLimitException(
    string integrationId,
    string message,
    DateTimeOffset retryAt,
    Exception? innerException = null)
    : IntegrationException(message, innerException)
{
    /// <summary>
    /// The <see cref="IIntegration.Id"/> of the integration that hit the limit.
    /// </summary>
    public string IntegrationId { get; } = integrationId;

    /// <summary>
    /// When the service expects to accept requests again.
    /// </summary>
    public DateTimeOffset RetryAt { get; } = retryAt;
}
