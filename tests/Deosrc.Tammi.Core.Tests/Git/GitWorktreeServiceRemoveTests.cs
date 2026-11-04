using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

public sealed class GitWorktreeServiceRemoveTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitWorktreeService _service;

    public GitWorktreeServiceRemoveTests()
    {
        _service = new GitWorktreeService(_repos.Git, new GitRepositoryReader());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Clean_RemovesWorktreeAndKeepsBranch()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();

        await _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken);

        Assert.False(Directory.Exists(worktree));
        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
        Assert.True((await _service.GetBranchStateAsync(clone, "task-1", cancellationToken: CancellationToken)).Exists);
    }

    [Fact]
    public async Task UncommittedChanges_ThrowsAndKeepsWorktree()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        TempRepositories.WriteFile(worktree, "new.txt", "new");

        var ex = await Assert.ThrowsAsync<WorktreeChangedException>(
            () => _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken));

        Assert.Equal("new.txt", Assert.Single(ex.Status.Changes).Path);
        Assert.True(File.Exists(Path.Combine(worktree, "new.txt")));
        Assert.Contains(worktree, await ListWorktreesAsync(clone));
    }

    [Fact]
    public async Task UnpushedCommits_Throws()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        await _repos.CommitFileAsync(worktree, "new.txt", "new", "Unpushed");

        var ex = await Assert.ThrowsAsync<WorktreeChangedException>(
            () => _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken));

        Assert.Equal(1, ex.Status.UnpushedCommitCount);
        Assert.True(Directory.Exists(worktree));
    }

    [Fact]
    public async Task Force_RemovesChangedWorktree()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        TempRepositories.WriteFile(worktree, "new.txt", "new");
        await _repos.CommitFileAsync(worktree, "committed.txt", "committed", "Unpushed");

        await _service.RemoveWorktreeAsync(clone, worktree, force: true, CancellationToken);

        Assert.False(Directory.Exists(worktree));
        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
    }

    [Fact]
    public async Task CleanWithSubmodules_Removes()
    {
        // Git itself refuses to remove a worktree with checked-out submodules without --force.
        (string clone, string worktree) = await CreateWorktreeAsync(withSubmodule: true);

        await _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken);

        Assert.False(Directory.Exists(worktree));
        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
    }

    [Fact]
    public async Task ChangeInSubmodule_Throws()
    {
        (string clone, string worktree) = await CreateWorktreeAsync(withSubmodule: true);
        TempRepositories.WriteFile(Path.Combine(worktree, "lib"), "README.md", "changed\n");

        var ex = await Assert.ThrowsAsync<WorktreeChangedException>(
            () => _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken));

        Assert.Empty(ex.Status.Changes);
        Assert.True(ex.Status.IsChanged);
        Assert.True(Directory.Exists(worktree));
    }

    [Fact]
    public async Task UnpushedCommitInSubmodule_ThrowsAndForceRemoves()
    {
        (string clone, string worktree) = await CreateWorktreeAsync(withSubmodule: true);
        await _repos.CommitFileAsync(Path.Combine(worktree, "lib"), "new.txt", "new", "Submodule commit");

        await Assert.ThrowsAsync<WorktreeChangedException>(
            () => _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken));
        await _service.RemoveWorktreeAsync(clone, worktree, force: true, CancellationToken);

        Assert.False(Directory.Exists(worktree));
    }

    [Fact]
    public async Task FolderAlreadyDeleted_Unregisters()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        TempRepositories.DeleteFolder(worktree);

        await _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken);

        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
    }

    [Fact]
    public async Task RepositoryDeleted_NeedsForce()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        TempRepositories.DeleteFolder(clone);

        await Assert.ThrowsAsync<GitException>(
            () => _service.RemoveWorktreeAsync(clone, worktree, cancellationToken: CancellationToken));
        Assert.True(Directory.Exists(worktree));

        await _service.RemoveWorktreeAsync(clone, worktree, force: true, CancellationToken);
        Assert.False(Directory.Exists(worktree));
    }

    [Fact]
    public async Task NotAWorktree_Throws()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "api");
        string otherClone = await _repos.CloneAsync(remote, "other");

        await Assert.ThrowsAnyAsync<GitException>(
            () => _service.RemoveWorktreeAsync(clone, otherClone, force: true, CancellationToken));

        Assert.True(Directory.Exists(otherClone));
    }

    private async Task<(string Clone, string Worktree)> CreateWorktreeAsync(bool withSubmodule = false)
    {
        string remote = withSubmodule
            ? await _repos.CreateRemoteWithSubmoduleAsync()
            : await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "clone");
        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/repo"),
                BranchName = "task-1",
            },
            CancellationToken);
        return (clone, worktree);
    }

    private async Task<IReadOnlyList<string>> ListWorktreesAsync(string repository)
    {
        GitCommandResult result = await _repos.RunAsync(repository, "worktree", "list", "--porcelain");
        return result.OutputLines
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => Path.GetFullPath(line["worktree ".Length..]))
            .ToArray();
    }
}
