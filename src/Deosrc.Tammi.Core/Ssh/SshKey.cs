using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// An SSH key pair in the user's SSH folder: a private key file and its <c>.pub</c> public key file.
/// </summary>
/// <param name="Name">The private key's file name, e.g. <c>id_ed25519</c>.</param>
/// <param name="PrivateKeyPath">The private key file.</param>
/// <param name="Algorithm">The public key's algorithm as OpenSSH names it, e.g. <c>ssh-ed25519</c>.</param>
/// <param name="Data">The public key's base64 data.</param>
/// <param name="Comment">The comment at the end of the public key, often an email address, or <c>null</c>.</param>
public sealed record SshKey(string Name, string PrivateKeyPath, string Algorithm, string Data, string? Comment)
{
    /// <summary>
    /// The public key file.
    /// </summary>
    public string PublicKeyPath => PrivateKeyPath + ".pub";

    /// <summary>
    /// The public key as one line, in the form services such as GitHub accept: algorithm, data and comment.
    /// </summary>
    public string PublicKey => Comment is null ? $"{Algorithm} {Data}" : $"{Algorithm} {Data} {Comment}";

    /// <summary>
    /// The key's SHA256 fingerprint as <c>ssh-keygen -l</c> and GitHub show it, e.g. <c>SHA256:nThbg6kX…</c>.
    /// </summary>
    public string Fingerprint
    {
        get
        {
            try
            {
                byte[] hash = SHA256.HashData(Convert.FromBase64String(Data));
                return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
            }
            catch (FormatException)
            {
                return "";
            }
        }
    }

    /// <summary>
    /// Whether a public key line (e.g. one a service lists) is this key. Comments are ignored, because services
    /// drop or replace them.
    /// </summary>
    public bool Matches(string? publicKey) =>
        TryParsePublicKey(publicKey, out string? algorithm, out string? data, out _)
        && string.Equals(algorithm, Algorithm, StringComparison.Ordinal)
        && string.Equals(data, Data, StringComparison.Ordinal);

    /// <summary>
    /// Splits a public key line, <c>algorithm data [comment]</c>, into its parts.
    /// </summary>
    public static bool TryParsePublicKey(
        string? line,
        [NotNullWhen(true)] out string? algorithm,
        [NotNullWhen(true)] out string? data,
        out string? comment)
    {
        algorithm = data = comment = null;
        string[] parts = (line ?? "").Trim().Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !IsBase64(parts[1]))
        {
            return false;
        }

        algorithm = parts[0];
        data = parts[1];
        comment = parts.Length == 3 ? parts[2].Trim() : null;
        return true;
    }

    private static bool IsBase64(string text)
    {
        Span<byte> buffer = new byte[text.Length];
        return Convert.TryFromBase64String(text, buffer, out int written) && written > 0;
    }
}
