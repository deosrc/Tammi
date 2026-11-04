using TAMMi.Core.Integrations;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// The details of a workspace's tasks, read from their sources. It's used while the workspace is open in a tab.
/// </summary>
/// <remarks>
/// Each task is read on its own, so a slow or failing source doesn't hold up the others. While tasks are read again,
/// their previous details are kept until the new ones arrive. Once every task has been read, the workspace's task
/// context file is written with them (see <see cref="TaskContextFile"/>), so agents see the tasks as they are now.
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

    /// <summary>
    /// Creates the list. Nothing is read until <see cref="Refresh"/> is called.
    /// </summary>
    public WorkspaceTaskList(Workspace workspace, IntegrationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(registry);
        Workspace = workspace;
        _registry = registry;
        _tasks = [.. workspace.Tasks.Select(reference => new WorkspaceTask(reference))];
    }

    /// <summary>
    /// The workspace whose tasks these are.
    /// </summary>
    public Workspace Workspace { get; }

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
    /// Why the task context file couldn't be written after the tasks were last read, or <see langword="null"/> if it
    /// was (or they haven't been read yet).
    /// </summary>
    public string? ContextFileProblem => _contextFileProblem;

    /// <summary>
    /// Raised when a task has been read, when reading starts or finishes, and when the task context file couldn't be
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
    /// A task that completes when every task has been read and the task context file written, or when a newer read or
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
            await TaskContextFile.WriteAsync(Workspace, Tasks, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"The task context file for agents couldn't be written. {ex.Message}";
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
