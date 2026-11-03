namespace TAMMi.Core.Integrations;

/// <summary>
/// An integration couldn't complete a request, e.g. because its service can't be reached or returned an error.
/// </summary>
public class IntegrationException : Exception
{
    public IntegrationException(string message)
        : base(message)
    {
    }

    public IntegrationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

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
/// An integration's service is limiting how often TAMMi can call it. The request can be tried again from
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
