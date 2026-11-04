using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

public sealed class GitWorktreeServiceRepairTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();
    private readonly GitWorktreeService _service;

    public GitWorktreeServiceRepairTests()
    {
        _service = new GitWorktreeService(_repos.Git, _reader);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Check_NewWorktree_IsHealthy()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();

        WorktreeCheck check = _service.CheckWorktree(worktree);

        Assert.Equal(WorktreeHealth.Healthy, check.Health);
        Assert.Equal(Path.Combine(clone, ".git", "worktrees", "app"), check.GitDirectory);
        Assert.Equal(clone, check.RepositoryPath);
        Assert.False(check.IsBroken);
    }

    [Fact]
    public void Check_MissingFolder_IsMissing()
    {
        Assert.Equal(WorktreeHealth.Missing, _service.CheckWorktree(_repos.PathTo("nope")).Health);
    }

    [Fact]
    public async Task Check_CloneOrSubmodule_IsNotAWorktree()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        string plainFolder = _repos.PathTo("plain");
        Directory.CreateDirectory(plainFolder);

        Assert.Equal(WorktreeHealth.NotAWorktree, _service.CheckWorktree(clone).Health);
        Assert.Equal(WorktreeHealth.NotAWorktree, _service.CheckWorktree(Path.Combine(worktree, "lib")).Health);
        Assert.Equal(WorktreeHealth.NotAWorktree, _service.CheckWorktree(plainFolder).Health);
    }

    [Fact]
    public async Task Check_RepositoryDeleted_IsRepositoryMissing()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        TempRepositories.DeleteFolder(clone);

        WorktreeCheck check = _service.CheckWorktree(worktree);

        Assert.Equal(WorktreeHealth.RepositoryMissing, check.Health);
        Assert.Equal(clone, check.RepositoryPath);
        Assert.True(check.IsBroken);
    }

    [Fact]
    public async Task Check_WorktreePruned_IsUnregistered()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        TempRepositories.DeleteFolder(Path.Combine(clone, ".git", "worktrees", "app"));

        Assert.Equal(WorktreeHealth.Unregistered, _service.CheckWorktree(worktree).Health);
    }

    [Fact]
    public async Task RepositoryMoved_RepairPointsWorktreeAndSubmodulesAtNewLocation()
    {
        (string clone, string worktree) = await CreateWorktreeAsync(nested: true);
        TempRepositories.WriteFile(worktree, "work.txt", "in progress");

        // Move it deeper, so relative links from inside it break too.
        string moved = _repos.PathTo(Path.Combine("moved", "deeper", "app"));
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        Directory.Move(clone, moved);
        Assert.Equal(WorktreeHealth.RepositoryMissing, _service.CheckWorktree(worktree).Health);

        WorktreeCheck check = await _service.RepairWorktreeAsync(worktree, moved, CancellationToken);

        Assert.Equal(WorktreeHealth.Healthy, check.Health);
        Assert.Equal(moved, check.RepositoryPath);
        await AssertWorktreeWorksAsync(worktree);
    }

    [Fact]
    public async Task WorktreeMoved_IsMovedAndRepairFixesIt()
    {
        (string clone, string worktree) = await CreateWorktreeAsync(nested: true);
        string moved = _repos.PathTo(Path.Combine("other", "workspace", "app"));
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        Directory.Move(worktree, moved);
        TempRepositories.WriteFile(moved, "work.txt", "in progress");

        Assert.Equal(WorktreeHealth.Moved, _service.CheckWorktree(moved).Health);

        WorktreeCheck check = await _service.RepairWorktreeAsync(moved, cancellationToken: CancellationToken);

        Assert.Equal(WorktreeHealth.Healthy, check.Health);
        Assert.Equal(clone, check.RepositoryPath);
        await AssertWorktreeWorksAsync(moved);
    }

    [Fact]
    public async Task Repair_Healthy_ChangesNothing()
    {
        (_, string worktree) = await CreateWorktreeAsync();
        string libLink = File.ReadAllText(Path.Combine(worktree, "lib", ".git"));

        WorktreeCheck check = await _service.RepairWorktreeAsync(worktree, cancellationToken: CancellationToken);

        Assert.Equal(WorktreeHealth.Healthy, check.Health);
        Assert.Equal(libLink, File.ReadAllText(Path.Combine(worktree, "lib", ".git")));
    }

    [Fact]
    public async Task Repair_WithDifferentRepository_Throws()
    {
        (string clone, string worktree) = await CreateWorktreeAsync();
        string other = await _repos.CloneAsync(await _repos.CreateRemoteAsync("other"), "other");
        TempRepositories.DeleteFolder(clone);

        await Assert.ThrowsAnyAsync<GitException>(
            () => _service.RepairWorktreeAsync(worktree, other, CancellationToken));

        Assert.Equal(WorktreeHealth.RepositoryMissing, _service.CheckWorktree(worktree).Health);
    }

    [Fact]
    public async Task Repair_MissingFolder_Throws()
    {
        await Assert.ThrowsAnyAsync<GitException>(
            () => _service.RepairWorktreeAsync(_repos.PathTo("nope"), cancellationToken: CancellationToken));
    }

    private async Task AssertWorktreeWorksAsync(string worktree)
    {
        // LibGit2Sharp reads the worktree and its submodules.
        RepositoryStatus status = _reader.ReadStatus(worktree);
        Assert.Equal("task-1", status.Branch);
        Assert.Equal("work.txt", Assert.Single(status.Changes).Path);
        SubmoduleStatus lib = Assert.Single(status.Submodules);
        Assert.True(lib.IsInitialized);
        Assert.Equal(SubmoduleCommitChange.None, lib.CommitChange);
        Assert.False(lib.Status!.IsChanged);
        if (lib.Status.Submodules is [SubmoduleStatus core])
        {
            Assert.True(core.IsInitialized);
            Assert.False(core.Status!.IsChanged);
        }

        // So does the git CLI, inside the submodules too.
        GitCommandResult result = await _repos.RunAsync(worktree, "status", "--porcelain", "--ignore-submodules=none");
        Assert.Equal(["?? work.txt"], result.OutputLines);
        await _repos.RunAsync(Path.Combine(worktree, "lib"), "status");
        await _repos.RunAsync(worktree, "submodule", "update", "--init", "--recursive");
    }

    private async Task<(string Clone, string Worktree)> CreateWorktreeAsync(bool nested = false)
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync(nested);
        string clone = await _repos.CloneAsync(remote, "app");
        string worktree = await _service.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = _repos.PathTo("workspace/app"),
                BranchName = "task-1",
            },
            CancellationToken);
        return (clone, worktree);
    }
}
