using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Tests.Catalogue;
using Deosrc.Tammi.Core.Tests.Git;
using Deosrc.Tammi.Core.Tests.Integrations;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class HomeGitStatusTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly WorkspaceTabs _tabs = new();
    private readonly GitRepositoryReader _reader = new();
    private readonly GitWorktreeService _worktrees;
    private readonly FakePullRequestSource _github = new("github");
    private readonly IntegrationRegistry _registry;
    private readonly FakeTimeProvider _time = new();
    private readonly WorkspaceGitStatusMonitors _monitors;
    private readonly WorkspacePullRequestLists _lists;
    private readonly HomeGitStatus _home;

    public HomeGitStatusTests()
    {
        _worktrees = new GitWorktreeService(_repos.Git, _reader);
        _registry = new IntegrationRegistry([_github]);
        _monitors = new WorkspaceGitStatusMonitors(_tabs, _reader, _worktrees, new FakeFileSystemWatchService());
        _lists = new WorkspacePullRequestLists(_tabs, _registry, _reader, _time);
        _home = new HomeGitStatus(_monitors, _lists, _reader, _worktrees, _registry, _time);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _lists.Dispose();
        _monitors.Dispose();
        _repos.Dispose();
    }

    [Fact]
    public async Task Get_BeforeARefresh_IsNotLoaded()
    {
        var workspace = await CreateWorkspaceAsync("api");

        var summary = _home.Get(workspace);

        Assert.False(summary.IsLoaded);
        Assert.Equal("api", Assert.Single(summary.Repositories).Git.Repository.FolderName);
    }

    [Fact]
    public async Task RefreshAsync_ClosedWorkspace_ListsRepositoriesWithLocalChanges()
    {
        var workspace = await CreateWorkspaceAsync("api", "web", "docs");
        await File.WriteAllTextAsync(Path.Combine(workspace.Folder, "api", "new.txt"), "x", CancellationToken);

        await _home.RefreshAsync([workspace], CancellationToken);

        var summary = _home.Get(workspace);
        Assert.True(summary.IsLoaded);
        Assert.Equal("api", Assert.Single(summary.LocalChanges).FolderName);
        Assert.Empty(summary.Unpushed);
    }

    [Fact]
    public async Task RefreshAsync_BrokenWorktree_HasNoBadges()
    {
        var workspace = await CreateWorkspaceAsync("api");
        Directory.Delete(Path.Combine(workspace.Folder, "api"), recursive: true);

        await _home.RefreshAsync([workspace], CancellationToken);

        var summary = _home.Get(workspace);
        Assert.Empty(summary.LocalChanges);
        Assert.Empty(summary.Unpushed);
    }

    [Fact]
    public async Task RefreshAsync_ReadsPullRequests()
    {
        var workspace = await CreateWorkspaceAsync("api");
        var pullRequest = _github.Add("task-1", 7);

        await _home.RefreshAsync([workspace], CancellationToken);

        var repository = Assert.Single(_home.Get(workspace).Repositories);
        Assert.Equal([pullRequest], repository.PullRequests.PullRequests);
    }

    [Fact]
    public async Task RefreshAsync_UnpushedCommit_IsUnpushedNotLocalChanges()
    {
        var workspace = await CreateWorkspaceAsync("api");
        string path = Path.Combine(workspace.Folder, "api");
        await File.WriteAllTextAsync(Path.Combine(path, "new.txt"), "x", CancellationToken);
        await _repos.RunAsync(path, "add", "new.txt");
        await _repos.RunAsync(path, "commit", "-m", "Add file");

        await _home.RefreshAsync([workspace], CancellationToken);

        var summary = _home.Get(workspace);
        Assert.Empty(summary.LocalChanges);
        Assert.Equal("api", Assert.Single(summary.Unpushed).FolderName);
    }

    [Fact]
    public async Task RefreshAsync_PullRequestChecks_AreListedByState()
    {
        var workspace = await CreateWorkspaceAsync("api");
        _github.Add("task-1", 1);
        _github.Add("task-1", 2);
        _github.Add("task-1", 3);
        PullRequestChecks[] checks = [PullRequestChecks.InProgress, PullRequestChecks.Failed, PullRequestChecks.Succeeded];
        _github.PullRequests["task-1"] =
            [.. _github.PullRequests["task-1"].Select((pullRequest, i) => pullRequest with { Checks = checks[i] })];

        await _home.RefreshAsync([workspace], CancellationToken);

        var summary = _home.Get(workspace);
        Assert.Equal([1], summary.ChecksRunning.Select(pullRequest => pullRequest.Number));
        Assert.Equal([2], summary.ChecksFailed.Select(pullRequest => pullRequest.Number));
    }

    [Fact]
    public async Task RefreshAsync_AllPullRequestsMerged_OnlyWhenThereAreSomeAndAllAreMerged()
    {
        var workspace = await CreateWorkspaceAsync("api");
        await _home.RefreshAsync([workspace], CancellationToken);
        Assert.False(_home.Get(workspace).AllPullRequestsMerged);

        _github.Add("task-1", 1, PullRequestState.Merged);
        await _home.RefreshAsync([workspace], CancellationToken);
        Assert.True(_home.Get(workspace).AllPullRequestsMerged);

        _github.Add("task-1", 2);
        await _home.RefreshAsync([workspace], CancellationToken);
        Assert.False(_home.Get(workspace).AllPullRequestsMerged);
    }

    [Fact]
    public async Task RefreshAsync_BehindUpstream_IsBehindNotLocalChanges()
    {
        var workspace = await CreateWorkspaceAsync("api");
        string path = Path.Combine(workspace.Folder, "api");
        await _repos.RunAsync(path, "branch", "--set-upstream-to=origin/main");
        string other = await _repos.CloneAsync(_repos.PathTo("api.git"), "other");
        await File.WriteAllTextAsync(Path.Combine(other, "up.txt"), "x", CancellationToken);
        await _repos.RunAsync(other, "add", "up.txt");
        await _repos.RunAsync(other, "commit", "-m", "Upstream");
        await _repos.RunAsync(other, "push", "origin", "HEAD:main");
        await _repos.RunAsync(path, "fetch", "origin");

        await _home.RefreshAsync([workspace], CancellationToken);

        var summary = _home.Get(workspace);
        Assert.Equal("api", Assert.Single(summary.Behind).FolderName);
        Assert.Empty(summary.LocalChanges);
        Assert.Empty(summary.Unpushed);
    }

    [Fact]
    public async Task RefreshAsync_DraftPullRequests_AreListed()
    {
        var workspace = await CreateWorkspaceAsync("api");
        _github.Add("task-1", 1, PullRequestState.Draft);
        _github.Add("task-1", 2);

        await _home.RefreshAsync([workspace], CancellationToken);

        Assert.Equal([1], _home.Get(workspace).Drafts.Select(pullRequest => pullRequest.Number));
    }

    [Fact]
    public async Task RefreshAsync_SeesChangesMadeSinceTheLastRead()
    {
        var workspace = await CreateWorkspaceAsync("api");
        await _home.RefreshAsync([workspace], CancellationToken);
        Assert.Empty(_home.Get(workspace).LocalChanges);

        await File.WriteAllTextAsync(Path.Combine(workspace.Folder, "api", "new.txt"), "x", CancellationToken);
        await _home.RefreshAsync([workspace], CancellationToken);

        Assert.Single(_home.Get(workspace).LocalChanges);
    }

    [Fact]
    public async Task OpenWorkspace_IsReadFromItsMonitor()
    {
        var workspace = await CreateWorkspaceAsync("api");
        _tabs.Open(workspace.Id, workspace.Name);
        var monitor = _monitors.Start(workspace);
        await monitor.RefreshAsync(CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(workspace.Folder, "api", "new.txt"), "x", CancellationToken);
        await monitor.RefreshAsync(CancellationToken);

        // No refresh of Home's own: the live monitor's status is used.
        Assert.Single(_home.Get(workspace).LocalChanges);
    }

    [Fact]
    public async Task Observe_LiveWorkspace_IsToldOfChanges_UntilDisposed()
    {
        var workspace = await CreateWorkspaceAsync("api");
        _tabs.Open(workspace.Id, workspace.Name);
        var monitor = _monitors.Start(workspace);
        await monitor.RefreshAsync(CancellationToken);
        int calls = 0;

        using (_home.Observe([workspace], () => Interlocked.Increment(ref calls)))
        {
            await monitor.RefreshAsync(CancellationToken);
            Assert.True(calls > 0);
        }

        int after = calls;
        await monitor.RefreshAsync(CancellationToken);
        Assert.Equal(after, calls);
    }

    private async Task<Workspace> CreateWorkspaceAsync(params string[] names)
    {
        string folder = _repos.PathTo("workspace");
        var repositories = new List<WorkspaceRepository>();
        foreach (string name in names)
        {
            string remote = await _repos.CreateRemoteAsync(name);
            string clone = await _repos.CloneAsync(remote, name);
            await _repos.RunAsync(clone, "remote", "add", "github", $"https://github.example/acme/{name}.git");
            await _worktrees.CreateWorktreeAsync(
                new CreateWorktreeRequest
                {
                    RepositoryPath = clone,
                    WorktreePath = Path.Combine(folder, name),
                    BranchName = "task-1",
                },
                CancellationToken);
            repositories.Add(new WorkspaceRepository(name, clone, "task-1", "origin/main"));
        }

        return new Workspace
        {
            Id = Workspace.NewId(),
            Name = "Workspace",
            Folder = folder,
            Repositories = repositories,
        };
    }
}
