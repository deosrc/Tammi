using TAMMi.Core.Integrations;
using TAMMi.Core.Tests.Integrations;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

public sealed class WorkspaceTaskListsTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly FakeTaskSource _github = new("github", "GH");
    private readonly IntegrationRegistry _registry;
    private readonly WorkspaceTabs _tabs = new();
    private readonly WorkspaceTaskLists _lists;

    public WorkspaceTaskListsTests()
    {
        _registry = new IntegrationRegistry([_github]);
        _lists = new WorkspaceTaskLists(_tabs, _registry);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _lists.Dispose();

    [Fact]
    public void Start_WorkspaceNotOpen_Throws()
    {
        var workspace = WorkspaceTaskListTests.CreateWorkspace();

        Assert.Throws<InvalidOperationException>(() => _lists.Start(workspace));
        Assert.Null(_lists.Find(workspace.Id));
    }

    [Fact]
    public async Task Start_ReadsTheTasks()
    {
        var task = _github.Add(1, "Task");
        var workspace = Open(task.Reference);

        var list = await WaitForAsync(() => _lists.Start(workspace), list => list.Tasks[0].IsLoaded);

        Assert.Equal(task, list.Tasks[0].Task);
        Assert.Same(list, _lists.Find(workspace.Id));
    }

    [Fact]
    public void Start_AlreadyStarted_ReturnsTheSameList_WithoutReadingAgain()
    {
        _github.Gate = new TaskCompletionSource();
        var workspace = Open(new TaskReference("github", "GH-1"));
        var list = _lists.Start(workspace);

        Assert.Same(list, _lists.Start(workspace with { Name = "Renamed" }));
        _github.Gate.SetResult();
    }

    [Fact]
    public void Start_TasksChanged_ReplacesTheList()
    {
        var workspace = Open(new TaskReference("github", "GH-1"));
        var list = _lists.Start(workspace);

        var changed = _lists.Start(workspace with { Tasks = [new TaskReference("github", "GH-2")] });

        Assert.NotSame(list, changed);
        Assert.Throws<ObjectDisposedException>(list.Refresh);
    }

    [Fact]
    public void ClosingTheTab_DropsItsList()
    {
        var workspace = Open();
        var list = _lists.Start(workspace);

        _tabs.Close(workspace.Id);

        Assert.Null(_lists.Find(workspace.Id));
        Assert.Throws<ObjectDisposedException>(list.Refresh);
    }

    [Fact]
    public async Task AddingAnIntegration_ReadsTheTasksAgain()
    {
        var workspace = Open(new TaskReference("jira", "ABC-1"));
        var list = await WaitForAsync(() => _lists.Start(workspace), list => list.Tasks[0].IsLoaded);
        Assert.Equal(WorkspaceTaskProblem.SourceNotConnected, list.Tasks[0].Problem);
        var jira = new FakeTaskSource("jira", "ABC");
        var task = jira.Add(1, "Now readable");

        await WaitForAsync(() => _registry.Add(jira), list, list => list.Tasks[0].Task is not null);

        Assert.Equal(task, list.Tasks[0].Task);
    }

    private Workspace Open(params TaskReference[] tasks)
    {
        var workspace = WorkspaceTaskListTests.CreateWorkspace(tasks);
        _tabs.Open(workspace.Id, workspace.Name);
        return workspace;
    }

    /// <summary>
    /// Starts a list, then waits until a change leaves <paramref name="condition"/> true.
    /// </summary>
    private static async Task<WorkspaceTaskList> WaitForAsync(
        Func<WorkspaceTaskList> start,
        Func<WorkspaceTaskList, bool> condition)
    {
        WorkspaceTaskList list = start();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged()
        {
            if (condition(list))
            {
                done.TrySetResult();
            }
        }

        list.Changed += OnChanged;
        try
        {
            OnChanged();
            await done.Task.WaitAsync(Timeout, CancellationToken);
            return list;
        }
        finally
        {
            list.Changed -= OnChanged;
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/>, then waits until a change to <paramref name="list"/> leaves
    /// <paramref name="condition"/> true.
    /// </summary>
    private static Task WaitForAsync(Action action, WorkspaceTaskList list, Func<WorkspaceTaskList, bool> condition) =>
        WaitForAsync(() =>
        {
            action();
            return list;
        }, condition);
}
