using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The details of a workspace's tasks, read from their sources. It's used while the workspace is open in a tab.
/// </summary>
/// <remarks>
/// Each task is read on its own, so a slow or failing source doesn't hold up the others. While tasks are read again,
/// their previous details are kept until the new ones arrive. Once every task has been read, the workspace's task
/// context file is written with them (see <see cref="WorkspaceContextFile"/>), so agents see the tasks as they are now.
/// </remarks>
public sealed class WorkspaceTaskList : IDisposable
{
    private readonly IntegrationRegistry _registry;
    private readonly Lock _lock = new();
    private readonly WorkspaceTask[] _tasks;
    private CancellationTokenSource? _refresh;
    private int _pending;
    private bool _disposed;
    private volatile string? _contextFileProblem;
    private volatile Workspace _workspace;

    /// <summary>
    /// Creates the list. Nothing is read until <see cref="Refresh"/> is called.
    /// </summary>
    public WorkspaceTaskList(Workspace workspace, IntegrationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(registry);
        _workspace = workspace;
        _registry = registry;
        _tasks = [.. workspace.Tasks.Select(reference => new WorkspaceTask(reference))];
    }

    /// <summary>
    /// The workspace whose tasks these are. It changes when the workspace is renamed (see <see cref="RenameAsync"/>).
    /// </summary>
    public Workspace Workspace => _workspace;

    /// <summary>
    /// Each of the workspace's tasks, in the workspace's order.
    /// </summary>
    public IReadOnlyList<WorkspaceTask> Tasks
    {
        get
        {
            lock (_lock)
            {
                return [.. _tasks];
            }
        }
    }

    /// <summary>
    /// Whether tasks are being read.
    /// </summary>
    public bool IsRefreshing
    {
        get
        {
            lock (_lock)
            {
                return _pending > 0;
            }
        }
    }

    /// <summary>
    /// Why the workspace context file couldn't be written after the tasks were last read, or <see langword="null"/> if it
    /// was (or they haven't been read yet).
    /// </summary>
    public string? ContextFileProblem => _contextFileProblem;

    /// <summary>
    /// Raised when a task has been read, when reading starts or finishes, and when the workspace context file couldn't be
    /// written or can be again. It's raised on a background thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Reads every task again in the background. A read that's under way is cancelled.
    /// </summary>
    public void Refresh() => _ = RefreshAsync();

    /// <summary>
    /// Reads every task again. A read that's under way is cancelled.
    /// </summary>
    /// <returns>
    /// A task that completes when every task has been read and the workspace context file written, or when a newer read or
    /// disposing cancels it.
    /// </returns>
    public Task RefreshAsync()
    {
        CancellationToken token;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _refresh?.Cancel();
            _refresh?.Dispose();
            _refresh = new CancellationTokenSource();
            token = _refresh.Token;
            _pending = _tasks.Length;
        }

        return ReadAllAsync(token);
    }

    /// <summary>
    /// Takes the workspace's new name, after it was renamed, without reading the tasks again. The workspace context file is
    /// written again with it: straight away if every task has been read, or else when they have.
    /// </summary>
    /// <param name="workspace">The renamed workspace. Its tasks must be the same.</param>
    /// <returns>A task that completes when the workspace context file has been written, if it's written straight away.</returns>
    /// <exception cref="ArgumentException">The workspace is a different one, or its tasks have changed.</exception>
    public Task RenameAsync(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.Id != _workspace.Id || !workspace.Tasks.SequenceEqual(_workspace.Tasks))
        {
            throw new ArgumentException("Only the workspace's name can change.", nameof(workspace));
        }

        CancellationToken token;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_workspace.Name == workspace.Name)
            {
                return Task.CompletedTask;
            }

            _workspace = workspace;

            // A read under way writes the file when it finishes, with the new name. Nothing's been read yet otherwise.
            if (_pending > 0 || _refresh is null)
            {
                return Task.CompletedTask;
            }

            token = _refresh.Token;
        }

        return WriteContextFileAsync(token);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _refresh?.Cancel();
            _refresh?.Dispose();
            _refresh = null;
        }
    }

    private async Task ReadAllAsync(CancellationToken token)
    {
        if (_tasks.Length > 0)
        {
            Changed?.Invoke();
            await Task.WhenAll(_tasks
                .Select((task, slot) => (task.Reference, slot))
                .Select(read => Task.Run(() => ReadAsync(read.slot, read.Reference, token), CancellationToken.None)))
                .ConfigureAwait(false);
        }

        await WriteContextFileAsync(token).ConfigureAwait(false);
    }

    private async Task WriteContextFileAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        string? problem = null;
        try
        {
            await WorkspaceContextFile.WriteAsync(Workspace, Tasks, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"The workspace context file for agents couldn't be written. {ex.Message}";
        }

        if (token.IsCancellationRequested || problem == _contextFileProblem)
        {
            return;
        }

        _contextFileProblem = problem;
        Changed?.Invoke();
    }

    private async Task ReadAsync(int slot, TaskReference reference, CancellationToken token)
    {
        WorkspaceTask? read = await ReadTaskAsync(reference, token).ConfigureAwait(false);
        lock (_lock)
        {
            if (token.IsCancellationRequested || read is null)
            {
                return;
            }

            _tasks[slot] = read;
            _pending--;
        }

        Changed?.Invoke();
    }

    /// <returns>The task as read, or <see langword="null"/> if reading was cancelled.</returns>
    private async Task<WorkspaceTask?> ReadTaskAsync(TaskReference reference, CancellationToken token)
    {
        var loaded = new WorkspaceTask(reference) { IsLoaded = true };
        if (_registry.Find(reference.Source) is not ITaskIntegration source)
        {
            return loaded with { Problem = WorkspaceTaskProblem.SourceNotConnected };
        }

        try
        {
            TaskInfo? task = await source.GetTaskAsync(reference.Key, token).ConfigureAwait(false);
            return task is null
                ? loaded with { Problem = WorkspaceTaskProblem.NotFound }
                : loaded with { Task = task };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            // Any failure is shown on the task rather than lost on a background thread, e.g. a key in the metadata
            // that the source can't read (ArgumentException).
            return loaded with { Problem = WorkspaceTaskProblem.ReadFailed, ProblemDetail = exception.Message };
        }
    }
}
