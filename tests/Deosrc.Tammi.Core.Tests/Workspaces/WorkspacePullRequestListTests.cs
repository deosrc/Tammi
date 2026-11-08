using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Tests.Git;
using Deosrc.Tammi.Core.Tests.Integrations;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspacePullRequestListTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();
    private readonly FakePullRequestSource _github = new("github");
    private readonly IntegrationRegistry _registry;
    private readonly FakeTimeProvider _time = new();

    public WorkspacePullRequestListTests()
    {
        _registry = new IntegrationRegistry([_github]);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Repositories_BeforeTheFirstRead_AreNotLoaded()
    {
        using var list = Create(await CreateWorkspaceAsync(("api", "https://github.example/acme/api.git")));

        WorkspaceRepositoryPullRequests repository = Assert.Single(list.Repositories);
        Assert.False(repository.IsLoaded);
        Assert.Empty(repository.PullRequests);
        Assert.Empty(_github.Calls);
    }

    [Fact]
    public async Task RefreshAsync_AsksForTheWorkspaceBranchFromTheLocalRepositorysRemotes()
    {
        var workspace = await CreateWorkspaceAsync(
            ("api", "https://github.example/acme/api.git"), ("web", "https://other.example/acme/web.git"));
        var pullRequest = _github.Add("task-1", 12);
        using var list = Create(workspace);

        await list.RefreshAsync();

        Assert.Equal([[pullRequest], []], list.Repositories.Select(repository => repository.PullRequests));
        Assert.All(list.Repositories, repository =>
        {
            Assert.True(repository.IsLoaded);
            Assert.Null(repository.Problem);
        });
        Assert.Equal([pullRequest], list.Find("api")?.PullRequests);
        // Remotes are in name order: github, then origin.
        Assert.Contains($"task-1:https://github.example/acme/api.git,{_repos.PathTo("remote-api.git")}", _github.Calls);
    }

    [Fact]
    public async Task RefreshAsync_IncludesPushUrls()
    {
        var workspace = await CreateWorkspaceAsync(("api", "https://other.example/acme/api.git"));
        await _repos.RunAsync(
            workspace.Repositories[0].LocalRepositoryPath,
            "remote", "set-url", "--push", "github", "https://github.example/me/api.git");
        var pullRequest = _github.Add("task-1", 3);
        using var list = Create(workspace);

        await list.RefreshAsync();

        Assert.Equal([pullRequest], Assert.Single(list.Repositories).PullRequests);
    }

    [Fact]
    public async Task RefreshAsync_SourceFails_SaysWhy_AndKeepsThePullRequestsReadBefore()
    {
        var pullRequest = _github.Add("task-1", 12);
        using var list = Create(await CreateWorkspaceAsync(("api", "https://github.example/acme/api.git")));
        await list.RefreshAsync();

        _github.Failure = new IntegrationException("GitHub couldn't be reached.");
        await list.RefreshAsync();

        WorkspaceRepositoryPullRequests repository = Assert.Single(list.Repositories);
        Assert.Equal([pullRequest], repository.PullRequests);
        Assert.Equal("Couldn't read the pull requests from Source github. GitHub couldn't be reached.", repository.Problem);

        _github.Failure = null;
        await list.RefreshAsync();

        Assert.Null(Assert.Single(list.Repositories).Problem);
    }

    [Fact]
    public async Task RefreshAsync_LocalRepositoryMissing_HasNoPullRequests_AndAsksNothing()
    {
        var workspace = await CreateWorkspaceAsync(("api", "https://github.example/acme/api.git"));
        workspace = workspace with
        {
            Repositories = [workspace.Repositories[0] with { LocalRepositoryPath = _repos.PathTo("missing") }],
        };
        using var list = Create(workspace);

        await list.RefreshAsync();

        WorkspaceRepositoryPullRequests repository = Assert.Single(list.Repositories);
        Assert.True(repository.IsLoaded);
        Assert.Empty(repository.PullRequests);
        Assert.Null(repository.Problem);
        Assert.Empty(_github.Calls);
    }

    [Fact]
    public async Task RefreshAsync_NoPullRequestIntegrations_HasNoPullRequests()
    {
        var workspace = await CreateWorkspaceAsync(("api", "https://github.example/acme/api.git"));
        using var list = new WorkspacePullRequestList(workspace, new IntegrationRegistry([]), _reader, _time);

        await list.RefreshAsync();

        Assert.True(Assert.Single(list.Repositories).IsLoaded);
    }

    [Fact]
    public async Task Start_ReadsNow_AndAgainEveryRefreshInterval_UntilDisposed()
    {
        var list = Create(await CreateWorkspaceAsync(("api", "https://github.example/acme/api.git")));

        await WaitForAsync(list, () => _github.Calls.Count == 1, list.Start);
        _github.Add("task-1", 5);
        await WaitForAsync(
            list,
            () => list.Repositories[0].PullRequests.Count == 1,
            () => _time.Advance(list.RefreshInterval));

        Assert.Equal(2, _github.Calls.Count);

        list.Dispose();
        _time.Advance(list.RefreshInterval * 3);

        Assert.Equal(2, _github.Calls.Count);
    }

    [Fact]
    public async Task RefreshAsync_CancelsAReadUnderWay()
    {
        using var list = Create(await CreateWorkspaceAsync(("api", "https://github.example/acme/api.git")));
        _github.Gate = new TaskCompletionSource();
        Task first = list.RefreshAsync();
        await WaitUntilAsync(() => _github.Calls.Count == 1);

        _github.Gate = null;
        _github.Add("task-1", 8);
        await list.RefreshAsync();
        await first.WaitAsync(Timeout, CancellationToken);

        Assert.Equal([8], Assert.Single(list.Repositories).PullRequests.Select(pullRequest => pullRequest.Number));
    }

    private WorkspacePullRequestList Create(Workspace workspace) => new(workspace, _registry, _reader, _time);

    /// <summary>
    /// A workspace with a Local Repository for each folder, cloned from a local remote and with a <c>github</c> remote
    /// at the URL given. Its worktrees aren't created: only the Local Repositories' remotes are read.
    /// </summary>
    private async Task<Workspace> CreateWorkspaceAsync(params (string Name, string RemoteUrl)[] repositories)
    {
        var workspaceRepositories = new List<WorkspaceRepository>();
        foreach ((string name, string remoteUrl) in repositories)
        {
            string remote = await _repos.CreateRemoteAsync($"remote-{name}");
            string clone = await _repos.CloneAsync(remote, name);
            await _repos.RunAsync(clone, "remote", "add", "github", remoteUrl);
            workspaceRepositories.Add(new WorkspaceRepository(name, clone, "task-1", "origin/main"));
        }

        return new Workspace
        {
            Id = Workspace.NewId(),
            Name = "Workspace",
            Folder = _repos.PathTo("workspace"),
            Repositories = workspaceRepositories,
        };
    }

    private static async Task WaitForAsync(WorkspacePullRequestList list, Func<bool> condition, Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged()
        {
            if (condition())
            {
                done.TrySetResult();
            }
        }

        list.Changed += OnChanged;
        try
        {
            action();
            OnChanged();
            await done.Task.WaitAsync(Timeout, CancellationToken);
        }
        finally
        {
            list.Changed -= OnChanged;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(Timeout);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
