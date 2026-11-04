using Tammi.Core.Git;
using Tammi.Core.Tests.Git;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceDeleterTests : IDisposable
{
    private const string Branch = "42-fix-login";

    private readonly TempRepositories _repos = new();
    private readonly GitWorktreeService _worktrees;
    private readonly WorkspaceStore _store;
    private readonly WorkspaceDeleter _deleter;

    public WorkspaceDeleterTests()
    {
        var reader = new GitRepositoryReader();
        _worktrees = new GitWorktreeService(_repos.Git, reader);
        _store = new WorkspaceStore(_repos.PathTo("data"), [], []);
        _deleter = new WorkspaceDeleter(_store, _worktrees, reader);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Folder => _repos.PathTo("workspaces/Fix login");

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task UnknownWorkspace_HasNoPlan()
    {
        Assert.Null(await _deleter.PlanAsync("unknown", Token));
    }

    [Fact]
    public async Task CleanWorkspace_DeletesFolderWorktreeAndIndexEntry()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        string worktree = Path.Combine(Folder, "api");

        var plan = await PlanAsync(workspace);
        Assert.Equal(WorkspaceDeletionScope.Folder, plan.Scope);
        Assert.False(plan.CouldLoseWork);
        var planned = Assert.Single(plan.Worktrees);
        Assert.Equal(worktree, planned.Path);
        Assert.Equal(WorktreeHealth.Healthy, planned.Health);

        var branches = await _deleter.DeleteAsync(plan, Token);

        Assert.False(Directory.Exists(Folder));
        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
        Assert.Null(await _store.FindAsync(workspace.Id, Token));
        var branch = Assert.Single(branches);
        Assert.Equal(Branch, branch.Branch);
        Assert.Equal(clone, branch.LocalRepositoryPath);
        Assert.True(branch.IsWorkspaceBranch);
        Assert.True(branch.SelectedByDefault);
    }

    [Fact]
    public async Task UncommittedChanges_NeedConfirmingAndAreDeleted()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        TempRepositories.WriteFile(Path.Combine(Folder, "api"), "new.txt", "new");

        var plan = await PlanAsync(workspace);
        Assert.True(plan.CouldLoseWork);
        var worktree = Assert.Single(plan.Worktrees);
        Assert.True(worktree.NeedsConfirmation);
        Assert.Equal("new.txt", Assert.Single(worktree.Status!.Changes).Path);

        await _deleter.DeleteAsync(plan, Token);

        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public async Task UnpushedCommit_BranchIsOfferedButNotSelected()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        await _repos.CommitFileAsync(Path.Combine(Folder, "api"), "new.txt", "new", "Unpushed");

        var plan = await PlanAsync(workspace);
        Assert.Equal(1, Assert.Single(plan.Worktrees).Status!.UnpushedCommitCount);

        var branch = Assert.Single(await _deleter.DeleteAsync(plan, Token));

        Assert.False(branch.State.IsPushed);
        Assert.False(branch.SelectedByDefault);
        Assert.True((await _worktrees.GetBranchStateAsync(clone, Branch, cancellationToken: Token)).Exists);
    }

    [Fact]
    public async Task ChangesAfterPlanning_StopsAndKeepsWorkspace()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        var plan = await PlanAsync(workspace);
        TempRepositories.WriteFile(Path.Combine(Folder, "api"), "new.txt", "new");

        await Assert.ThrowsAsync<WorkspaceDeletionException>(() => _deleter.DeleteAsync(plan, Token));

        Assert.True(File.Exists(Path.Combine(Folder, "api", "new.txt")));
        Assert.NotNull(await _store.FindAsync(workspace.Id, Token));
    }

    [Fact]
    public async Task OtherContent_IsListedAndDeleted()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        TempRepositories.WriteFile(Folder, "notes.md", "notes");
        TempRepositories.WriteFile(Folder, "scratch/output.txt", "output");

        var plan = await PlanAsync(workspace);
        Assert.True(plan.CouldLoseWork);
        Assert.Equal([Path.Combine(Folder, "notes.md"), Path.Combine(Folder, "scratch")], plan.OtherContent);

        await _deleter.DeleteAsync(plan, Token);

        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public async Task ContentAddedAfterPlanning_StopsBeforeDeletingFolder()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        var plan = await PlanAsync(workspace);
        TempRepositories.WriteFile(Folder, "notes.md", "notes");

        await Assert.ThrowsAsync<WorkspaceDeletionException>(() => _deleter.DeleteAsync(plan, Token));

        Assert.True(File.Exists(Path.Combine(Folder, "notes.md")));
        Assert.True(File.Exists(WorkspaceStore.GetMetadataFilePath(Folder)));
        Assert.NotNull(await _store.FindAsync(workspace.Id, Token));
    }

    [Fact]
    public async Task FullCloneInFolder_IsOtherContent()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        string remote = _repos.PathTo("api.git");
        await _repos.RunAsync(_repos.Root, "clone", remote, Path.Combine(Folder, "copy"));

        var plan = await PlanAsync(workspace);

        Assert.Equal(Path.Combine(Folder, "copy"), Assert.Single(plan.OtherContent));
        Assert.Single(plan.Worktrees);
    }

    [Fact]
    public async Task UnrelatedBranches_AreNotOfferedOrTouched()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        await _repos.RunAsync(clone, "branch", "unrelated");

        var plan = await PlanAsync(workspace);
        var branches = await _deleter.DeleteAsync(plan, Token);
        await _deleter.DeleteBranchesAsync(branches, Token);

        Assert.False((await _worktrees.GetBranchStateAsync(clone, Branch, cancellationToken: Token)).Exists);
        Assert.True((await _worktrees.GetBranchStateAsync(clone, "unrelated", cancellationToken: Token)).Exists);
        Assert.True((await _worktrees.GetBranchStateAsync(clone, "main", cancellationToken: Token)).Exists);
    }

    [Fact]
    public async Task WorktreeFolderAlreadyDeleted_IsUnregistered()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        string worktree = Path.Combine(Folder, "api");
        TempRepositories.DeleteFolder(worktree);

        var plan = await PlanAsync(workspace);
        Assert.False(plan.CouldLoseWork);
        Assert.Equal(WorktreeHealth.Missing, Assert.Single(plan.Worktrees).Health);

        var branches = await _deleter.DeleteAsync(plan, Token);

        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
        Assert.Single(branches);
    }

    [Fact]
    public async Task LocalRepositoryDeleted_NeedsConfirmingAndIsDeleted()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        TempRepositories.DeleteFolder(clone);

        var plan = await PlanAsync(workspace);
        var worktree = Assert.Single(plan.Worktrees);
        Assert.Equal(WorktreeHealth.RepositoryMissing, worktree.Health);
        Assert.True(worktree.NeedsConfirmation);

        Assert.Empty(await _deleter.DeleteAsync(plan, Token));
        Assert.False(Directory.Exists(Folder));
    }

    [Fact]
    public async Task MissingFolder_RemovesIndexEntryOnly()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        string moved = _repos.PathTo("moved");
        Directory.Move(Folder, moved);

        var plan = await PlanAsync(workspace);
        Assert.Equal(WorkspaceDeletionScope.IndexEntryOnly, plan.Scope);
        Assert.NotNull(plan.Reason);

        Assert.Empty(await _deleter.DeleteAsync(plan, Token));
        Assert.Null(await _store.FindAsync(workspace.Id, Token));
        Assert.True(File.Exists(WorkspaceStore.GetMetadataFilePath(moved)));
    }

    [Fact]
    public async Task FolderHoldsDifferentWorkspace_LeavesItAlone()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        string metadata = WorkspaceStore.GetMetadataFilePath(Folder);
        string otherId = Workspace.NewId();
        await File.WriteAllTextAsync(
            metadata,
            (await File.ReadAllTextAsync(metadata, Token)).Replace(workspace.Id, otherId, StringComparison.Ordinal),
            Token);

        var plan = await PlanAsync(workspace);
        Assert.Equal(WorkspaceDeletionScope.IndexEntryOnly, plan.Scope);

        await _deleter.DeleteAsync(plan, Token);

        Assert.Null(await _store.FindAsync(workspace.Id, Token));
        Assert.True(Directory.Exists(Path.Combine(Folder, "api")));
        Assert.Equal(otherId, (await _store.LoadAsync(Folder, Token)).Id);
    }

    [Fact]
    public async Task CorruptMetadata_FindsWorktreesInFolder()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        string worktree = Path.Combine(Folder, "api");
        await File.WriteAllTextAsync(WorkspaceStore.GetMetadataFilePath(Folder), "not json", Token);

        var plan = await PlanAsync(workspace);
        Assert.Equal(WorkspaceDeletionScope.Folder, plan.Scope);
        Assert.False(plan.MetadataRead);
        var found = Assert.Single(plan.Worktrees);
        Assert.Equal(worktree, found.Path);
        Assert.Null(found.Repository);
        Assert.False(plan.CouldLoseWork);

        var branch = Assert.Single(await _deleter.DeleteAsync(plan, Token));

        Assert.False(Directory.Exists(Folder));
        Assert.DoesNotContain(worktree, await ListWorktreesAsync(clone));
        Assert.Equal(Branch, branch.Branch);
        Assert.False(branch.IsWorkspaceBranch);
        Assert.False(branch.SelectedByDefault);
    }

    [Fact]
    public async Task MetadataBrokenAfterPlanning_Stops()
    {
        (Workspace workspace, _) = await CreateWorkspaceAsync();
        var plan = await PlanAsync(workspace);
        await File.WriteAllTextAsync(WorkspaceStore.GetMetadataFilePath(Folder), "not json", Token);

        await Assert.ThrowsAsync<WorkspaceDeletionException>(() => _deleter.DeleteAsync(plan, Token));

        Assert.True(Directory.Exists(Path.Combine(Folder, "api")));
    }

    [Fact]
    public async Task DeleteBranchesAsync_ReportsFailuresAndContinues()
    {
        (Workspace workspace, string clone) = await CreateWorkspaceAsync();
        var branch = Assert.Single(await _deleter.DeleteAsync(await PlanAsync(workspace), Token));
        var missing = branch with { Branch = "missing" };

        var results = await _deleter.DeleteBranchesAsync([missing, branch], Token);

        Assert.False(results[0].Succeeded);
        Assert.NotNull(results[0].Error);
        Assert.True(results[1].Succeeded);
        Assert.False((await _worktrees.GetBranchStateAsync(clone, Branch, cancellationToken: Token)).Exists);
    }

    private async Task<WorkspaceDeletionPlan> PlanAsync(Workspace workspace) =>
        (await _deleter.PlanAsync(workspace.Id, Token))!;

    private async Task<(Workspace Workspace, string Clone)> CreateWorkspaceAsync()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string clone = await _repos.CloneAsync(remote, "clone");
        await _worktrees.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = clone,
                WorktreePath = Path.Combine(Folder, "api"),
                BranchName = Branch,
            },
            Token);
        var workspace = await _store.SaveAsync(
            new Workspace
            {
                Id = Workspace.NewId(),
                Name = "Fix login",
                Folder = Folder,
                Repositories = [new WorkspaceRepository("api", clone, Branch, "origin/main")],
            },
            Token);
        return (workspace, clone);
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
