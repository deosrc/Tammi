using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitUserConfigTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly string _globalConfig;
    private readonly GitCli _git;
    private readonly GitUserConfig _config;

    public GitUserConfigTests()
    {
        // The repositories' test global config sets an author, so these tests use an empty one of their own.
        _globalConfig = _repos.PathTo("user.gitconfig");
        File.WriteAllText(_globalConfig, "");
        _git = new GitCli(new GitCliOptions
        {
            Environment = new Dictionary<string, string?>
            {
                ["GIT_CONFIG_GLOBAL"] = _globalConfig,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
            },
        });
        _config = new GitUserConfig(_git, _repos.Root);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task GetIdentityAsync_NothingSet_ReturnsNulls()
    {
        GitIdentity identity = await _config.GetIdentityAsync(CancellationToken);

        Assert.Equal(new GitIdentity(null, null), identity);
    }

    [Fact]
    public async Task Set_SavesToTheGlobalConfig_WithoutSurroundingSpaces()
    {
        await _config.SetNameAsync("  Ada Lovelace ", CancellationToken);
        await _config.SetEmailAsync("ada@example.com", CancellationToken);

        Assert.Equal(new GitIdentity("Ada Lovelace", "ada@example.com"), await _config.GetIdentityAsync(CancellationToken));
        Assert.Contains("name = Ada Lovelace", File.ReadAllText(_globalConfig));
    }

    [Fact]
    public async Task Set_Blank_UnsetsTheValue()
    {
        await _config.SetNameAsync("Ada Lovelace", CancellationToken);
        await _config.SetEmailAsync("ada@example.com", CancellationToken);

        await _config.SetNameAsync("  ", CancellationToken);
        await _config.SetEmailAsync(null, CancellationToken);

        Assert.Equal(new GitIdentity(null, null), await _config.GetIdentityAsync(CancellationToken));
    }

    [Fact]
    public async Task Set_BlankWhenNotSet_Succeeds()
    {
        await _config.SetNameAsync("", CancellationToken);

        Assert.Null((await _config.GetIdentityAsync(CancellationToken)).Name);
    }

    [Fact]
    public async Task GetIdentityAsync_IgnoresTheRepositorysOwnConfig()
    {
        string repository = _repos.PathTo("repo");
        await _git.RunAsync(_repos.Root, ["init", repository], cancellationToken: CancellationToken);
        await _git.RunAsync(repository, ["config", "user.name", "Local Name"], cancellationToken: CancellationToken);
        var config = new GitUserConfig(_git, repository);

        Assert.Null((await config.GetIdentityAsync(CancellationToken)).Name);
    }

    [Fact]
    public async Task GetIdentityAsync_GitMissing_Throws()
    {
        var config = new GitUserConfig(new GitCli(new GitCliOptions { ExecutablePath = "tammi-git-does-not-exist" }), _repos.Root);

        await Assert.ThrowsAsync<GitNotInstalledException>(() => config.GetIdentityAsync(CancellationToken));
    }
}
