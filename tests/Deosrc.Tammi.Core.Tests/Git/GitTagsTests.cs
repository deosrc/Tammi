using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitTagsTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitTags _tags;

    public GitTagsTests() => _tags = new GitTags(_repos.Git);

    public void Dispose() => _repos.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(string Remote, string Mine)> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("origin");
        return (remote, await _repos.CloneAsync(remote, "mine"));
    }

    private async Task<string> GitAsync(string path, params string[] args) =>
        (await _repos.Git.RunAsync(path, args, cancellationToken: Token)).StandardOutput.Trim();

    [Fact]
    public async Task CreateAsync_MakesALightweightTag()
    {
        var (_, mine) = await CloneAsync();

        await _tags.CreateAsync(mine, "v1", "HEAD", Token);

        Assert.Equal("commit", await GitAsync(mine, "cat-file", "-t", "v1"));
    }

    [Fact]
    public async Task PushAsync_SendsTheTag_AndExistsOnRemoteSeesIt()
    {
        var (remote, mine) = await CloneAsync();
        await _tags.CreateAsync(mine, "v1", "HEAD", Token);
        Assert.False(await _tags.ExistsOnRemoteAsync(mine, "v1", Token));

        await _tags.PushAsync(mine, "v1", Token);

        Assert.True(await _tags.ExistsOnRemoteAsync(mine, "v1", Token));
        Assert.Equal("v1", await GitAsync(remote, "tag", "--list", "v1"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheTagLocallyAndOnTheRemote()
    {
        var (remote, mine) = await CloneAsync();
        await _tags.CreateAsync(mine, "v1", "HEAD", Token);
        await _tags.PushAsync(mine, "v1", Token);

        await _tags.DeleteAsync(mine, "v1", Token);

        Assert.Equal("", await GitAsync(mine, "tag", "--list"));
        Assert.Equal("", await GitAsync(remote, "tag", "--list"));
    }

    [Fact]
    public async Task DeleteAsync_WorksForATagThatIsOnlyLocal()
    {
        var (_, mine) = await CloneAsync();
        await _tags.CreateAsync(mine, "v1", "HEAD", Token);

        await _tags.DeleteAsync(mine, "v1", Token);

        Assert.Equal("", await GitAsync(mine, "tag", "--list"));
    }

    [Fact]
    public async Task GetRemoteAsync_ReturnsOrigin()
    {
        var (_, mine) = await CloneAsync();

        Assert.Equal("origin", await _tags.GetRemoteAsync(mine, Token));
    }
}
