using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

public sealed class GitRepositoryReaderRemotesTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Clone_HasOrigin()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "clone");

        var remotes = _reader.ReadRemotes(clone);

        var origin = Assert.Single(remotes);
        Assert.Equal("origin", origin.Name);
        Assert.Equal(remote, origin.Url);
        Assert.Null(origin.PushUrl);
    }

    [Fact]
    public async Task SeveralRemotes_AreInNameOrderWithPushUrls()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "clone");
        await _repos.RunAsync(clone, "remote", "add", "upstream", "git@github.com:acme/api.git");
        await _repos.RunAsync(clone, "remote", "add", "fork", "https://github.com/me/api.git");
        await _repos.RunAsync(clone, "remote", "set-url", "--push", "fork", "git@github.com:me/api.git");

        var remotes = _reader.ReadRemotes(clone);

        Assert.Equal(
            [
                new GitRemote("fork", "https://github.com/me/api.git", "git@github.com:me/api.git"),
                new GitRemote("origin", remote),
                new GitRemote("upstream", "git@github.com:acme/api.git"),
            ],
            remotes);
    }

    [Fact]
    public async Task NoRemotes_IsEmpty()
    {
        string path = _repos.PathTo("local");
        await _repos.RunAsync(_repos.Root, "init", path);

        Assert.Empty(_reader.ReadRemotes(path));
    }

    [Fact]
    public void NotARepository_Throws()
    {
        string path = _repos.PathTo("plain");
        Directory.CreateDirectory(path);

        Assert.Throws<GitRepositoryNotFoundException>(() => _reader.ReadRemotes(path));
    }
}
