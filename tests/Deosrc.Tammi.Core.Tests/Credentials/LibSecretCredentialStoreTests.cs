using System.Runtime.Versioning;
using TAMMi.Core.Credentials;

namespace TAMMi.Core.Tests.Credentials;

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
