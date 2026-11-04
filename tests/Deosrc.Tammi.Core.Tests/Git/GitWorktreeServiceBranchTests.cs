using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

public sealed class GitWorktreeServiceBranchTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitWorktreeService _service;

    public GitWorktreeServiceBranchTests()
    {
        _service = new GitWorktreeService(_repos.Git, new GitRepositoryReader());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task NoNewCommits_IsMergedAndPushed()
    {
        (string clone, _) = await CreateWorktreeAsync();

        BranchState state = await _service.GetBranchStateAsync(clone, "task-1", cancellationToken: CancellationToken);

        Assert.Equal(new BranchState(true, true, true), state);
        Assert.True(state.IsSafeToDelete);
    }

    [Fact]
    public async Task UnpushedCommit_IsNeitherMergedNorPushed()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        await _repos.CommitFileAsync(worktree, "new.txt", "new", "Unpushed");

        BranchState state = await _service.GetBranchStateAsync(clone, "task-1", cancellationToken: CancellationToken);

        Assert.Equal(new BranchState(true, false, false), state);
        Assert.False(state.IsSafeToDelete);
    }

    [Fact]
    public async Task PushedToOwnBranch_IsPushedButNotMerged()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        await _repos.CommitFileAsync(worktree, "new.txt", "new", "Pushed");
        await _repos.RunAsync(worktree, "push", "origin", "task-1");

        BranchState state = await _service.GetBranchStateAsync(clone, "task-1", cancellationToken: CancellationToken);

        Assert.Equal(new BranchState(true, false, true), state);
        Assert.True(state.IsSafeToDelete);
    }

    [Fact]
    public async Task MergedIntoDefaultBranch_IsMerged()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        await _repos.CommitFileAsync(worktree, "new.txt", "new", "Merged");
        await _repos.RunAsync(worktree, "push", "origin", "task-1:main");
        await _repos.RunAsync(clone, "fetch");

        BranchState state = await _service.GetBranchStateAsync(clone, "task-1", cancellationToken: CancellationToken);

        Assert.True(state.IsMerged);
    }

    [Fact]
    public async Task ChosenMergeTarget_IsUsed()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        await _repos.CommitFileAsync(worktree, "new.txt", "new", "Merged into develop");
        await _repos.RunAsync(worktree, "push", "origin", "task-1:develop");
        await _repos.RunAsync(clone, "fetch");

        Assert.True((await _service.GetBranchStateAsync(clone, "task-1", "origin/develop", CancellationToken)).IsMerged);
        Assert.False((await _service.GetBranchStateAsync(clone, "task-1", "origin/main", CancellationToken)).IsMerged);
    }

    [Fact]
    public async Task UnknownMergeTarget_Throws()
    {
        (string clone, _) = await CreateWorktreeAsync();

        await Assert.ThrowsAsync<GitCommandException>(
            () => _service.GetBranchStateAsync(clone, "task-1", "origin/nope", CancellationToken));
    }

    [Fact]
    public async Task MissingBranch_DoesNotExist()
    {
        (string clone, _) = await CreateWorktreeAsync();

        BranchState state = await _service.GetBranchStateAsync(clone, "nope", cancellationToken: CancellationToken);

        Assert.Equal(BranchState.Missing, state);
        Assert.False(state.IsSafeToDelete);
    }

    [Fact]
    public async Task DeleteBranch_AfterRemovingWorktree_DeletesUnmergedBranch()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        await _repos.CommitFileAsync(worktree, "new.txt", "new", "Unpushed");
        await _service.RemoveWorktreeAsync(clone, worktree, force: true, CancellationToken);

        await _service.DeleteBranchAsync(clone, "task-1", CancellationToken);

        Assert.False((await _service.GetBranchStateAsync(clone, "task-1", cancellationToken: CancellationToken)).Exists);
    }

    [Fact]
    public async Task DeleteBranch_CheckedOutInWorktree_Throws()
    {
        (string clone, _) = await CreateWorktreeAsync();

        await Assert.ThrowsAsync<GitCommandException>(
            () => _service.DeleteBranchAsync(clone, "task-1", CancellationToken));
    }

    private async Task<(string Clone, string Worktree)> CreateWorktreeAsync()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "clone");
        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/api"),
                BranchName = "task-1",
            },
            CancellationToken);
        return (clone, worktree);
    }
}
