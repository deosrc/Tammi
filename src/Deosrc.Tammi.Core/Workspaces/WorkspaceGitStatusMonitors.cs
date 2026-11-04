using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The git status monitors of the workspaces open in tabs. A workspace's monitor keeps running while its tab is open,
/// even when another tab is shown, and stops when its tab closes.
/// </summary>
public sealed class WorkspaceGitStatusMonitors : IDisposable
{
    private readonly WorkspaceTabs _tabs;
    private readonly IGitRepositoryReader _reader;
    private readonly IGitWorktreeService _worktrees;
    private readonly IFileSystemWatchService _watchService;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, WorkspaceGitStatusMonitor> _monitors = [];
    private bool _disposed;

    public WorkspaceGitStatusMonitors(
        WorkspaceTabs tabs,
        IGitRepositoryReader reader,
        IGitWorktreeService worktrees,
        IFileSystemWatchService watchService)
    {
        _tabs = tabs;
        _reader = reader;
        _worktrees = worktrees;
        _watchService = watchService;
        _tabs.Changed += OnTabsChanged;
    }

    /// <summary>
    /// Returns the monitor of an open workspace, or <see langword="null"/> if it isn't being monitored.
    /// </summary>
    public WorkspaceGitStatusMonitor? Find(string workspaceId)
    {
        lock (_lock)
        {
            return _monitors.GetValueOrDefault(workspaceId);
        }
    }

    /// <summary>
    /// Starts monitoring a workspace that's open in a tab. If it's already monitored, returns its monitor, unless its
    /// folder or repositories have changed: then its monitor is replaced.
    /// </summary>
    /// <exception cref="InvalidOperationException">The workspace isn't open in a tab.</exception>
    public WorkspaceGitStatusMonitor Start(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        WorkspaceGitStatusMonitor monitor;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_tabs.Find(workspace.Id) is null)
            {
                throw new InvalidOperationException($"The workspace '{workspace.Name}' isn't open.");
            }

            if (_monitors.TryGetValue(workspace.Id, out var existing))
            {
                if (IsSameRepositories(existing.Workspace, workspace))
                {
                    return existing;
                }

                existing.Dispose();
            }

            monitor = new WorkspaceGitStatusMonitor(workspace, _reader, _worktrees, _watchService);
            _monitors[workspace.Id] = monitor;
        }

        monitor.Start();
        return monitor;
    }

    /// <summary>
    /// Stops monitoring a workspace, e.g. while it's being deleted, so its folders aren't being watched. Starting it
    /// again makes a new monitor.
    /// </summary>
    /// <returns>Whether the workspace was being monitored.</returns>
    public bool Stop(string workspaceId)
    {
        lock (_lock)
        {
            if (!_monitors.Remove(workspaceId, out var monitor))
            {
                return false;
            }

            monitor.Dispose();
            return true;
        }
    }

    /// <summary>
    /// Stops every monitor.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _tabs.Changed -= OnTabsChanged;
            foreach (var monitor in _monitors.Values)
            {
                monitor.Dispose();
            }

            _monitors.Clear();
        }
    }

    private static bool IsSameRepositories(Workspace a, Workspace b) =>
        FileSystemPaths.Comparer.Equals(a.Folder, b.Folder) && a.Repositories.SequenceEqual(b.Repositories);

    private void OnTabsChanged()
    {
        lock (_lock)
        {
            foreach (string id in _monitors.Keys.Where(id => _tabs.Find(id) is null).ToArray())
            {
                _monitors.Remove(id, out var monitor);
                monitor!.Dispose();
            }
        }
    }
}
