using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TAMMi.Core.Credentials;

/// <summary>
/// Keeps secrets with the Secret Service (e.g. GNOME Keyring or KWallet), through libsecret.
/// </summary>
/// <remarks>
/// Secrets are saved in the default collection (usually the "Login" keyring), labelled <c>TAMMi:&lt;key&gt;</c>.
/// libsecret is a system library, so it isn't shipped with TAMMi. Calls block while the Secret Service asks the user
/// to unlock a keyring, so they run on the thread pool and can be cancelled.
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed partial class LibSecretCredentialStore : ICredentialStore
{
    /// <summary>
    /// The libsecret schema for TAMMi's secrets. The <see cref="KeyAttribute"/> attribute holds the key.
    /// </summary>
    internal const string SchemaName = "com.deosrc.tammi.Credential";

    internal const string KeyAttribute = "key";

    private static readonly Lazy<nint> Schema = new(CreateSchema, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        CredentialStoreKeys.Validate(key);
        return Run(key, cancellationToken, (attributes, cancellable) =>
        {
            nint password = Native.secret_password_lookupv_sync(Schema.Value, attributes, cancellable, out nint error);
            ThrowIfError(error, cancellationToken);
            if (password == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(password);
            }
            finally
            {
                // Clears the memory before freeing it.
                Native.secret_password_free(password);
            }
        });
    }

    /// <inheritdoc />
    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        CredentialStoreKeys.Validate(key);
        ArgumentNullException.ThrowIfNull(secret);
        return Run(key, cancellationToken, (attributes, cancellable) =>
        {
            bool saved = Native.secret_password_storev_sync(
                Schema.Value,
                attributes,
                collection: null,
                CredentialStoreKeys.AppPrefix + key,
                secret,
                cancellable,
                out nint error);
            ThrowIfError(error, cancellationToken);
            return saved
                ? true
                : throw new CredentialStoreException(
                    "The Secret Service didn't save the credential. Check that your keyring is unlocked.");
        });
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        CredentialStoreKeys.Validate(key);
        return Run(key, cancellationToken, (attributes, cancellable) =>
        {
            bool deleted = Native.secret_password_clearv_sync(Schema.Value, attributes, cancellable, out nint error);
            ThrowIfError(error, cancellationToken);
            return deleted;
        });
    }

    /// <summary>
    /// Turns a libsecret error into an exception. Errors from D-Bus mean the Secret Service can't be reached.
    /// </summary>
    /// <param name="domain">The error's domain, e.g. <c>g-dbus-error-quark</c>.</param>
    internal static Exception CreateException(string? domain, int code, string? message)
    {
        message = string.IsNullOrWhiteSpace(message) ? "Unknown error." : message.Trim();
        if (domain == "g-dbus-error-quark" || (domain == "g-io-error-quark" && IsBusError(code)))
        {
            return new CredentialStoreUnavailableException(
                "No secret store is available to keep TAMMi's credentials in. TAMMi needs a Secret Service, such as "
                    + "GNOME Keyring or KWallet, running in your desktop session. Install one (e.g. the "
                    + $"'gnome-keyring' package), sign out and back in, and restart TAMMi. Details: {message}");
        }

        return new CredentialStoreException($"The Secret Service failed: {message}");
    }

    /// <summary>
    /// Whether an I/O error means the session bus, or the Secret Service on it, can't be reached: e.g. there is no
    /// session bus (<c>NotFound</c>), or the Secret Service didn't start in time (<c>TimedOut</c>).
    /// </summary>
    private static bool IsBusError(int code) =>
        code is GIOError.NotFound or GIOError.NotSupported or GIOError.TimedOut or GIOError.DBusError;

    private static Task<T> Run<T>(
        string key,
        CancellationToken cancellationToken,
        Func<nint, nint, T> operation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(
            () =>
            {
                try
                {
                    using var attributes = new NativeStringTable([(KeyAttribute, key)]);
                    nint cancellable = Native.g_cancellable_new();
                    try
                    {
                        using CancellationTokenRegistration registration =
                            cancellationToken.Register(() => Native.g_cancellable_cancel(cancellable));
                        return operation(attributes.Handle, cancellable);
                    }
                    finally
                    {
                        Native.g_object_unref(cancellable);
                    }
                }
                catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
                {
                    throw new CredentialStoreUnavailableException(
                        "No secret store is available to keep TAMMi's credentials in, because libsecret isn't "
                            + "installed. Install it (e.g. 'libsecret-1-0' on Debian and Ubuntu, or 'libsecret' on "
                            + "Fedora and Arch) and restart TAMMi.",
                        exception);
                }
            },
            cancellationToken);
    }

    private static void ThrowIfError(nint error, CancellationToken cancellationToken)
    {
        if (error == 0)
        {
            return;
        }

        GError details = Marshal.PtrToStructure<GError>(error);
        string? domain = Marshal.PtrToStringUTF8(Native.g_quark_to_string(details.Domain));
        string? message = Marshal.PtrToStringUTF8(details.Message);
        Native.g_error_free(error);

        if (domain == "g-io-error-quark" && details.Code == GIOError.Cancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        throw CreateException(domain, details.Code, message);
    }

    private static nint CreateSchema()
    {
        // Attribute types are stored as pointer-sized integers; SECRET_SCHEMA_ATTRIBUTE_STRING is 0.
        using var attributeTypes = new NativeStringTable([(KeyAttribute, null)]);
        return Native.secret_schema_newv(SchemaName, flags: 0, attributeTypes.Handle);
    }

    /// <summary>
    /// <c>GIOErrorEnum</c> values.
    /// </summary>
    private static class GIOError
    {
        public const int NotFound = 1;
        public const int NotSupported = 15;
        public const int Cancelled = 19;
        public const int TimedOut = 24;
        public const int DBusError = 36;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GError
    {
        public uint Domain;
        public int Code;
        public nint Message;
    }

    /// <summary>
    /// A <c>GHashTable</c> of UTF-8 strings, as libsecret takes attributes. A <see langword="null"/> value is stored
    /// as a null pointer.
    /// </summary>
    private sealed class NativeStringTable : IDisposable
    {
        private readonly List<nint> _strings = [];

        public NativeStringTable(IEnumerable<(string Key, string? Value)> entries)
        {
            Handle = Native.g_hash_table_new(Native.StringHash, Native.StringEqual);
            foreach ((string key, string? value) in entries)
            {
                Native.g_hash_table_insert(Handle, Allocate(key), value is null ? 0 : Allocate(value));
            }
        }

        public nint Handle { get; }

        public void Dispose()
        {
            Native.g_hash_table_unref(Handle);
            foreach (nint value in _strings)
            {
                Marshal.FreeCoTaskMem(value);
            }
        }

        private nint Allocate(string value)
        {
            nint pointer = Marshal.StringToCoTaskMemUTF8(value);
            _strings.Add(pointer);
            return pointer;
        }
    }

    private static partial class Native
    {
        private const string LibSecret = "libsecret-1.so.0";
        private const string GLib = "libglib-2.0.so.0";
        private const string GObject = "libgobject-2.0.so.0";
        private const string Gio = "libgio-2.0.so.0";

        private static readonly Lazy<nint> GLibHandle = new(() => NativeLibrary.Load(GLib));

        public static nint StringHash => NativeLibrary.GetExport(GLibHandle.Value, "g_str_hash");

        public static nint StringEqual => NativeLibrary.GetExport(GLibHandle.Value, "g_str_equal");

        [LibraryImport(LibSecret, StringMarshalling = StringMarshalling.Utf8)]
        public static partial nint secret_schema_newv(string name, int flags, nint attributeNamesAndTypes);

        [LibraryImport(LibSecret, StringMarshalling = StringMarshalling.Utf8)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool secret_password_storev_sync(
            nint schema,
            nint attributes,
            string? collection,
            string label,
            string password,
            nint cancellable,
            out nint error);

        [LibraryImport(LibSecret)]
        public static partial nint secret_password_lookupv_sync(
            nint schema,
            nint attributes,
            nint cancellable,
            out nint error);

        [LibraryImport(LibSecret)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool secret_password_clearv_sync(
            nint schema,
            nint attributes,
            nint cancellable,
            out nint error);

        [LibraryImport(LibSecret)]
        public static partial void secret_password_free(nint password);

        [LibraryImport(GLib)]
        public static partial nint g_hash_table_new(nint hashFunction, nint equalFunction);

        [LibraryImport(GLib)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool g_hash_table_insert(nint table, nint key, nint value);

        [LibraryImport(GLib)]
        public static partial void g_hash_table_unref(nint table);

        [LibraryImport(GLib)]
        public static partial nint g_quark_to_string(uint quark);

        [LibraryImport(GLib)]
        public static partial void g_error_free(nint error);

        [LibraryImport(Gio)]
        public static partial nint g_cancellable_new();

        [LibraryImport(Gio)]
        public static partial void g_cancellable_cancel(nint cancellable);

        [LibraryImport(GObject)]
        public static partial void g_object_unref(nint instance);
    }
}
