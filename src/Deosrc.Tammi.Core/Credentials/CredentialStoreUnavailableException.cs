namespace Deosrc.Tammi.Core.Credentials;

/// <summary>
/// There is no secret store to keep credentials in, e.g. no Secret Service is running on Linux. The message says how
/// to fix it.
/// </summary>
public sealed class CredentialStoreUnavailableException(string message, Exception? innerException = null)
    : CredentialStoreException(message, innerException);
