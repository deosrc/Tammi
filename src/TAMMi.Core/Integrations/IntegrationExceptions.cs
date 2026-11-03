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
