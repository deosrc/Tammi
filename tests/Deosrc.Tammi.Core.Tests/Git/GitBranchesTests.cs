using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitBranchesTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitBranches _branches;

    public GitBranchesTests() => _branches = new GitBranches(_repos.Git);

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
    public async Task CreateAsync_WithoutSwitching_LeavesHeadAlone()
    {
        var (_, mine) = await CloneAsync();

        await _branches.CreateAsync(mine, "feature", "HEAD", checkout: false, Token);

        Assert.Equal("feature", await GitAsync(mine, "branch", "--list", "--format=%(refname:short)", "feature"));
        Assert.Equal("main", await GitAsync(mine, "branch", "--show-current"));
    }

    [Fact]
    public async Task CreateAsync_Switching_ChecksOutTheNewBranch()
    {
        var (_, mine) = await CloneAsync();

        await _branches.CreateAsync(mine, "feature", "HEAD", checkout: true, Token);

        Assert.Equal("feature", await GitAsync(mine, "branch", "--show-current"));
    }

    [Fact]
    public async Task CreateAsync_ExistingName_Throws()
    {
        var (_, mine) = await CloneAsync();

        await Assert.ThrowsAsync<GitCommandException>(() => _branches.CreateAsync(mine, "main", "HEAD", false, Token));
    }

    [Fact]
    public async Task MergeAsync_DivergedBranch_MakesMergeCommit()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", checkout: true, Token);
        await _repos.CommitFileAsync(mine, "feature.txt", "f", "feature work");
        await _branches.CheckoutAsync(mine, "main", Token);
        await _repos.CommitFileAsync(mine, "main.txt", "m", "main work");

        await _branches.MergeAsync(mine, "feature", Token);

        Assert.Equal("main", await GitAsync(mine, "branch", "--show-current"));
        Assert.Equal("2", await GitAsync(mine, "rev-list", "--parents", "-1", "HEAD").ContinueWith(
            r => (r.Result.Split(' ').Length - 1).ToString(), TaskScheduler.Default));
        Assert.True(File.Exists(Path.Combine(mine, "feature.txt")));
    }

    [Fact]
    public async Task RebaseAsync_PutsCurrentBranchOnTopOfTheOther()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", checkout: true, Token);
        await _repos.CommitFileAsync(mine, "feature.txt", "f", "feature work");
        await _branches.CheckoutAsync(mine, "main", Token);
        await _repos.CommitFileAsync(mine, "main.txt", "m", "main work");

        await _branches.RebaseAsync(mine, "feature", Token);

        Assert.Equal("main", await GitAsync(mine, "branch", "--show-current"));
        Assert.Equal("main work", await GitAsync(mine, "log", "-1", "--format=%s"));
        Assert.Equal("feature work", await GitAsync(mine, "log", "-1", "--format=%s", "HEAD~1"));
    }

    [Fact]
    public async Task MergeAsync_Conflict_Throws()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", checkout: true, Token);
        await _repos.CommitFileAsync(mine, "same.txt", "a", "feature");
        await _branches.CheckoutAsync(mine, "main", Token);
        await _repos.CommitFileAsync(mine, "same.txt", "b", "main");

        await Assert.ThrowsAsync<GitCommandException>(() => _branches.MergeAsync(mine, "feature", Token));
    }

    [Fact]
    public async Task SwitchAsync_LocalBranch()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", false, Token);

        await _branches.CheckoutAsync(mine, "feature", Token);

        Assert.Equal("feature", await GitAsync(mine, "branch", "--show-current"));
    }

    [Fact]
    public async Task SwitchAsync_RemoteTrackingBranch_CreatesATrackingBranch()
    {
        var (remote, mine) = await CloneAsync();
        await GitAsync(remote, "branch", "feature");
        await GitAsync(mine, "fetch", "origin");

        await _branches.CheckoutAsync(mine, "origin/feature", Token);

        Assert.Equal("feature", await GitAsync(mine, "branch", "--show-current"));
        Assert.Equal(new GitUpstream("origin", "feature"), await _branches.GetUpstreamAsync(mine, "feature", Token));
    }

    [Fact]
    public async Task RenameAsync_MovesTheBranch()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "old", "HEAD", true, Token);

        await _branches.RenameAsync(mine, "old", "new", Token);

        Assert.Equal("new", await GitAsync(mine, "branch", "--show-current"));
    }

    [Fact]
    public async Task GetUpstreamAsync_NoUpstream_ReturnsNull()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", false, Token);

        Assert.Null(await _branches.GetUpstreamAsync(mine, "feature", Token));
    }

    [Fact]
    public async Task DeleteAsync_Both_RemovesLocalAndRemote()
    {
        var (remote, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", false, Token);
        await GitAsync(mine, "push", "--set-upstream", "origin", "feature");

        await _branches.DeleteAsync(mine, "feature", deleteLocal: true, deleteRemote: true, Token);

        Assert.Equal("", await GitAsync(mine, "branch", "--list", "feature"));
        Assert.Equal("", await GitAsync(remote, "branch", "--list", "feature"));
    }

    [Fact]
    public async Task DeleteAsync_LocalOnly_KeepsTheRemoteBranch()
    {
        var (remote, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", false, Token);
        await GitAsync(mine, "push", "--set-upstream", "origin", "feature");

        await _branches.DeleteAsync(mine, "feature", deleteLocal: true, deleteRemote: false, Token);

        Assert.Equal("", await GitAsync(mine, "branch", "--list", "feature"));
        Assert.NotEqual("", await GitAsync(remote, "branch", "--list", "feature"));
    }

    [Fact]
    public async Task DeleteAsync_RemoteOnly_KeepsTheLocalBranch()
    {
        var (remote, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", false, Token);
        await GitAsync(mine, "push", "--set-upstream", "origin", "feature");

        await _branches.DeleteAsync(mine, "feature", deleteLocal: false, deleteRemote: true, Token);

        Assert.NotEqual("", await GitAsync(mine, "branch", "--list", "feature"));
        Assert.Equal("", await GitAsync(remote, "branch", "--list", "feature"));
    }

    [Fact]
    public async Task DeleteAsync_RemoteWithoutUpstream_ThrowsAndKeepsTheLocalBranch()
    {
        var (_, mine) = await CloneAsync();
        await _branches.CreateAsync(mine, "feature", "HEAD", false, Token);

        await Assert.ThrowsAsync<GitException>(() => _branches.DeleteAsync(mine, "feature", true, true, Token));

        Assert.NotEqual("", await GitAsync(mine, "branch", "--list", "feature"));
    }

    [Fact]
    public async Task DeleteRemoteAsync_RemovesTheRemoteBranch()
    {
        var (remote, mine) = await CloneAsync();
        await GitAsync(remote, "branch", "feature");
        await GitAsync(mine, "fetch", "origin");

        await _branches.DeleteRemoteAsync(mine, "origin/feature", Token);

        Assert.Equal("", await GitAsync(remote, "branch", "--list", "feature"));
    }
}
