using Tammi.Core.Integrations;
using Tammi.Core.Tests.Integrations;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceTaskListTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("tammi-tests-");
    private readonly FakeTaskSource _github = new("github", "GH");
    private readonly FakeTaskSource _jira = new("jira", "ABC");
    private readonly IntegrationRegistry _registry;

    public WorkspaceTaskListTests()
    {
        _registry = new IntegrationRegistry([_github, _jira]);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void Tasks_BeforeTheFirstRead_AreTheWorkspacesTasks_NotYetLoaded()
    {
        using var list = new WorkspaceTaskList(
            CreateWorkspace(new TaskReference("github", "GH-1"), new TaskReference("jira", "ABC-1")), _registry);

        Assert.Equal(["GH-1", "ABC-1"], list.Tasks.Select(task => task.Key));
        Assert.All(list.Tasks, task => Assert.False(task.IsLoaded));
        Assert.Empty(_github.Calls);
    }

    [Fact]
    public async Task RefreshAsync_ReadsEachTaskFromItsSource()
    {
        var github = _github.Add(1, "Fix the bug") with
        {
            Status = "Open",
            StatusCategory = TaskStatusCategory.ToDo,
        };
        _github.Tasks[0] = github;
        var jira = _jira.Add(7, "Add a feature");
        using var list = new WorkspaceTaskList(CreateWorkspace(github.Reference, jira.Reference), _registry);

        await list.RefreshAsync();

        Assert.Equal([github, jira], list.Tasks.Select(task => task.Task));
        Assert.All(list.Tasks, task =>
        {
            Assert.True(task.IsLoaded);
            Assert.Null(task.Problem);
        });
        Assert.False(list.IsRefreshing);
        Assert.Equal(["get:GH-1"], _github.Calls);
        Assert.Equal(["get:ABC-7"], _jira.Calls);
    }

    [Fact]
    public async Task RefreshAsync_SourceNotConnected_SaysSo()
    {
        using var list = new WorkspaceTaskList(CreateWorkspace(new TaskReference("removed", "X-1")), _registry);

        await list.RefreshAsync();

        WorkspaceTask task = Assert.Single(list.Tasks);
        Assert.True(task.IsLoaded);
        Assert.Null(task.Task);
        Assert.Equal(WorkspaceTaskProblem.SourceNotConnected, task.Problem);
    }

    [Fact]
    public async Task RefreshAsync_TaskMissing_SaysItWasntFound()
    {
        using var list = new WorkspaceTaskList(CreateWorkspace(new TaskReference("github", "GH-9")), _registry);

        await list.RefreshAsync();

        Assert.Equal(WorkspaceTaskProblem.NotFound, Assert.Single(list.Tasks).Problem);
    }

    [Fact]
    public async Task RefreshAsync_SourceFails_KeepsTheError_AndReadsTheOtherTasks()
    {
        _github.Failure = new IntegrationException("GitHub couldn't be reached.");
        var jira = _jira.Add(1, "Works");
        using var list = new WorkspaceTaskList(
            CreateWorkspace(new TaskReference("github", "GH-1"), jira.Reference), _registry);

        await list.RefreshAsync();

        WorkspaceTask failed = list.Tasks[0];
        Assert.Equal(WorkspaceTaskProblem.ReadFailed, failed.Problem);
        Assert.Equal("GitHub couldn't be reached.", failed.ProblemDetail);
        Assert.Equal(jira, list.Tasks[1].Task);
    }

    [Fact]
    public async Task RefreshAsync_Again_KeepsThePreviousDetails_UntilTheNewOnesArrive()
    {
        var task = _github.Add(1, "Before");
        using var list = new WorkspaceTaskList(CreateWorkspace(task.Reference), _registry);
        await list.RefreshAsync();
        _github.Tasks[0] = task with { Title = "After" };
        _github.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task refresh = list.RefreshAsync();

        Assert.True(list.IsRefreshing);
        Assert.Equal("Before", list.Tasks[0].Task?.Title);
        _github.Gate.SetResult();
        await refresh;
        Assert.False(list.IsRefreshing);
        Assert.Equal("After", list.Tasks[0].Task?.Title);
    }

    [Fact]
    public async Task RefreshAsync_Again_CancelsTheReadUnderWay()
    {
        var task = _github.Add(1, "Task");
        using var list = new WorkspaceTaskList(CreateWorkspace(task.Reference), _registry);
        _github.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task first = list.RefreshAsync();

        _github.Gate = null;
        await list.RefreshAsync();
        await first;

        Assert.Equal(task, list.Tasks[0].Task);
        Assert.Null(list.Tasks[0].Problem);
        Assert.False(list.IsRefreshing);
    }

    [Fact]
    public async Task RefreshAsync_NoTasks_ReadsNothing_AndRaisesNothing()
    {
        using var list = new WorkspaceTaskList(CreateWorkspace(), _registry);
        var changes = 0;
        list.Changed += () => changes++;

        await list.RefreshAsync();

        Assert.Empty(list.Tasks);
        Assert.False(list.IsRefreshing);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task RefreshAsync_RaisesChanged_ForTheStartAndEachTask()
    {
        var first = _github.Add(1, "One");
        var second = _github.Add(2, "Two");
        using var list = new WorkspaceTaskList(CreateWorkspace(first.Reference, second.Reference), _registry);
        var changes = 0;
        list.Changed += () => Interlocked.Increment(ref changes);

        await list.RefreshAsync();

        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task RefreshAsync_AfterDispose_Throws()
    {
        var list = new WorkspaceTaskList(CreateWorkspace(), _registry);
        list.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(list.RefreshAsync);
    }

    [Fact]
    public async Task RefreshAsync_WritesTheTaskContextFile()
    {
        var task = _github.Add(1, "Fix the bug") with { Description = "It breaks." };
        _github.Tasks[0] = task;
        var workspace = CreateWorkspace(task.Reference, new TaskReference("removed", "X-1"));
        using var list = new WorkspaceTaskList(workspace, _registry);

        await list.RefreshAsync();

        var contents = await File.ReadAllTextAsync(TaskContextFile.PathOf(workspace), CancellationToken);
        Assert.Equal(TaskContextFile.Render(workspace, list.Tasks), contents);
        Assert.Contains("## GH-1: Fix the bug", contents, StringComparison.Ordinal);
        Assert.Contains("## X-1", contents, StringComparison.Ordinal);
        Assert.Null(list.ContextFileProblem);
    }

    [Fact]
    public async Task RefreshAsync_Again_RewritesTheTaskContextFile()
    {
        var task = _github.Add(1, "Before");
        var workspace = CreateWorkspace(task.Reference);
        using var list = new WorkspaceTaskList(workspace, _registry);
        await list.RefreshAsync();
        _github.Tasks[0] = task with { Title = "After" };

        await list.RefreshAsync();

        var contents = await File.ReadAllTextAsync(TaskContextFile.PathOf(workspace), CancellationToken);
        Assert.Contains("## GH-1: After", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("Before", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_NoTasks_WritesTheTaskContextFile()
    {
        var workspace = CreateWorkspace();
        using var list = new WorkspaceTaskList(workspace, _registry);

        await list.RefreshAsync();

        Assert.Contains(
            "no tasks",
            await File.ReadAllTextAsync(TaskContextFile.PathOf(workspace), CancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_TaskContextFileCantBeWritten_SaysWhy_AndRaisesChanged()
    {
        var task = _github.Add(1, "Task");
        var workspace = NewWorkspace(task.Reference);
        using var list = new WorkspaceTaskList(workspace, _registry);
        var changes = 0;
        list.Changed += () => Interlocked.Increment(ref changes);

        await list.RefreshAsync();

        Assert.Contains("couldn't be written", list.ContextFileProblem, StringComparison.Ordinal);
        Assert.Equal(3, changes);
        Assert.Equal(task, list.Tasks[0].Task);
    }

    [Fact]
    public async Task RefreshAsync_TaskContextFileCanBeWrittenAgain_ClearsTheProblem()
    {
        var workspace = NewWorkspace(_github.Add(1, "Task").Reference) with
        {
            Folder = Path.Combine(_root.FullName, "later"),
        };
        using var list = new WorkspaceTaskList(workspace, _registry);
        await list.RefreshAsync();
        Assert.NotNull(list.ContextFileProblem);
        Directory.CreateDirectory(workspace.MetadataFolder);

        await list.RefreshAsync();

        Assert.Null(list.ContextFileProblem);
        Assert.True(File.Exists(TaskContextFile.PathOf(workspace)));
    }

    /// <summary>
    /// A workspace with its folder and <c>.tammi/</c> folder on disk, so its task context file can be written.
    /// </summary>
    private Workspace CreateWorkspace(params TaskReference[] tasks)
    {
        var workspace = NewWorkspace(tasks) with { Folder = Path.Combine(_root.FullName, "workspace") };
        Directory.CreateDirectory(workspace.MetadataFolder);
        return workspace;
    }

    /// <summary>
    /// A workspace whose folder doesn't exist, so its task context file can't be written.
    /// </summary>
    internal static Workspace NewWorkspace(params TaskReference[] tasks) => new()
    {
        Id = Workspace.NewId(),
        Name = "Workspace",
        Folder = Path.Combine(Path.GetTempPath(), $"tammi-tests-missing-{Guid.NewGuid():N}"),
        Tasks = tasks,
    };
}
