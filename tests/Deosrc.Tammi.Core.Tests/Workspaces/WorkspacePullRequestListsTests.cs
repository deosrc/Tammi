using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Tests.Integrations;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspacePullRequestListsTests : IDisposable
{
    private readonly FakePullRequestSource _github = new("github");
    private readonly IntegrationRegistry _registry;
    private readonly WorkspaceTabs _tabs = new();
    private readonly WorkspacePullRequestLists _lists;

    public WorkspacePullRequestListsTests()
    {
        _registry = new IntegrationRegistry([_github]);
        _lists = new WorkspacePullRequestLists(_tabs, _registry, new GitRepositoryReader(), new FakeTimeProvider());
    }

    public void Dispose() => _lists.Dispose();

    [Fact]
    public void Start_WorkspaceNotOpen_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _lists.Start(NewWorkspace()));
    }

    [Fact]
    public void Start_Again_ReturnsTheSameList_UnlessTheRepositoriesChanged()
    {
        var workspace = NewWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);

        var list = _lists.Start(workspace);

        Assert.Same(list, _lists.Start(workspace with { Name = "Renamed" }));
        Assert.Same(list, _lists.Find(workspace.Id));

        var changed = _lists.Start(workspace with
        {
            Repositories = [new WorkspaceRepository("web", "/missing/web", "task-1", "origin/main")],
        });

        Assert.NotSame(list, changed);
        Assert.Throws<ObjectDisposedException>(list.Refresh);
    }

    [Fact]
    public void ClosingTheTab_DropsItsList()
    {
        var workspace = NewWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);
        var list = _lists.Start(workspace);

        _tabs.Close(workspace.Id);

        Assert.Null(_lists.Find(workspace.Id));
        Assert.Throws<ObjectDisposedException>(list.Refresh);
    }

    [Fact]
    public void Dispose_DropsEveryList()
    {
        var workspace = NewWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);
        var list = _lists.Start(workspace);

        _lists.Dispose();

        Assert.Throws<ObjectDisposedException>(list.Refresh);
        Assert.Throws<ObjectDisposedException>(() => _lists.Start(workspace));
    }

    private static Workspace NewWorkspace() => new()
    {
        Id = Workspace.NewId(),
        Name = "Workspace",
        Folder = Path.Combine(Path.GetTempPath(), $"tammi-tests-missing-{Guid.NewGuid():N}"),
        Repositories = [new WorkspaceRepository("api", "/missing/api", "task-1", "origin/main")],
    };
}
