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
