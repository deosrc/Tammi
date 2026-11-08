namespace Deosrc.Tammi.UI;

/// <summary>
/// Paths of the app's pages, relative to the base URI.
/// </summary>
public static class AppRoutes
{
    public const string Home = "";

    public const string Settings = "settings";

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
        new("About", About),
    ];

    public const string SettingsIntegrations = Settings + "/integrations";

    public const string SettingsGit = Settings + "/git";

    public const string SettingsRepositories = Settings + "/repositories";

    public const string SettingsTools = Settings + "/tools";

    public const string About = Settings + "/about";

    public const string ThirdPartySoftware = About + "/third-party";

    /// <summary>
    /// The Create Workspace flow. It isn't under <c>workspaces/</c>, so it can't clash with a workspace's tab.
    /// </summary>
    public const string NewWorkspace = "new-workspace";

    private const string WorkspacePrefix = "workspaces/";

    /// <summary>
    /// The path of a workspace's tab.
    /// </summary>
    public static string Workspace(string workspaceId) => WorkspacePrefix + Uri.EscapeDataString(workspaceId);
}
