using System.Runtime.Versioning;
using TAMMi.Core.Credentials;

namespace TAMMi.Core.Tests.Credentials;

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
