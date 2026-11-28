namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// Requests, from outside the main window's pages (e.g. the visualiser's window), to open a workspace in it.
/// </summary>
public sealed class WorkspaceOpenRequests
{
    /// <summary>
    /// Raised with a workspace's ID when it should be opened, or shown if it's already open. It can be raised on any
    /// thread.
    /// </summary>
    public event Action<string>? Requested;

    /// <summary>
    /// Asks for the workspace to be opened.
    /// </summary>
    public void Request(string workspaceId) => Requested?.Invoke(workspaceId);
}
