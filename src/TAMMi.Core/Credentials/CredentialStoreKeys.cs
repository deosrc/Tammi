namespace TAMMi.Core.Credentials;

/// <summary>
/// Checks credential keys, for the <see cref="ICredentialStore"/> implementations.
/// </summary>
internal static class CredentialStoreKeys
{
    /// <summary>
    /// The prefix that marks TAMMi's entries where the store is shared with other apps.
    /// </summary>
    public const string AppPrefix = "TAMMi:";

    public static void Validate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Any(char.IsControl))
        {
            throw new ArgumentException("A credential key can't contain control characters.", nameof(key));
        }
    }
}
