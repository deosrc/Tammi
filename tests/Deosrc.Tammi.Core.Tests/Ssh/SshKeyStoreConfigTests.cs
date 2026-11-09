using Deosrc.Tammi.Core.Ssh;

namespace Deosrc.Tammi.Core.Tests.Ssh;

public sealed class SshKeyStoreConfigTests : IDisposable
{
    private const string PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));
    private readonly SshKeyStore _store;

    public SshKeyStoreConfigTests()
    {
        _store = new SshKeyStore(new TestProcessLauncher(), Path.Combine(_root, ".ssh"));
    }

    private string Config => Path.Combine(_root, ".ssh", "config");

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SshKey Key(string name) => new(name, Path.Combine(_root, ".ssh", name), "ssh-ed25519", PublicKey.Split(' ')[1], null);

    private void WriteConfig(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        File.WriteAllText(Config, text);
    }

    [Fact]
    public async Task IsLoadedBySshAsync_DefaultName_IsLoaded_WithoutConfig()
    {
        Assert.True(await _store.IsLoadedBySshAsync(Key("id_ed25519"), CancellationToken));
    }

    [Fact]
    public async Task IsLoadedBySshAsync_CustomName_NoEntry_IsNotLoaded()
    {
        WriteConfig("Host *\n\tIdentityAgent ~/agent.sock\n");
        Assert.False(await _store.IsLoadedBySshAsync(Key("github"), CancellationToken));
    }

    [Theory]
    [InlineData("Host github.com\n\tIdentityFile ~/.ssh/github\n")]
    [InlineData("Host github.com\n\tIdentityFile=\"~/.ssh/github\"\n")]
    [InlineData("Host *\n\tIdentityFile .ssh/github\n")]
    public async Task IsLoadedBySshAsync_CustomName_NamedByIdentityFile_IsLoaded(string config)
    {
        WriteConfig(config);
        Assert.True(await _store.IsLoadedBySshAsync(Key("github"), CancellationToken));
    }

    [Fact]
    public async Task AddHostEntriesAsync_NoConfig_CreatesIt()
    {
        var added = await _store.AddHostEntriesAsync(Key("github"), ["github.com"], CancellationToken);

        Assert.Equal(["github.com"], added);
        string text = File.ReadAllText(Config);
        Assert.Contains("Host github.com", text);
        Assert.Contains("IdentityFile " + Path.Combine(_root, ".ssh", "github").Replace('\\', '/'), text);
        Assert.Contains("IdentitiesOnly yes", text);
        Assert.True(await _store.IsLoadedBySshAsync(Key("github"), CancellationToken));
    }

    [Fact]
    public async Task AddHostEntriesAsync_PlacesEntryBeforeHostStar_AndKeepsContent()
    {
        WriteConfig("Host work\n\tUser me\n\n# Everything\nHost *\n\tIdentityAgent ~/agent.sock\n");

        await _store.AddHostEntriesAsync(Key("github"), ["github.com", "ghe.example.com"], CancellationToken);

        string text = File.ReadAllText(Config);
        int work = text.IndexOf("Host work", StringComparison.Ordinal);
        int github = text.IndexOf("Host github.com", StringComparison.Ordinal);
        int ghe = text.IndexOf("Host ghe.example.com", StringComparison.Ordinal);
        int star = text.IndexOf("Host *", StringComparison.Ordinal);
        Assert.True(work < github && github < ghe && ghe < star);
        Assert.Contains("# Everything\nHost *", text);
        Assert.Contains("IdentityAgent ~/agent.sock", text);
        Assert.Contains("User me", text);
    }

    [Fact]
    public async Task AddHostEntriesAsync_SkipsHostsWithAnEntryForTheKey()
    {
        WriteConfig("Host github.com\n\tIdentityFile ~/.ssh/github\n");

        var added = await _store.AddHostEntriesAsync(Key("github"), ["github.com", "ghe.example.com"], CancellationToken);

        Assert.Equal(["ghe.example.com"], added);
        Assert.Equal(1, File.ReadAllText(Config).Split("Host github.com").Length - 1);

        Assert.Empty(await _store.AddHostEntriesAsync(Key("github"), ["github.com", "ghe.example.com"], CancellationToken));
    }

    [Fact]
    public async Task GetHostsWithoutEntryAsync_OtherKeyOnHost_StillNeedsEntry()
    {
        WriteConfig("Host github.com\n\tIdentityFile ~/.ssh/other\n");

        Assert.Equal(["github.com"], await _store.GetHostsWithoutEntryAsync(Key("github"), ["github.com"], CancellationToken));
    }
}
