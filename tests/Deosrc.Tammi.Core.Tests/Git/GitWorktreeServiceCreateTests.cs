using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitWorktreeServiceCreateTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();
    private readonly GitWorktreeService _service;

    public GitWorktreeServiceCreateTests()
    {
        _service = new GitWorktreeService(_repos.Git, _reader);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task CreateWorktree_FetchesAndBranchesFromRemoteDefaultBranch()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "api");

        // A commit pushed after cloning is only in the worktree if Tammi fetches first.
        string seed = _repos.PathTo("api-seed");
        await _repos.CommitFileAsync(seed, "later.txt", "later", "Later commit");
        await _repos.RunAsync(seed, "push", "origin", "main");
        string remoteMain = await RevParseAsync(seed, "HEAD");

        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = Path.Combine(_repos.PathTo("workspace"), "api"),
                BranchName = "task-1-add-thing",
            },
            CancellationToken);

        Assert.Equal(Path.Combine(_repos.PathTo("workspace"), "api"), worktree);
        RepositoryStatus status = _reader.ReadStatus(worktree);
        Assert.Equal("task-1-add-thing", status.Branch);
        Assert.Equal(remoteMain, status.HeadSha);
        Assert.Null(status.Upstream);
        Assert.False(status.IsChanged);
        Assert.Equal(WorktreeHealth.Healthy, _service.CheckWorktree(worktree).Health);

        // The Local Repository's own checkout is left alone.
        Assert.Equal("main", _reader.ReadStatus(clone).Branch);
    }

    [Fact]
    public async Task CreateWorktree_ChosenBase_BranchesFromIt()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string seed = _repos.PathTo("api-seed");
        await _repos.RunAsync(seed, "switch", "-c", "develop");
        await _repos.CommitFileAsync(seed, "develop.txt", "develop", "Develop commit");
        await _repos.RunAsync(seed, "push", "origin", "develop");
        string develop = await RevParseAsync(seed, "HEAD");
        string clone = await _repos.CloneAsync(remote, "api");

        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/api"),
                BranchName = "task-2",
                BaseBranch = "origin/develop",
            },
            CancellationToken);

        Assert.Equal(develop, _reader.ReadStatus(worktree).HeadSha);
    }

    [Fact]
    public async Task CreateWorktree_InitialisesSubmodulesRecursively()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync(nested: true);
        string clone = await _repos.CloneAsync(remote, "app");

        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/app"),
                BranchName = "task-3",
            },
            CancellationToken);

        RepositoryStatus status = _reader.ReadStatus(worktree);
        SubmoduleStatus lib = Assert.Single(status.Submodules);
        Assert.True(lib.IsInitialized);
        SubmoduleStatus core = Assert.Single(lib.Status!.Submodules);
        Assert.True(core.IsInitialized);
        Assert.True(File.Exists(Path.Combine(worktree, "lib", "core", "README.md")));
        Assert.False(status.IsChanged);

        // The Local Repository's submodules weren't checked out, and still aren't.
        Assert.False(Assert.Single(_reader.ReadStatus(clone).Submodules).IsInitialized);
    }

    [Fact]
    public async Task CreateWorktree_WithoutSubmodules_LeavesThemUninitialised()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        string clone = await _repos.CloneAsync(remote, "app");

        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/app"),
                BranchName = "task-4",
                InitSubmodules = false,
            },
            CancellationToken);

        Assert.False(Assert.Single(_reader.ReadStatus(worktree).Submodules).IsInitialized);
    }

    [Fact]
    public async Task CreateWorktree_BranchExists_Throws()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "api");
        await _repos.RunAsync(clone, "branch", "task-5");

        await Assert.ThrowsAsync<GitCommandException>(() => _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/api"),
                BranchName = "task-5",
            },
            CancellationToken));

        Assert.False(Directory.Exists(_repos.PathTo("workspace/api")));
    }

    [Fact]
    public async Task CreateWorktree_UnknownRemote_Throws()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "api");

        await Assert.ThrowsAsync<GitCommandException>(() => _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/api"),
                BranchName = "task-6",
                Remote = "upstream",
            },
            CancellationToken));
    }

    [Fact]
    public async Task GetDefaultBranch_AfterClone_ReadsRemoteHead()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "api");

        Assert.Equal("origin/main", await _service.GetDefaultBranchAsync(clone, cancellationToken: CancellationToken));
    }

    [Fact]
    public async Task GetDefaultBranch_RemoteHeadNotSet_AsksRemote()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        await _repos.RunAsync(remote, "symbolic-ref", "HEAD", "refs/heads/trunk");
        await _repos.RunAsync(_repos.PathTo("api-seed"), "push", "origin", "main:trunk");
        string clone = await _repos.CloneAsync(remote, "api");
        await _repos.RunAsync(clone, "remote", "set-head", "origin", "--delete");

        Assert.Equal("origin/trunk", await _service.GetDefaultBranchAsync(clone, cancellationToken: CancellationToken));
    }

    [Fact]
    public async Task GetDefaultBranch_NoRemote_Throws()
    {
        string repo = _repos.PathTo("local");
        await _repos.RunAsync(_repos.Root, "init", repo);

        await Assert.ThrowsAnyAsync<GitException>(
            () => _service.GetDefaultBranchAsync(repo, cancellationToken: CancellationToken));
    }

    [Theory]
    [InlineData("task-1-add-thing", true)]
    [InlineData("feature/ABC-12", true)]
    [InlineData("", false)]
    [InlineData("-x", false)]
    [InlineData("two words", false)]
    [InlineData("double..dot", false)]
    [InlineData("ends.lock", false)]
    [InlineData("@{-1}", false)]
    public async Task IsValidBranchName(string name, bool expected)
    {
        Assert.Equal(expected, await _service.IsValidBranchNameAsync(name, CancellationToken));
    }

    private async Task<string> RevParseAsync(string repository, string revision) =>
        (await _repos.RunAsync(repository, "rev-parse", revision)).StandardOutput.Trim();
}
