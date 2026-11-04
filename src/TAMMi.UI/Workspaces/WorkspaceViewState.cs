using TAMMi.Core.Workspaces;

namespace TAMMi.UI.Workspaces;

/// <summary>
/// What each Workspace View was showing, so it shows the same again when the user comes back to it (e.g. from Home,
/// or after closing and reopening its tab).
/// </summary>
public sealed class WorkspaceViewState
{
    private readonly Dictionary<string, IWorkspaceProcess> _shown = [];

    /// <summary>
    /// The terminal or agent a workspace's view was showing, or <c>null</c> if it was showing the dashboard.
    /// </summary>
    public IWorkspaceProcess? GetShown(string workspaceId) => _shown.GetValueOrDefault(workspaceId);

    /// <summary>
    /// Sets the terminal or agent a workspace's view shows, or <c>null</c> for the dashboard.
    /// </summary>
    public void SetShown(string workspaceId, IWorkspaceProcess? process)
    {
        if (process is null)
        {
            _shown.Remove(workspaceId);
        }
        else
        {
            _shown[workspaceId] = process;
        }
    }
}
