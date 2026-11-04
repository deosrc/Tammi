namespace TAMMi.Core.Integrations;

/// <summary>
/// Whether TAMMi can reach an integration's service with its credentials, as last checked.
/// </summary>
public enum IntegrationConnectionState
{
    /// <summary>
    /// The connection hasn't been checked since TAMMi started.
    /// </summary>
    NotChecked,

    /// <summary>
    /// The connection is being checked.
    /// </summary>
    Checking,

    /// <summary>
    /// The service accepted the credentials.
    /// </summary>
    Connected,

    /// <summary>
    /// No credentials are stored, so the integration needs authenticating.
    /// </summary>
    NotAuthenticated,

    /// <summary>
    /// The service rejected the credentials, e.g. because the token expired or was revoked.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// The check failed for another reason, e.g. the service couldn't be reached or the credential store failed.
    /// </summary>
    Failed,
}
