using TAMMi.Core.Git;
using TAMMi.Core.Tests.Catalogue;
using TAMMi.Core.Tests.Git;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

public sealed class WorkspaceGitStatusMonitorsTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly WorkspaceTabs _tabs = new();
    private readonly FakeFileSystemWatchService _watchService = new();
    private readonly WorkspaceGitStatusMonitors _monitors;

    public WorkspaceGitStatusMonitorsTests()
    {
        var reader = new GitRepositoryReader();
        _monitors = new WorkspaceGitStatusMonitors(
            _tabs,
            reader,
            new GitWorktreeService(_repos.Git, reader),
            _watchService);
    }

    public void Dispose()
    {
        _monitors.Dispose();
        _repos.Dispose();
    }

    [Fact]
    public void Start_WorkspaceNotOpen_Throws()
    {
        var workspace = CreateWorkspace();

        Assert.Throws<InvalidOperationException>(() => _monitors.Start(workspace));
        Assert.Null(_monitors.Find(workspace.Id));
    }

    [Fact]
    public void Start_AlreadyMonitored_ReturnsTheSameMonitor()
    {
        var workspace = CreateWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);

        var monitor = _monitors.Start(workspace);

        Assert.Same(monitor, _monitors.Start(workspace with { Name = "Renamed" }));
        Assert.Same(monitor, _monitors.Find(workspace.Id));
    }

    [Fact]
    public void Start_RepositoriesChanged_ReplacesTheMonitor()
    {
        var workspace = CreateWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);
        var monitor = _monitors.Start(workspace);

        var changed = _monitors.Start(workspace with
        {
            Repositories = [new WorkspaceRepository("web", _repos.PathTo("web"), "task-1", "origin/main")],
        });

        Assert.NotSame(monitor, changed);
        Assert.Throws<ObjectDisposedException>(monitor.Start);
        Assert.Equal("web", Assert.Single(changed.Repositories).Repository.FolderName);
    }

    [Fact]
    public void ClosingTheTab_StopsItsMonitor()
    {
        var workspace = CreateWorkspace();
        var other = CreateWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);
        _tabs.Open(other.Id, other.Name);
        var monitor = _monitors.Start(workspace);
        var otherMonitor = _monitors.Start(other);

        _tabs.Close(workspace.Id);

        Assert.Null(_monitors.Find(workspace.Id));
        Assert.Throws<ObjectDisposedException>(monitor.Start);
        Assert.Same(otherMonitor, _monitors.Find(other.Id));
    }

    [Fact]
    public void Stop_StopsTheMonitorUntilItsStartedAgain()
    {
        var workspace = CreateWorkspace();
        _tabs.Open(workspace.Id, workspace.Name);
        var monitor = _monitors.Start(workspace);

        Assert.True(_monitors.Stop(workspace.Id));

        Assert.Null(_monitors.Find(workspace.Id));
        Assert.Throws<ObjectDisposedException>(monitor.Start);
        Assert.False(_monitors.Stop(workspace.Id));
        Assert.NotSame(monitor, _monitors.Start(workspace));
    }

    private Workspace CreateWorkspace() => new()
    {
        Id = Workspace.NewId(),
        Name = "Workspace",
        Folder = _repos.PathTo("workspace"),
        Repositories = [new WorkspaceRepository("api", _repos.PathTo("api"), "task-1", "origin/main")],
    };
}
