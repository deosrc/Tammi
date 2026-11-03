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
