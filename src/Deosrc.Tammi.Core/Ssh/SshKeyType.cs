namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// The kinds of SSH key Tammi can generate.
/// </summary>
public enum SshKeyType
{
    /// <summary>
    /// An Ed25519 key: short, fast and supported by every current service. The default.
    /// </summary>
    Ed25519,

    /// <summary>
    /// A 4096-bit RSA key, for older servers that don't accept Ed25519.
    /// </summary>
    Rsa,
}
