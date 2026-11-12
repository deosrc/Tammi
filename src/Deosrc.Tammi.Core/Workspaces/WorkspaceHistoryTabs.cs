namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The History tabs open in every workspace: at most one each. Like terminals, they stay open when a workspace's tab
/// closes, so it shows them again when it reopens.
/// </summary>
public sealed class WorkspaceHistoryTabs
{
    private readonly Lock _lock = new();
    private readonly List<WorkspaceHistoryTab> _tabs = [];

    /// <summary>
    /// Raised with a workspace's ID when its History tab opens or closes. It can be raised on a background thread.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// A workspace's History tab, as a list of zero or one.
    /// </summary>
    public IReadOnlyList<WorkspaceHistoryTab> For(string workspaceId)
    {
        lock (_lock)
        {
            return _tabs.Where(tab => tab.WorkspaceId == workspaceId).ToArray();
        }
    }

    /// <summary>
    /// Opens a workspace's History tab. If it's already open, returns that tab instead.
    /// </summary>
    public WorkspaceHistoryTab Open(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workspaceId);
        WorkspaceHistoryTab tab;
        lock (_lock)
        {
            if (_tabs.Find(open => open.WorkspaceId == workspaceId) is { } existing)
            {
                return existing;
            }

            tab = new WorkspaceHistoryTab(workspaceId);
            _tabs.Add(tab);
        }

        Changed?.Invoke(workspaceId);
        return tab;
    }

    /// <summary>
    /// Closes a History tab. Closing one that's already closed does nothing.
    /// </summary>
    public void Close(WorkspaceHistoryTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        lock (_lock)
        {
            if (!_tabs.Remove(tab))
            {
                return;
            }
        }

        Changed?.Invoke(tab.WorkspaceId);
    }

    /// <summary>
    /// Closes a workspace's History tab, e.g. before it's deleted.
    /// </summary>
    public void CloseAll(string workspaceId)
    {
        foreach (var tab in For(workspaceId))
        {
            Close(tab);
        }
    }
}
