namespace TAMMi.Core.Credentials;

/// <summary>
/// The platform's secret store failed to read, save or delete a secret.
/// </summary>
public class CredentialStoreException : Exception
{
    public CredentialStoreException(string message)
        : base(message)
    {
    }

    public CredentialStoreException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// There is no secret store to keep credentials in, e.g. no Secret Service is running on Linux. The message says how
/// to fix it.
/// </summary>
public sealed class CredentialStoreUnavailableException(string message, Exception? innerException = null)
    : CredentialStoreException(message, innerException);
