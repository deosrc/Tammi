using System.Collections.Concurrent;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// The Edit Workspace flows in progress, one per workspace, so each is resumed when the user returns to its workspace's
/// tab. A flow is discarded when it's finished or cancelled, or when the workspace's tab is closed.
/// </summary>
public sealed class EditWorkspaceSessions
{
    private readonly ConcurrentDictionary<string, EditWorkspaceSession> _sessions = new();
    private readonly WorkspaceTabs _tabs;

    public EditWorkspaceSessions(WorkspaceTabs tabs)
    {
        _tabs = tabs;
        _tabs.Changed += DiscardClosed;
    }

    /// <summary>
    /// Raised when a flow is added or discarded. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    public EditWorkspaceSession? Find(string workspaceId) =>
        _sessions.TryGetValue(workspaceId, out var session) ? session : null;

    public void Add(EditWorkspaceSession session)
    {
        _sessions[session.Workspace.Id] = session;
        Changed?.Invoke();
    }

    /// <summary>
    /// Discards a workspace's flow, stopping a save that's running and removing what it added.
    /// </summary>
    public void Discard(string workspaceId)
    {
        if (_sessions.TryRemove(workspaceId, out var session))
        {
            session.CancelSaving();
            Changed?.Invoke();
        }
    }

    private void DiscardClosed()
    {
        foreach (var id in _sessions.Keys.Where(id => _tabs.Find(id) is null))
        {
            Discard(id);
        }
    }
}
