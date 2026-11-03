using System.Runtime.Versioning;
using TAMMi.Core.Credentials;

namespace TAMMi.Core.Tests.Credentials;

/// <summary>
/// Saves, reads and deletes real secrets in the platform secret store, under random keys that are deleted afterwards.
/// The tests are skipped when the store isn't available, e.g. on a CI machine without a Secret Service.
/// </summary>
public abstract class CredentialStoreContractTests
{
    private readonly string _key = $"tests:{Guid.NewGuid():N}";

    private ICredentialStore? _store;

    /// <summary>
    /// The store under test. Using it skips the test on other platforms.
    /// </summary>
    private protected ICredentialStore Store
    {
        get
        {
            if (!IsSupportedPlatform)
            {
                Assert.Skip("The store isn't supported on this platform.");
            }

            return _store ??= CreateStore();
        }
    }

    private protected abstract bool IsSupportedPlatform { get; }

    private protected abstract ICredentialStore CreateStore();

    [Fact]
    public async Task SavedSecret_CanBeReadReplacedAndDeleted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SkipIfUnavailableAsync();
        try
        {
            await Store.SaveAsync(_key, "first sécret ✓", cancellationToken);
            Assert.Equal("first sécret ✓", await Store.GetAsync(_key, cancellationToken));

            await Store.SaveAsync(_key, "second", cancellationToken);
            Assert.Equal("second", await Store.GetAsync(_key, cancellationToken));

            Assert.True(await Store.DeleteAsync(_key, cancellationToken));
            Assert.Null(await Store.GetAsync(_key, cancellationToken));
        }
        finally
        {
            await Store.DeleteAsync(_key, CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNothingIsSaved()
    {
        await SkipIfUnavailableAsync();

        Assert.Null(await Store.GetAsync(_key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNothingIsSaved()
    {
        await SkipIfUnavailableAsync();

        Assert.False(await Store.DeleteAsync(_key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmptySecret_CanBeSaved()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SkipIfUnavailableAsync();
        try
        {
            await Store.SaveAsync(_key, "", cancellationToken);
            Assert.Equal("", await Store.GetAsync(_key, cancellationToken));
        }
        finally
        {
            await Store.DeleteAsync(_key, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Calls_Throw_WhenCancelled()
    {
        ICredentialStore store = Store;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetAsync(_key, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(_key, "x", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(_key, cancellation.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("line\nbreak")]
    public async Task Calls_Throw_ForInvalidKeys(string key)
    {
        ICredentialStore store = Store;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.GetAsync(key, cancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.SaveAsync(key, "x", cancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync(key, cancellationToken));
    }

    private async Task SkipIfUnavailableAsync()
    {
        try
        {
            await Store.GetAsync(_key, TestContext.Current.CancellationToken);
        }
        catch (CredentialStoreUnavailableException exception)
        {
            Assert.Skip($"The secret store isn't available: {exception.Message}");
        }
    }
}

[SupportedOSPlatform("linux")]
public sealed class LibSecretCredentialStoreTests : CredentialStoreContractTests
{
    private protected override bool IsSupportedPlatform => OperatingSystem.IsLinux();

    private protected override ICredentialStore CreateStore() => new LibSecretCredentialStore();

    [Fact]
    public void DBusErrors_MeanTheStoreIsUnavailable()
    {
        Exception exception = LibSecretCredentialStore.CreateException(
            "g-dbus-error-quark",
            2,
            "The name org.freedesktop.secrets was not provided by any .service files");

        var unavailable = Assert.IsType<CredentialStoreUnavailableException>(exception);
        Assert.Contains("Secret Service", unavailable.Message);
        Assert.Contains("org.freedesktop.secrets", unavailable.Message);
    }

    [Fact]
    public void MissingSessionBus_MeansTheStoreIsUnavailable()
    {
        Exception exception = LibSecretCredentialStore.CreateException(
            "g-io-error-quark",
            1,
            "Could not connect: No such file or directory");

        Assert.IsType<CredentialStoreUnavailableException>(exception);
    }

    [Fact]
    public void OtherErrors_AreStoreFailures()
    {
        Exception exception = LibSecretCredentialStore.CreateException("secret-error", 1, "Something broke");

        Assert.IsType<CredentialStoreException>(exception);
        Assert.Contains("Something broke", exception.Message);
    }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStoreTests : CredentialStoreContractTests
{
    private protected override bool IsSupportedPlatform => OperatingSystem.IsWindows();

    private protected override ICredentialStore CreateStore() => new WindowsCredentialStore();

    [Fact]
    public async Task SaveAsync_Throws_WhenSecretIsTooLarge()
    {
        ICredentialStore store = Store;
        string secret = new('x', WindowsCredentialStore.MaxSecretBytes + 1);

        await Assert.ThrowsAsync<CredentialStoreException>(
            () => store.SaveAsync("tests:too-large", secret, TestContext.Current.CancellationToken));
    }
}
