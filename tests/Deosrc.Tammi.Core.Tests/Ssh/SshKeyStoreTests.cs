using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Ssh;

namespace Deosrc.Tammi.Core.Tests.Ssh;

public sealed class SshKeyStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));
    private readonly TestProcessLauncher _processes = new();
    private readonly SshKeyStore _store;

    public SshKeyStoreTests()
    {
        _store = new SshKeyStore(_processes, KeyDirectory);
    }

    private string KeyDirectory => Path.Combine(_root, ".ssh");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ListAsync_NoFolder_IsEmpty()
    {
        Assert.Empty(await _store.ListAsync(CancellationToken));
    }

    [Fact]
    public async Task ListAsync_ListsPairsByName_AndSkipsLonePublicKeysAndOtherFiles()
    {
        Directory.CreateDirectory(KeyDirectory);
        WritePair("work", "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz work@example.com");
        WritePair("id_rsa", "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQ== ");
        File.WriteAllText(Path.Combine(KeyDirectory, "lonely.pub"), "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5 x");
        WritePair("broken", "not a key");
        File.WriteAllText(Path.Combine(KeyDirectory, "known_hosts"), "");

        IReadOnlyList<SshKey> keys = await _store.ListAsync(CancellationToken);

        Assert.Equal(["id_rsa", "work"], keys.Select(key => key.Name));
        Assert.Equal("work@example.com", keys[1].Comment);
        Assert.Equal(Path.Combine(KeyDirectory, "work"), keys[1].PrivateKeyPath);
        Assert.Null(keys[0].Comment);
    }

    [Fact]
    public void SuggestName_SkipsNamesThatAreTaken()
    {
        Assert.Equal("id_ed25519", _store.SuggestName(SshKeyType.Ed25519));
        Assert.Equal("id_rsa", _store.SuggestName(SshKeyType.Rsa));

        Directory.CreateDirectory(KeyDirectory);
        File.WriteAllText(Path.Combine(KeyDirectory, "id_ed25519"), "");
        File.WriteAllText(Path.Combine(KeyDirectory, "id_ed25519_2.pub"), "");

        Assert.Equal("id_ed25519_3", _store.SuggestName(SshKeyType.Ed25519));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("..")]
    [InlineData("keys/work")]
    [InlineData("keys\\work")]
    [InlineData("work.pub")]
    public void CheckName_RejectsUnusableNames(string? name)
    {
        Assert.NotNull(_store.CheckName(name));
    }

    [Fact]
    public void CheckName_RejectsTakenNames_AndAcceptsOthers()
    {
        Directory.CreateDirectory(KeyDirectory);
        File.WriteAllText(Path.Combine(KeyDirectory, "config"), "");

        Assert.Contains("already", _store.CheckName("config"));
        Assert.Null(_store.CheckName("id_work"));
    }

    [Theory]
    [InlineData(SshKeyType.Ed25519, "ssh-ed25519", null)]
    [InlineData(SshKeyType.Ed25519, "ssh-ed25519", "correct horse battery staple")]
    [InlineData(SshKeyType.Rsa, "ssh-rsa", null)]
    public async Task GenerateAsync_WritesAKeyPair_ThatIsListed(SshKeyType type, string algorithm, string? passphrase)
    {
        SkipWithoutKeygen();

        SshKey key = await _store.GenerateAsync(
            new SshKeyRequest("id_test", type) { Comment = " ada@example.com ", Passphrase = passphrase },
            CancellationToken);

        Assert.Equal("id_test", key.Name);
        Assert.Equal(algorithm, key.Algorithm);
        Assert.Equal("ada@example.com", key.Comment);
        Assert.True(File.Exists(key.PrivateKeyPath));
        Assert.True(File.Exists(key.PublicKeyPath));
        Assert.Equal(key, Assert.Single(await _store.ListAsync(CancellationToken)));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(KeyDirectory));
        }

        // The fingerprint matches ssh-keygen's, and the passphrase is needed to read the private key.
        ProcessResult fingerprint = await _processes.RunAsync(
            new ProcessRequest("ssh-keygen", "-l", "-E", "sha256", "-f", key.PublicKeyPath), CancellationToken);
        Assert.Contains(key.Fingerprint + " ", fingerprint.StandardOutput);
        ProcessResult withoutPassphrase = await _processes.RunAsync(
            new ProcessRequest("ssh-keygen", "-y", "-P", "", "-f", key.PrivateKeyPath), CancellationToken);
        Assert.Equal(passphrase is null, withoutPassphrase.Succeeded);
    }

    [Fact]
    public async Task GenerateAsync_TakenName_ThrowsWithoutChangingTheFile()
    {
        SkipWithoutKeygen();
        Directory.CreateDirectory(KeyDirectory);
        string existing = Path.Combine(KeyDirectory, "id_ed25519");
        File.WriteAllText(existing, "keep me");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store.GenerateAsync(new SshKeyRequest("id_ed25519", SshKeyType.Ed25519), CancellationToken));

        Assert.Equal("keep me", File.ReadAllText(existing));
    }

    [Fact]
    public async Task GenerateAsync_NoKeygen_Throws()
    {
        var store = new SshKeyStore(new NoExecutables(), KeyDirectory);

        Assert.False(store.CanGenerate);
        await Assert.ThrowsAsync<SshKeygenNotFoundException>(() =>
            store.GenerateAsync(new SshKeyRequest("id_ed25519", SshKeyType.Ed25519), CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_DeletesBothFiles()
    {
        Directory.CreateDirectory(KeyDirectory);
        WritePair("work", "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz");
        WritePair("other", "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz");
        SshKey key = (await _store.ListAsync(CancellationToken)).Single(key => key.Name == "work");

        await _store.RemoveAsync(key, CancellationToken);

        Assert.False(File.Exists(key.PrivateKeyPath));
        Assert.False(File.Exists(key.PublicKeyPath));
        Assert.Equal("other", Assert.Single(await _store.ListAsync(CancellationToken)).Name);
    }

    private void WritePair(string name, string publicKey)
    {
        File.WriteAllText(Path.Combine(KeyDirectory, name), "private");
        File.WriteAllText(Path.Combine(KeyDirectory, name + ".pub"), publicKey + "\n");
    }

    private void SkipWithoutKeygen()
    {
        if (!_store.CanGenerate)
        {
            Assert.Skip("ssh-keygen isn't installed.");
        }
    }

    private sealed class NoExecutables : ProcessLauncherBase
    {
        public override string? FindExecutable(string name) => null;

        public override void Open(string target) => throw new NotSupportedException();
    }
}
