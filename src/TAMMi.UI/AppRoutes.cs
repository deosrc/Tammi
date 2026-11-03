namespace TAMMi.UI;

/// <summary>
/// Paths of the app's pages, relative to the base URI.
/// </summary>
public static class AppRoutes
{
    public const string Home = "";

    public const string Settings = "settings";

    private const string WorkspacePrefix = "workspaces/";

    /// <summary>
    /// The path of a workspace's tab.
    /// </summary>
    public static string Workspace(string workspaceId) => WorkspacePrefix + Uri.EscapeDataString(workspaceId);
}
