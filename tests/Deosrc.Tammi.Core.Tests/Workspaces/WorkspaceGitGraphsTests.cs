using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tests.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceGitGraphsTests : IDisposable
{
    private static readonly WorkspaceRepository Api = new("api", "/repos/api", "feature", "origin/main");
    private static readonly WorkspaceRepository Web = new("web", "/repos/web", "feature", "origin/main");
    private readonly InMemorySettingsStore _settings = new();
    private readonly WorkspaceGitGraphs _graphs;
    private readonly List<string> _changed = [];

    public WorkspaceGitGraphsTests()
    {
        _graphs = new WorkspaceGitGraphs(new GitRepositoryReader(), new GitWorkingTree(new GitCli()), new GitSync(new GitCli()), new GitTags(new GitCli()), new GitBranches(new GitCli()), new GitStash(new GitCli()), new GitDiffs(new GitRepositoryReader(), new GitCli()), _settings);
        _graphs.Changed += _changed.Add;
    }

    public void Dispose() => _graphs.Dispose();

    [Fact]
    public void Open_NamesTheGraphAfterTheRepositoryAndSubmodule()
    {
        Workspace workspace = WorkspaceWith("one");

        Assert.Equal("Git: api", _graphs.Open(workspace, Api).Name);
        Assert.Equal("Git: api/libs/core", _graphs.Open(workspace, Api, "libs/core").Name);
        Assert.Equal(["one", "one"], _changed);
    }

    [Fact]
    public void OpenRepository_WithAPath_OpensASecondaryGraphInTheRepositorysTab()
    {
        var primary = _graphs.OpenRepository("/repos/api");
        var submodule = _graphs.OpenRepository("/repos/api", "/repos/api/libs/core");

        Assert.True(primary.IsPrimary);
        Assert.False(submodule.IsPrimary);
        Assert.Equal(primary.WorkspaceId, submodule.WorkspaceId);
        Assert.Equal("Git: core", submodule.Name);
        Assert.Same(submodule, _graphs.OpenRepository("/repos/api", "/repos/api/libs/core"));
        Assert.Equal([primary], _graphs.StandaloneGraphs());
        Assert.Equal([primary, submodule], _graphs.For(primary.WorkspaceId));

        _graphs.CloseAll(primary.WorkspaceId);

        Assert.Empty(_graphs.For(primary.WorkspaceId));
        Assert.Null(_graphs.ForRepository("/repos/api"));
    }

    [Fact]
    public void Open_AlreadyOpen_ReturnsTheSameGraph()
    {
        Workspace workspace = WorkspaceWith("one");
        WorkspaceGitGraph first = _graphs.Open(workspace, Api);

        Assert.Same(first, _graphs.Open(workspace, Api));
        Assert.NotSame(first, _graphs.Open(workspace, Web));
        Assert.NotSame(first, _graphs.Open(workspace, Api, "lib"));
        Assert.NotSame(first, _graphs.Open(WorkspaceWith("two"), Api));
        Assert.Equal(3, _graphs.For("one").Count);
    }

    [Fact]
    public void Open_SameRepositoryOfAReloadedWorkspace_ReturnsTheSameGraph()
    {
        WorkspaceGitGraph first = _graphs.Open(WorkspaceWith("one"), Api);

        WorkspaceGitGraph again = _graphs.Open(WorkspaceWith("one"), Api with { });

        Assert.Same(first, again);
        Assert.Single(_graphs.For("one"));
        Assert.Equal(["one"], _changed);
    }

    [Fact]
    public async Task Open_UsesTheScopeFromTheSettings()
    {
        await _settings.UpdateAsync(
            settings => settings with { Git = settings.Git with { GraphScope = GitGraphScope.Everything } },
            TestContext.Current.CancellationToken);

        Assert.Equal(GitGraphScope.Everything, _graphs.Open(WorkspaceWith("one"), Api).Scope);
    }

    [Fact]
    public async Task SetScopeAsync_KeepsTheScopeForGraphsOpenedAfterwards()
    {
        await _graphs.Open(WorkspaceWith("one"), Api).SetScopeAsync(GitGraphScope.Everything);

        Assert.Equal(GitGraphScope.Everything, _settings.Current.Git.GraphScope);
        Assert.Equal(GitGraphScope.Everything, _graphs.Open(WorkspaceWith("two"), Api).Scope);
    }

    [Fact]
    public void For_ListsTheWorkspacesGraphsInTheOrderTheyOpened()
    {
        WorkspaceGitGraph web = _graphs.Open(WorkspaceWith("one"), Web);
        _graphs.Open(WorkspaceWith("two"), Api);
        WorkspaceGitGraph api = _graphs.Open(WorkspaceWith("one"), Api);

        Assert.Equal([web, api], _graphs.For("one"));
        Assert.True(web.StartOrder < api.StartOrder);
    }

    [Fact]
    public void Close_RemovesTheGraph()
    {
        WorkspaceGitGraph graph = _graphs.Open(WorkspaceWith("one"), Api);
        _changed.Clear();

        _graphs.Close(graph);
        _graphs.Close(graph);

        Assert.Empty(_graphs.For("one"));
        Assert.Equal(["one"], _changed);
    }

    [Fact]
    public void CloseAll_ClosesOnlyThatWorkspacesGraphs()
    {
        _graphs.Open(WorkspaceWith("one"), Api);
        _graphs.Open(WorkspaceWith("one"), Web);
        _graphs.Open(WorkspaceWith("two"), Api);

        _graphs.CloseAll("one");

        Assert.Empty(_graphs.For("one"));
        Assert.Single(_graphs.For("two"));
    }

    private static Workspace WorkspaceWith(string id) => new()
    {
        Id = id,
        Name = id,
        Folder = Path.Combine(Path.GetTempPath(), id),
        Repositories = [Api, Web],
    };
}
