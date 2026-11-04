using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Terminals;

/// <summary>
/// The terminals of every workspace. They keep running when a workspace's tab closes, so it can show them again when
/// it reopens, and they all end when this is disposed, as the app closes.
/// </summary>
public sealed class WorkspaceTerminals(TerminalLauncher launcher) : IAsyncDisposable
{
    /// <summary>
    /// The name of a terminal started in the Workspace Folder. One started in a repository is named after its folder.
    /// </summary>
    public const string WorkspaceFolderTerminalName = "Terminal";

    private readonly Lock _lock = new();
    private readonly List<WorkspaceTerminal> _terminals = [];
    private bool _disposed;

    /// <summary>
    /// Raised with a workspace's ID when one of its terminals starts, exits or is closed. It can be raised on a
    /// background thread.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// A workspace's open terminals, in the order they were started.
    /// </summary>
    public IReadOnlyList<WorkspaceTerminal> For(string workspaceId)
    {
        lock (_lock)
        {
            return _terminals.Where(terminal => terminal.WorkspaceId == workspaceId).ToArray();
        }
    }

    /// <summary>
    /// Starts a terminal in a workspace, with the terminal shell from the settings.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">
    /// The repository to start it in, or <c>null</c> to start it in the Workspace Folder.
    /// </param>
    /// <returns>The terminal.</returns>
    /// <exception cref="FileNotFoundException">The shell can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder to start it in doesn't exist.</exception>
    /// <exception cref="ObjectDisposedException">The app is closing.</exception>
    public WorkspaceTerminal Start(Workspace workspace, WorkspaceRepository? repository = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var folder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        WorkspaceTerminal terminal;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // The terminal resizes the session to fit as soon as it's shown.
            var pty = launcher.Start(folder, PtySize.Default);
            terminal = new WorkspaceTerminal(
                workspace.Id,
                UniqueName(workspace.Id, repository?.FolderName ?? WorkspaceFolderTerminalName),
                folder,
                pty);
            _terminals.Add(terminal);
        }

        _ = terminal.Session.Pty.Exited.ContinueWith(
            _ => Changed?.Invoke(terminal.WorkspaceId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        Changed?.Invoke(workspace.Id);
        return terminal;
    }

    /// <summary>
    /// Closes a terminal, ending its shell. Closing one that's already closed does nothing.
    /// </summary>
    /// <returns>Completes once the shell has ended, or been given up on.</returns>
    public async Task CloseAsync(WorkspaceTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        lock (_lock)
        {
            if (!_terminals.Remove(terminal))
            {
                return;
            }
        }

        Changed?.Invoke(terminal.WorkspaceId);
        await terminal.Session.Pty.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Closes all of a workspace's terminals, e.g. before it's deleted.
    /// </summary>
    public Task CloseAllAsync(string workspaceId) =>
        Task.WhenAll(For(workspaceId).Select(CloseAsync));

    /// <summary>
    /// Closes every terminal, and stops new ones from starting.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        WorkspaceTerminal[] terminals;
        lock (_lock)
        {
            _disposed = true;
            terminals = [.. _terminals];
        }

        await Task.WhenAll(terminals.Select(CloseAsync)).ConfigureAwait(false);
    }

    private string UniqueName(string workspaceId, string name)
    {
        var used = _terminals
            .Where(terminal => terminal.WorkspaceId == workspaceId)
            .Select(terminal => terminal.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name))
        {
            return name;
        }

        var number = 2;
        while (used.Contains($"{name} {number}"))
        {
            number++;
        }

        return $"{name} {number}";
    }
}
