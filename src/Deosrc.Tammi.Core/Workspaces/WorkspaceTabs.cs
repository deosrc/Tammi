namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The workspaces open in tabs, in tab order.
/// </summary>
/// <remarks>
/// This only tracks which tabs are open. Which tab is shown is up to the UI (e.g. the current route).
/// </remarks>
public sealed class WorkspaceTabs
{
    private readonly List<WorkspaceTab> _tabs = [];

    /// <summary>
    /// Raised when a tab is opened, closed or renamed.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The open tabs, in tab order.
    /// </summary>
    public IReadOnlyList<WorkspaceTab> Tabs => _tabs;

    /// <summary>
    /// Returns the tab for a workspace, or <c>null</c> if the workspace isn't open.
    /// </summary>
    public WorkspaceTab? Find(string workspaceId) =>
        _tabs.Find(tab => tab.WorkspaceId == workspaceId);

    /// <summary>
    /// Opens a tab for a workspace at the end of the tab strip. If the workspace is already open, returns
    /// its existing tab instead.
    /// </summary>
    public WorkspaceTab Open(string workspaceId, string name)
    {
        if (Find(workspaceId) is { } existing)
        {
            return existing;
        }

        var tab = new WorkspaceTab(workspaceId, name);
        _tabs.Add(tab);
        Changed?.Invoke();
        return tab;
    }

    /// <summary>
    /// Changes the name shown on a workspace's tab, after the workspace was renamed. Nothing happens if it isn't open.
    /// </summary>
    public void Rename(string workspaceId, string name)
    {
        var index = _tabs.FindIndex(tab => tab.WorkspaceId == workspaceId);
        if (index < 0 || _tabs[index].Name == name)
        {
            return;
        }

        _tabs[index] = _tabs[index] with { Name = name };
        Changed?.Invoke();
    }

    /// <summary>
    /// Closes a workspace's tab.
    /// </summary>
    /// <returns>
    /// The tab to show instead if the closed tab was being shown: the next tab, or the previous one if it was
    /// the last tab. Returns <c>null</c> when no other tabs are open or the workspace wasn't open.
    /// </returns>
    public WorkspaceTab? Close(string workspaceId)
    {
        var index = _tabs.FindIndex(tab => tab.WorkspaceId == workspaceId);
        if (index < 0)
        {
            return null;
        }

        _tabs.RemoveAt(index);
        Changed?.Invoke();

        if (_tabs.Count == 0)
        {
            return null;
        }

        return _tabs[Math.Min(index, _tabs.Count - 1)];
    }
}
