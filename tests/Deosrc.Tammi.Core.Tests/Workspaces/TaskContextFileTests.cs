using Tammi.Core.Integrations;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Workspaces;

public sealed class TaskContextFileTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("tammi-tests-");
    private readonly Workspace _workspace;

    public TaskContextFileTests()
    {
        _workspace = new Workspace
        {
            Id = Workspace.NewId(),
            Name = "api-42-fix-login",
            Folder = Path.Combine(_root.FullName, "api-42-fix-login"),
            Tasks = [new TaskReference("github", "acme/api#42"), new TaskReference("jira", "ABC-1")],
        };
        Directory.CreateDirectory(_workspace.MetadataFolder);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void PathOf_IsInTheMetadataFolder()
    {
        Assert.Equal(Path.Combine(_workspace.MetadataFolder, "tasks.md"), TaskContextFile.PathOf(_workspace));
    }

    [Fact]
    public void Render_ReadTask_HasItsDetails()
    {
        var info = new TaskInfo("github", "acme/api#42", "Fix login", new Uri("https://github.com/acme/api/issues/42"))
        {
            Description = "Login fails.\r\n\r\nSteps:\r\n1. Log in\r\n",
            Status = "Open",
            StatusCategory = TaskStatusCategory.ToDo,
            RepositoryUrl = new Uri("https://github.com/acme/api"),
        };
        var task = new WorkspaceTask(info.Reference) { IsLoaded = true, Task = info };

        var contents = TaskContextFile.Render(_workspace, [task]);

        Assert.Equal(
            "# Tasks\n\n"
            + "The tasks of the Tammi workspace \"api-42-fix-login\". "
            + "Tammi rewrites this file when the tasks change, so don't edit it.\n\n"
            + "## acme/api#42: Fix login\n\n"
            + "- Link: https://github.com/acme/api/issues/42\n"
            + "- Status: Open (to do)\n"
            + "- Repository: https://github.com/acme/api\n\n"
            + "### Description\n\n"
            + "Login fails.\n\nSteps:\n1. Log in\n",
            contents);
    }

    [Fact]
    public void Render_TaskWithoutStatusOrDescription_SaysSo()
    {
        var info = new TaskInfo("jira", "ABC-1", "Add a feature", new Uri("https://jira.example/browse/ABC-1"))
        {
            Status = "In Review",
        };
        var task = new WorkspaceTask(info.Reference) { IsLoaded = true, Task = info };

        var contents = TaskContextFile.Render(_workspace, [task]);

        Assert.Contains("- Status: In Review\n", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("Repository", contents, StringComparison.Ordinal);
        Assert.EndsWith("### Description\n\nNo description.\n", contents, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "hasn't read this task's details yet")]
    [InlineData(WorkspaceTaskProblem.SourceNotConnected, "Task Source isn't connected")]
    [InlineData(WorkspaceTaskProblem.NotFound, "wasn't found")]
    [InlineData(WorkspaceTaskProblem.ReadFailed, "couldn't read this task's details. GitHub is down.")]
    public void Render_TaskNotRead_ListsItsKey_AndSaysWhy(WorkspaceTaskProblem? problem, string expected)
    {
        var task = new WorkspaceTask(new TaskReference("github", "acme/api#42"))
        {
            IsLoaded = problem is not null,
            Problem = problem,
            ProblemDetail = problem == WorkspaceTaskProblem.ReadFailed ? "GitHub is down." : null,
        };

        var contents = TaskContextFile.Render(_workspace, [task]);

        Assert.Contains("\n## acme/api#42\n\n", contents, StringComparison.Ordinal);
        Assert.Contains(expected, contents, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NoTasks_SaysSo()
    {
        Assert.Contains(
            "This workspace has no tasks.",
            TaskContextFile.Render(_workspace with { Tasks = [] }, []),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WritesTheFile_WithoutLeavingTemporaryFiles()
    {
        WorkspaceTask[] tasks = [new(new TaskReference("github", "acme/api#42"))];

        await TaskContextFile.WriteAsync(_workspace, tasks, CancellationToken);

        Assert.Equal(
            TaskContextFile.Render(_workspace, tasks),
            await File.ReadAllTextAsync(TaskContextFile.PathOf(_workspace), CancellationToken));
        Assert.Equal(["tasks.md"], Directory.GetFiles(_workspace.MetadataFolder).Select(Path.GetFileName));
    }

    [Fact]
    public async Task WriteAsync_SameContents_DoesntRewriteTheFile()
    {
        WorkspaceTask[] tasks = [new(new TaskReference("github", "acme/api#42"))];
        await TaskContextFile.WriteAsync(_workspace, tasks, CancellationToken);
        var path = TaskContextFile.PathOf(_workspace);
        var written = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, written);

        await TaskContextFile.WriteAsync(_workspace, tasks, CancellationToken);

        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task WriteAsync_MetadataFolderMissing_Throws()
    {
        var missing = _workspace with { Folder = Path.Combine(_root.FullName, "missing") };

        await Assert.ThrowsAnyAsync<IOException>(() => TaskContextFile.WriteAsync(missing, [], CancellationToken));
    }

    [Fact]
    public async Task CreateIfMissingAsync_NoFile_ListsTheTasksByKey()
    {
        var path = await TaskContextFile.CreateIfMissingAsync(_workspace, CancellationToken);

        Assert.Equal(TaskContextFile.PathOf(_workspace), path);
        var contents = await File.ReadAllTextAsync(path, CancellationToken);
        Assert.Contains("## acme/api#42\n", contents, StringComparison.Ordinal);
        Assert.Contains("## ABC-1\n", contents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateIfMissingAsync_FileExists_KeepsIt()
    {
        var path = TaskContextFile.PathOf(_workspace);
        await File.WriteAllTextAsync(path, "The tasks' details", CancellationToken);

        await TaskContextFile.CreateIfMissingAsync(_workspace, CancellationToken);

        Assert.Equal("The tasks' details", await File.ReadAllTextAsync(path, CancellationToken));
    }
}
