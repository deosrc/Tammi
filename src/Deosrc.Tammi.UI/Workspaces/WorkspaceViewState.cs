using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// What each Workspace View was showing, so it shows the same again when the user comes back to it (e.g. from Home,
/// or after closing and reopening its tab).
/// </summary>
public sealed class WorkspaceViewState
{
    private readonly Dictionary<string, IMainAreaTab> _shown = [];

    /// <summary>
    /// The main area tab a workspace's view was showing, or <c>null</c> if it was showing the dashboard.
    /// </summary>
    public IMainAreaTab? GetShown(string workspaceId) => _shown.GetValueOrDefault(workspaceId);

    /// <summary>
    /// The path to open a workspace's tab at: its view as it was last shown, or the dashboard.
    /// </summary>
    public string RouteOf(string workspaceId) =>
        GetShown(workspaceId) is { } tab ? AppRoutes.WorkspaceTab(workspaceId, tab.Id) : AppRoutes.Workspace(workspaceId);

    /// <summary>
    /// The path to open a repository's git tab at: its secondary tab as it was last shown, or its own git graph.
    /// </summary>
    public string RepositoryRouteOf(string repositoryPath) =>
        GetShown(StandaloneRepository.IdFor(repositoryPath)) is { } tab
            ? AppRoutes.RepositoryGitTab(repositoryPath, tab.Id)
            : AppRoutes.RepositoryGit(repositoryPath);

    /// <summary>
    /// Sets the main area tab a workspace's view shows, or <c>null</c> for the dashboard.
    /// </summary>
    public void SetShown(string workspaceId, IMainAreaTab? tab)
    {
        if (tab is null)
        {
            _shown.Remove(workspaceId);
        }
        else
        {
            _shown[workspaceId] = tab;
        }
    }
}
