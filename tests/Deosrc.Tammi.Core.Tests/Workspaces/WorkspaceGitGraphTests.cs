using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tests.Git;
using Deosrc.Tammi.Core.Tests.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceGitGraphTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly WorkspaceGitGraphs _graphs;

    public WorkspaceGitGraphTests() =>
        _graphs = new WorkspaceGitGraphs(new GitRepositoryReader(), new GitWorkingTree(_repos.Git), new GitSync(_repos.Git), new InMemorySettingsStore());

    public void Dispose()
    {
        _graphs.Dispose();
        _repos.Dispose();
    }

    [Fact]
    public async Task LoadAsync_WorkspaceBranchScope_ShowsOnlyTheWorkspaceBranchAndItsBase()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        await _repos.RunAsync(clone, "switch", "-c", "other");
        await _repos.CommitFileAsync(clone, "other.txt", "other", "On other");
        await _repos.RunAsync(clone, "switch", "main");
        await _repos.CommitFileAsync(clone, "main.txt", "main", "On main");

        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        Assert.Equal(GitGraphScope.WorkspaceBranch, graph.Scope);
        Assert.Equal(["On main", "Initial commit"], Subjects(graph));
        Assert.True(graph.IsComplete);
        Assert.Null(graph.Problem);

        await graph.SetScopeAsync(GitGraphScope.Everything);

        Assert.Contains("On other", Subjects(graph));
        Assert.Equal(2, graph.LaneCount);
    }

    [Fact]
    public async Task LoadAsync_LabelsCommitsWithTheirBranchesAndTags()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        await _repos.RunAsync(clone, "tag", "v1");

        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        Assert.Equal(
            ["main", "origin/main", "v1"],
            graph.LabelsFor(graph.Rows[0].Commit.Sha).Select(reference => reference.Name));
        Assert.Equal("main", graph.Refs.HeadBranch);
    }

    [Fact]
    public async Task SyncAsync_Push_UpdatesTheGraphAndLeavesNoProblem()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        await _repos.CommitFileAsync(clone, "a.txt", "a", "Add a");
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        bool worked = await graph.SyncAsync(GitSyncOperation.Push);

        Assert.True(worked);
        Assert.Null(graph.SyncProblem);
        Assert.Null(graph.Syncing);
        Assert.Equal(graph.Rows[0].Commit.Sha, graph.Refs.References.Single(r => r.Name == "origin/main").TargetSha);
    }

    [Fact]
    public async Task SyncAsync_Failure_KeepsTheProblemForTheView()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        await _repos.RunAsync(clone, "remote", "set-url", "origin", _repos.PathTo("missing.git"));
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        bool worked = await graph.SyncAsync(GitSyncOperation.Fetch);

        Assert.False(worked);
        Assert.Equal(GitSyncFailure.Network, graph.SyncProblem?.Failure);
    }

    [Fact]
    public async Task RefreshIfChangedAsync_ReadsTheGraphAgainOnlyWhenTheRefsChange()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();
        await graph.SelectAsync(graph.Rows[0].Commit.Sha);
        IReadOnlyList<GitGraphRow> before = graph.Rows;

        TempRepositories.WriteFile(clone, "uncommitted.txt", "not a commit");
        await graph.RefreshIfChangedAsync();
        Assert.Same(before, graph.Rows);

        await _repos.CommitFileAsync(clone, "a.txt", "a", "Add a");
        await graph.RefreshIfChangedAsync();

        Assert.Equal(["Add a", "Initial commit"], Subjects(graph));
        Assert.Equal("Initial commit", graph.Selected?.Commit.Subject);
    }

    [Fact]
    public async Task SelectAsync_ReadsTheCommitsChangedFiles()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        await _repos.CommitFileAsync(workspace.GetWorktreePath(repository), "a.txt", "a", "Add a");
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        await graph.SelectAsync(graph.Rows[0].Commit.Sha);

        Assert.Equal("Add a", graph.Selected?.Commit.Subject);
        Assert.Equal([new CommitFileChange("a.txt", FileChangeKind.Added)], graph.SelectedChanges);

        await graph.SelectAsync(null);

        Assert.Null(graph.Selected);
        Assert.Null(graph.SelectedChanges);
    }

    [Fact]
    public async Task LoadAsync_Submodule_ReadsTheSubmodulesHistory()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        await _repos.CloneAsync(remote, "app", "--recurse-submodules");
        var repository = new WorkspaceRepository("app", remote, "main", "origin/main");
        Workspace workspace = WorkspaceAt(repository);

        WorkspaceGitGraph graph = _graphs.Open(workspace, repository, "lib");
        await graph.LoadAsync();

        Assert.Equal("Git: app/lib", graph.Name);
        Assert.Equal(Path.Combine(_repos.Root, "app", "lib"), graph.Path);
        Assert.Equal(["Initial commit"], Subjects(graph));
        Assert.Null(graph.Problem);
    }

    [Fact]
    public async Task LoadAsync_NoRepository_SaysWhy()
    {
        var repository = new WorkspaceRepository("missing", _repos.PathTo("missing.git"), "main", "origin/main");
        Directory.CreateDirectory(_repos.PathTo("missing"));

        WorkspaceGitGraph graph = _graphs.Open(WorkspaceAt(repository), repository);
        await graph.LoadAsync();

        Assert.NotNull(graph.Problem);
        Assert.Empty(graph.Rows);
        Assert.True(graph.IsComplete);
    }

    [Fact]
    public async Task Calls_AfterClosing_DoNothing()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        _graphs.Close(graph);

        await graph.LoadAsync();
        await graph.LoadMoreAsync();

        Assert.Empty(graph.Rows);
    }

    [Fact]
    public async Task SelectWorkingChangesAsync_StageAndCommit_AddsTheCommitAndClearsTheChanges()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        TempRepositories.WriteFile(clone, "a.txt", "a");
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        await graph.SelectWorkingChangesAsync();

        Assert.True(graph.IsWorkingChangesSelected);
        Assert.Equal("a.txt", Assert.Single(graph.UnstagedChanges).Path);
        Assert.Empty(graph.StagedChanges);
        Assert.Equal(graph.Rows[0].Commit.Sha, graph.HeadSha);

        await graph.StageAsync(graph.UnstagedChanges);

        Assert.Empty(graph.UnstagedChanges);
        Assert.Equal("a.txt", Assert.Single(graph.StagedChanges).Path);

        Assert.True(await graph.CommitAsync("Add a"));

        Assert.Equal("Add a", graph.Rows[0].Commit.Subject);
        Assert.Empty(graph.WorkingChanges!);
        Assert.False(graph.IsWorkingChangesSelected);
        Assert.Equal(graph.Rows[0].Commit.Sha, graph.Selected?.Commit.Sha);
        Assert.Null(graph.WorkingChangesProblem);
    }

    [Fact]
    public async Task CommitAsync_NothingStaged_ReportsTheProblem()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();
        await graph.SelectWorkingChangesAsync();

        Assert.False(await graph.CommitAsync("Nothing"));

        Assert.NotNull(graph.WorkingChangesProblem);
    }

    [Fact]
    public async Task OpenDiffAsync_ShowsTheDiffUntilItIsClosed()
    {
        (Workspace workspace, WorkspaceRepository repository) = await CreateWorkspaceAsync();
        string clone = workspace.GetWorktreePath(repository);
        TempRepositories.WriteFile(clone, "README.md", "changed\n");
        WorkspaceGitGraph graph = _graphs.Open(workspace, repository);
        await graph.LoadAsync();

        await graph.OpenDiffAsync(GitDiffSource.Unstaged, "README.md");

        Assert.NotNull(graph.ActiveDiff);
        Assert.NotEmpty(graph.ActiveDiff.Hunks);

        graph.CloseDiff();

        Assert.Null(graph.ActiveDiff);
        Assert.Null(graph.DiffTarget);
    }

    [Fact]
    public async Task SubmoduleOnANewCommit_CanBeStagedUnstagedAndCommitted()
    {
        (WorkspaceGitGraph graph, _) = await OpenParentOfMovedSubmoduleAsync();

        FileChange change = Assert.Single(graph.UnstagedChanges);
        Assert.Equal("lib", change.Path);
        Assert.True(change.IsSubmodule);
        Assert.Empty(graph.StagedChanges);

        await graph.StageAsync([change]);

        Assert.Empty(graph.UnstagedChanges);
        Assert.Equal("lib", Assert.Single(graph.StagedChanges).Path);

        await graph.UnstageAsync(graph.StagedChanges);

        Assert.Equal("lib", Assert.Single(graph.UnstagedChanges).Path);

        await graph.StageAsync(graph.UnstagedChanges);
        Assert.True(await graph.CommitAsync("Update lib"));

        Assert.Empty(graph.WorkingChanges!);
        string head = graph.Rows[0].Commit.Sha;
        Assert.Contains(new GitRepositoryReader().ReadCommitChanges(graph.Path, head), c => c.Path == "lib");
        Assert.Equal("Update lib", graph.Rows[0].Commit.Subject);
    }

    [Fact]
    public async Task SubmoduleWithOnlyUncommittedFiles_HasNothingToStage()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(remote, "app", "--recurse-submodules");
        TempRepositories.WriteFile(Path.Combine(app, "lib"), "scratch.txt", "scratch");
        var repository = new WorkspaceRepository("app", remote, "main", "origin/main");
        WorkspaceGitGraph graph = _graphs.Open(WorkspaceAt(repository), repository);
        await graph.LoadAsync();
        await graph.SelectWorkingChangesAsync();

        Assert.Empty(graph.WorkingChanges!);
    }

    [Fact]
    public async Task OpenDiffAsync_SubmodulePointer_ShowsTheOldAndNewCommits()
    {
        (WorkspaceGitGraph graph, string lib) = await OpenParentOfMovedSubmoduleAsync();
        string newSha = new GitRepositoryReader().ReadHistory(lib, new HistoryOptions { MaxCount = 1 })[0].Sha;

        await graph.OpenDiffAsync(GitDiffSource.Unstaged, "lib");

        DiffHunk hunk = Assert.Single(graph.ActiveDiff!.Hunks);
        Assert.Contains(hunk.Lines, l => l.Kind == DiffLineKind.Removed && l.Text.StartsWith("Subproject commit "));
        Assert.Contains(hunk.Lines, l => l.Kind == DiffLineKind.Added && l.Text == $"Subproject commit {newSha}");

        await graph.StageAsync(graph.UnstagedChanges);
        await graph.OpenDiffAsync(GitDiffSource.Staged, "lib");

        Assert.Contains(graph.ActiveDiff!.Hunks[0].Lines, l => l.Kind == DiffLineKind.Added && l.Text == $"Subproject commit {newSha}");
    }

    // A clone with its submodule checked out, where the submodule has a commit the clone doesn't record yet.
    private async Task<(WorkspaceGitGraph Graph, string Lib)> OpenParentOfMovedSubmoduleAsync()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(remote, "app", "--recurse-submodules");
        string lib = Path.Combine(app, "lib");
        await _repos.CommitFileAsync(lib, "more.txt", "more", "Move lib on");
        var repository = new WorkspaceRepository("app", remote, "main", "origin/main");
        WorkspaceGitGraph graph = _graphs.Open(WorkspaceAt(repository), repository);
        await graph.LoadAsync();
        await graph.SelectWorkingChangesAsync();
        return (graph, lib);
    }

    private static string[] Subjects(WorkspaceGitGraph graph) => [.. graph.Rows.Select(row => row.Commit.Subject)];

    private async Task<(Workspace, WorkspaceRepository)> CreateWorkspaceAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        await _repos.CloneAsync(remote, "clone");
        var repository = new WorkspaceRepository("clone", remote, "main", "origin/main");
        return (WorkspaceAt(repository), repository);
    }

    private Workspace WorkspaceAt(WorkspaceRepository repository) => new()
    {
        Id = "workspace",
        Name = "workspace",
        Folder = _repos.Root,
        Repositories = [repository],
    };
}
