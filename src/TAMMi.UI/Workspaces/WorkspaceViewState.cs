using TAMMi.Core.Terminals;

namespace TAMMi.UI.Workspaces;

/// <summary>
/// What each Workspace View was showing, so it shows the same again when the user comes back to it (e.g. from Home,
/// or after closing and reopening its tab).
/// </summary>
public sealed class WorkspaceViewState
{
    private readonly Dictionary<string, WorkspaceTerminal> _shownTerminals = [];

    /// <summary>
    /// The terminal a workspace's view was showing, or <c>null</c> if it wasn't showing one.
    /// </summary>
    public WorkspaceTerminal? GetShownTerminal(string workspaceId) => _shownTerminals.GetValueOrDefault(workspaceId);

    /// <summary>
    /// Sets the terminal a workspace's view shows.
    /// </summary>
    public void SetShownTerminal(string workspaceId, WorkspaceTerminal? terminal)
    {
        if (terminal is null)
        {
            _shownTerminals.Remove(workspaceId);
        }
        else
        {
            _shownTerminals[workspaceId] = terminal;
        }
    }
}
