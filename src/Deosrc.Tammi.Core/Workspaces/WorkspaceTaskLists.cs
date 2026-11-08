using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The task lists of the workspaces open in tabs. A workspace's list is read when it's first started, kept while its tab
/// is open (so switching tabs doesn't read the tasks again), and dropped when its tab closes. Every list is read again
/// when integrations are added or removed, since tasks from them may now be readable, or no longer be.
/// </summary>
public sealed class WorkspaceTaskLists : IDisposable
{
    private readonly WorkspaceTabs _tabs;
    private readonly IntegrationRegistry _registry;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, WorkspaceTaskList> _lists = [];
    private bool _disposed;

    public WorkspaceTaskLists(WorkspaceTabs tabs, IntegrationRegistry registry)
    {
        _tabs = tabs;
        _registry = registry;
        _tabs.Changed += OnTabsChanged;
        _registry.Changed += OnRegistryChanged;
    }

    /// <summary>
    /// Returns the task list of an open workspace, or <see langword="null"/> if it hasn't been started.
    /// </summary>
    public WorkspaceTaskList? Find(string workspaceId)
    {
        lock (_lock)
        {
            return _lists.GetValueOrDefault(workspaceId);
        }
    }

    /// <summary>
    /// Starts reading the tasks of a workspace that's open in a tab. If they're already read, returns its list (taking
    /// the workspace's new name, if it was renamed), unless its tasks have changed: then its list is replaced.
    /// </summary>
    /// <exception cref="InvalidOperationException">The workspace isn't open in a tab.</exception>
    public WorkspaceTaskList Start(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        WorkspaceTaskList list;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_tabs.Find(workspace.Id) is null)
            {
                throw new InvalidOperationException($"The workspace '{workspace.Name}' isn't open.");
            }

            if (_lists.TryGetValue(workspace.Id, out var existing))
            {
                if (existing.Workspace.Tasks.SequenceEqual(workspace.Tasks))
                {
                    _ = existing.RenameAsync(workspace);
                    return existing;
                }

                existing.Dispose();
            }

            list = new WorkspaceTaskList(workspace, _registry);
            _lists[workspace.Id] = list;
        }

        list.Refresh();
        return list;
    }

    /// <summary>
    /// Drops every list.
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
            _registry.Changed -= OnRegistryChanged;
            foreach (var list in _lists.Values)
            {
                list.Dispose();
            }

            _lists.Clear();
        }
    }

    private void OnTabsChanged()
    {
        lock (_lock)
        {
            foreach (string id in _lists.Keys.Where(id => _tabs.Find(id) is null).ToArray())
            {
                _lists.Remove(id, out var list);
                list!.Dispose();
            }
        }
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        WorkspaceTaskList[] lists;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            lists = [.. _lists.Values];
        }

        foreach (var list in lists)
        {
            try
            {
                list.Refresh();
            }
            catch (ObjectDisposedException)
            {
                // Its tab closed meanwhile.
            }
        }
    }
}
