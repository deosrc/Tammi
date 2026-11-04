using TAMMi.Core.Git;
using TAMMi.Core.Platform;
using TAMMi.Core.Tests.Catalogue;
using TAMMi.Core.Tests.Git;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

public sealed class WorkspaceGitStatusMonitorTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();
    private readonly GitWorktreeService _worktrees;
    private readonly FakeFileSystemWatchService _watchService = new();
    private readonly List<IDisposable> _disposables = [];

    public WorkspaceGitStatusMonitorTests()
    {
        _worktrees = new GitWorktreeService(_repos.Git, _reader);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private string WorkspaceFolder => _repos.PathTo("workspace");

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _repos.Dispose();
    }

    [Fact]
    public async Task Start_ReadsEachRepositoryAndWatchesItsWorktreeAndGitData()
    {
        var workspace = await CreateWorkspaceAsync("api", "web");
        var monitor = CreateMonitor(workspace);

        Assert.All(monitor.Repositories, repository => Assert.False(repository.IsLoaded));
        await WaitForAsync(monitor, () => monitor.Repositories.All(repository => repository.IsLoaded), monitor.Start);

        Assert.Equal(["api", "web"], monitor.Repositories.Select(repository => repository.Repository.FolderName));
        Assert.All(monitor.Repositories, repository =>
        {
            Assert.Equal(WorktreeHealth.Healthy, repository.Health);
            Assert.Equal("task-1", repository.Status?.Branch);
            Assert.False(repository.Status!.IsChanged);
            Assert.Null(repository.Problem);
        });

        string api = Path.Combine(WorkspaceFolder, "api");
        string commonDirectory = Path.Combine(_repos.PathTo("api"), ".git");
        Assert.Contains(api, _watchService.WatchedFolders);
        Assert.Contains(Path.Combine(commonDirectory, "worktrees", "api"), _watchService.WatchedFolders);
        Assert.Contains(Path.Combine(commonDirectory, "refs"), _watchService.WatchedFolders);
        Assert.Contains(commonDirectory, _watchService.WatchedFolders);
        Assert.False(_watchService.OptionsFor(commonDirectory).IncludeSubdirectories);
        Assert.True(_watchService.OptionsFor(api).IncludeSubdirectories);
    }

    [Fact]
    public async Task FileChange_ReadsThatRepositoryAgain()
    {
        var workspace = await CreateWorkspaceAsync("api");
        var monitor = CreateMonitor(workspace);
        monitor.Start();
        await monitor.RefreshAsync(CancellationToken);
        string api = Path.Combine(WorkspaceFolder, "api");

        TempRepositories.WriteFile(api, "new.txt", "new");
        await WaitForAsync(
            monitor,
            () => monitor.Repositories[0].Status?.IsChanged == true,
            () => _watchService.RaiseChange(api, Path.Combine(api, "new.txt")));

        Assert.Equal("new.txt", Assert.Single(monitor.Repositories[0].Status!.Changes).Path);
    }

    [Fact]
    public async Task BrokenWorktree_IsReportedWithoutReadingIt()
    {
        var workspace = await CreateWorkspaceAsync("api");
        TempRepositories.DeleteFolder(_repos.PathTo("api"));
        var monitor = CreateMonitor(workspace);

        await monitor.RefreshAsync(CancellationToken);

        var repository = Assert.Single(monitor.Repositories);
        Assert.True(repository.IsLoaded);
        Assert.Equal(WorktreeHealth.RepositoryMissing, repository.Health);
        Assert.Null(repository.Status);

        // Its folder is still watched, so repairing it shows up.
        Assert.Equal([Path.Combine(WorkspaceFolder, "api")], _watchService.WatchedFolders);
    }

    [Fact]
    public async Task MissingWorktree_IsNotWatched()
    {
        var workspace = await CreateWorkspaceAsync("api");
        TempRepositories.DeleteFolder(Path.Combine(WorkspaceFolder, "api"));
        var monitor = CreateMonitor(workspace);

        await monitor.RefreshAsync(CancellationToken);

        Assert.Equal(WorktreeHealth.Missing, Assert.Single(monitor.Repositories).Health);
        Assert.Empty(_watchService.WatchedFolders);
    }

    [Fact]
    public async Task WatchError_WatchesTheFolderAgain()
    {
        var workspace = await CreateWorkspaceAsync("api");
        var monitor = CreateMonitor(workspace);
        await monitor.RefreshAsync(CancellationToken);
        string api = Path.Combine(WorkspaceFolder, "api");
        int watchCount = _watchService.WatchedFolders.Count;

        var read = WaitForChangeAsync(monitor);
        _watchService.RaiseChange(api, api, new IOException("The watch stopped."));
        await read;

        Assert.Equal(watchCount, _watchService.WatchedFolders.Count);
        Assert.Contains(api, _watchService.WatchedFolders);
    }

    [Fact]
    public async Task Dispose_StopsWatching()
    {
        var workspace = await CreateWorkspaceAsync("api");
        var monitor = CreateMonitor(workspace);
        await monitor.RefreshAsync(CancellationToken);

        monitor.Dispose();

        Assert.Empty(_watchService.WatchedFolders);
        Assert.Throws<ObjectDisposedException>(monitor.Start);
    }

    [Fact]
    public async Task RealWatcher_FileEditsAndCommitsUpdateTheStatus()
    {
        var workspace = await CreateWorkspaceAsync("api");
        using var monitor = new WorkspaceGitStatusMonitor(workspace, _reader, _worktrees, new FileSystemWatchService());
        await WaitForAsync(monitor, () => monitor.Repositories[0].IsLoaded, monitor.Start);
        string api = Path.Combine(WorkspaceFolder, "api");

        // Editing a file changes only the worktree folder.
        await WaitForAsync(
            monitor,
            () => monitor.Repositories[0].Status?.Changes.Count == 1,
            () => File.AppendAllText(Path.Combine(api, "README.md"), "More\n"));

        // Committing changes only the git data in the Local Repository.
        await WaitForAsync(
            monitor,
            () => monitor.Repositories[0].Status is { Changes.Count: 0, UnpushedCommitCount: 1 },
            () => _repos.RunAsync(api, "commit", "-am", "Edit README"));
    }

    private WorkspaceGitStatusMonitor CreateMonitor(Workspace workspace)
    {
        var monitor = new WorkspaceGitStatusMonitor(workspace, _reader, _worktrees, _watchService);
        _disposables.Add(monitor);
        return monitor;
    }

    private async Task<Workspace> CreateWorkspaceAsync(params string[] names)
    {
        var repositories = new List<WorkspaceRepository>();
        foreach (string name in names)
        {
            string remote = await _repos.CreateRemoteAsync(name);
            string clone = await _repos.CloneAsync(remote, name);
            await _worktrees.CreateWorktreeAsync(
                new CreateWorktreeRequest
                {
                    RepositoryPath = clone,
                    WorktreePath = Path.Combine(WorkspaceFolder, name),
                    BranchName = "task-1",
                },
                CancellationToken);
            repositories.Add(new WorkspaceRepository(name, clone, "task-1", "origin/main"));
        }

        return new Workspace
        {
            Id = Workspace.NewId(),
            Name = "Workspace",
            Folder = WorkspaceFolder,
            Repositories = repositories,
        };
    }

    private static Task WaitForChangeAsync(WorkspaceGitStatusMonitor monitor) =>
        WaitForAsync(monitor, () => true, () => { });

    private static Task WaitForAsync(WorkspaceGitStatusMonitor monitor, Func<bool> condition, Action action) =>
        WaitForAsync(monitor, condition, () =>
        {
            action();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Runs <paramref name="action"/>, then waits until a status read leaves <paramref name="condition"/> true.
    /// </summary>
    private static async Task WaitForAsync(WorkspaceGitStatusMonitor monitor, Func<bool> condition, Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged()
        {
            if (condition())
            {
                done.TrySetResult();
            }
        }

        monitor.Changed += OnChanged;
        try
        {
            await action();
            await done.Task.WaitAsync(Timeout, CancellationToken);
        }
        finally
        {
            monitor.Changed -= OnChanged;
        }
    }
}
