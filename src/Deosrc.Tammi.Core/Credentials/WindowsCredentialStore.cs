using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Deosrc.Tammi.Core.Credentials;

/// <summary>
/// Keeps secrets in Windows Credential Manager, as generic credentials named <c>Tammi:&lt;key&gt;</c>.
/// </summary>
/// <remarks>
/// Credentials are saved for the local machine only, so they don't roam with the user's profile, in keeping with
/// Tammi's local app data. Secrets are stored as UTF-8 and can be at most 2560 bytes long.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsCredentialStore : ICredentialStore
{
    /// <summary>
    /// <c>CRED_MAX_CREDENTIAL_BLOB_SIZE</c>: the largest secret Credential Manager accepts.
    /// </summary>
    internal const int MaxSecretBytes = 5 * 512;

    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int ErrorNoSuchLogonSession = 1312;

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        CredentialStoreKeys.Validate(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CredReadW(TargetName(key), CredTypeGeneric, 0, out nint credential))
        {
            int error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound
                ? Task.FromResult<string?>(null)
                : throw CreateException(error, "read");
        }

        try
        {
            return Task.FromResult<string?>(ReadSecret(credential));
        }
        finally
        {
            CredFree(credential);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        CredentialStoreKeys.Validate(key);
        ArgumentNullException.ThrowIfNull(secret);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] blob = Encoding.UTF8.GetBytes(secret);
        try
        {
            if (blob.Length > MaxSecretBytes)
            {
                throw new CredentialStoreException(
                    $"The secret is {blob.Length} bytes long, but Windows Credential Manager can only store "
                        + $"{MaxSecretBytes} bytes.");
            }

            Write(TargetName(key), blob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        CredentialStoreKeys.Validate(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (CredDeleteW(TargetName(key), CredTypeGeneric, 0))
        {
            return Task.FromResult(true);
        }

        int error = Marshal.GetLastPInvokeError();
        return error == ErrorNotFound ? Task.FromResult(false) : throw CreateException(error, "delete");
    }

    private static string TargetName(string key) => CredentialStoreKeys.AppPrefix + key;

    private static unsafe string ReadSecret(nint credential)
    {
        var native = (NativeCredential*)credential;
        return native->CredentialBlobSize == 0
            ? string.Empty
            : Encoding.UTF8.GetString(native->CredentialBlob, (int)native->CredentialBlobSize);
    }

    private static unsafe void Write(string targetName, byte[] blob)
    {
        fixed (char* target = targetName)
        fixed (byte* secret = blob)
        {
            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = secret,
                Persist = CredPersistLocalMachine,
            };

            if (!CredWriteW(&credential, 0))
            {
                throw CreateException(Marshal.GetLastPInvokeError(), "save");
            }
        }
    }

    private static CredentialStoreException CreateException(int error, string operation)
    {
        var inner = new Win32Exception(error);
        return error == ErrorNoSuchLogonSession
            ? new CredentialStoreUnavailableException(
                "Windows Credential Manager isn't available, because this Windows session can't store credentials. "
                    + "Sign in to Windows with a user account, not a service or network logon, and restart Tammi.",
                inner)
            : new CredentialStoreException(
                $"Windows Credential Manager couldn't {operation} the credential: {inner.Message}",
                inner);
    }

    /// <summary>
    /// <c>CREDENTIALW</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public char* TargetName;
        public char* Comment;
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public byte* CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public char* TargetAlias;
        public char* UserName;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredReadW(string targetName, uint type, uint flags, out nint credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool CredWriteW(NativeCredential* credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDeleteW(string targetName, uint type, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    private static partial void CredFree(nint buffer);
}
