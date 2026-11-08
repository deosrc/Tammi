namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// A key to generate with <see cref="ISshKeyStore.GenerateAsync"/>.
/// </summary>
/// <param name="Name">The private key's file name in the SSH folder, e.g. <c>id_ed25519</c>.</param>
/// <param name="Type">The kind of key.</param>
public sealed record SshKeyRequest(string Name, SshKeyType Type)
{
    /// <summary>
    /// The comment to put at the end of the public key, usually the user's email address, or <c>null</c> for none.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// The passphrase that protects the private key, or <c>null</c> or empty for none.
    /// </summary>
    public string? Passphrase { get; init; }
}
