namespace Deosrc.Tammi.UI;

/// <summary>
/// Paths of the app's pages, relative to the base URI.
/// </summary>
public static class AppRoutes
{
    public const string Home = "";

    public const string Settings = "settings";

    /// <summary>
    /// The AI usage page.
    /// </summary>
    public const string Usage = "usage";

    /// <summary>
    /// The settings sections, in the order they're listed. <see cref="Settings"/> shows the first one.
    /// </summary>
    public static IReadOnlyList<SettingsSection> SettingsSections { get; } =
    [
        new("General", Settings),
        new("Integrations", SettingsIntegrations),
        new("Git", SettingsGit),
        new("Repositories", SettingsRepositories),
        new("Agents", Settings + "/agents"),
        new("Tools", SettingsTools),
        new("Visualiser", SettingsVisualiser),
        new("About", About),
    ];

    public const string SettingsIntegrations = Settings + "/integrations";

    public const string SettingsGit = Settings + "/git";

    public const string SettingsRepositories = Settings + "/repositories";

    public const string SettingsTools = Settings + "/tools";

    public const string SettingsVisualiser = Settings + "/visualiser";

    public const string About = Settings + "/about";

    public const string ThirdPartySoftware = About + "/third-party";

    /// <summary>
    /// The Create Workspace flow. It isn't under <c>workspaces/</c>, so it can't clash with a workspace's tab.
    /// </summary>
    public const string NewWorkspace = "new-workspace";

    private const string EditWorkspacePrefix = "edit-workspace/";

    /// <summary>
    /// The path of the flow that changes a workspace's tasks and repositories. It isn't under <c>workspaces/</c>, so it
    /// can't clash with the workspace's tab.
    /// </summary>
    public static string EditWorkspace(string workspaceId) =>
        EditWorkspacePrefix + Uri.EscapeDataString(workspaceId);

    private const string WorkspacePrefix = "workspaces/";

    /// <summary>
    /// The path of a workspace's tab.
    /// </summary>
    public static string Workspace(string workspaceId) => WorkspacePrefix + Uri.EscapeDataString(workspaceId);

    /// <summary>
    /// The path of a workspace's tab, starting the Delete Workspace flow in it.
    /// </summary>
    public static string DeleteWorkspace(string workspaceId) => Workspace(workspaceId) + "?delete=true";

    /// <summary>
    /// Gets the workspace a path is the tab of.
    /// </summary>
    /// <param name="path">A path relative to the base URI, without its query or fragment.</param>
    public static string? WorkspaceIdOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        if (!trimmed.StartsWith(WorkspacePrefix, StringComparison.Ordinal) || trimmed.Length == WorkspacePrefix.Length
            || trimmed.IndexOf('/', WorkspacePrefix.Length) >= 0)
        {
            return null;
        }

        return Uri.UnescapeDataString(trimmed[WorkspacePrefix.Length..]);
    }
}
