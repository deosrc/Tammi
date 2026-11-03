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
