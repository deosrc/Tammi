using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitWorktreeServiceBranchListTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitWorktreeService _service;

    public GitWorktreeServiceBranchListTests()
    {
        _service = new GitWorktreeService(_repos.Git, new GitRepositoryReader());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task ListBranches_RemoteTrackingThenLocal_WithoutSymbolicRefs()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string seed = _repos.PathTo("api-seed");
        await _repos.RunAsync(seed, "push", "origin", "main:develop");
        string clone = await _repos.CloneAsync(remote, "api");
        await _repos.RunAsync(clone, "branch", "feature");

        var branches = await _service.ListBranchesAsync(clone, Token);

        Assert.Equal(["origin/develop", "origin/main", "feature", "main"], branches);
    }

    [Fact]
    public async Task CommitExists_KnownAndUnknown()
    {
        string clone = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "api");

        Assert.True(await _service.CommitExistsAsync(clone, "origin/main", Token));
        Assert.True(await _service.CommitExistsAsync(clone, "main", Token));
        Assert.False(await _service.CommitExistsAsync(clone, "origin/missing", Token));
        Assert.False(await _service.CommitExistsAsync(clone, "", Token));
        Assert.False(await _service.CommitExistsAsync(clone, "--all", Token));
    }
}
