namespace TAMMi.UI;

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
        new("Integrations", Settings + "/integrations"),
        new("Git", Settings + "/git"),
        new("Agents", Settings + "/agents"),
        new("Tools", Settings + "/tools"),
        new("About", About),
    ];

    public const string About = Settings + "/about";

    public const string ThirdPartySoftware = About + "/third-party";

    /// <summary>
    /// A developer page for trying the embedded terminal.
    /// </summary>
    public const string DevTerminal = "dev/terminal";

    /// <summary>
    /// A developer page for starting and stopping agents.
    /// </summary>
    public const string DevAgent = "dev/agent";

    private const string WorkspacePrefix = "workspaces/";

    /// <summary>
    /// The path of a workspace's tab.
    /// </summary>
    public static string Workspace(string workspaceId) => WorkspacePrefix + Uri.EscapeDataString(workspaceId);
}
