namespace Deosrc.Tammi.UI;

/// <summary>
/// Paths of the app's pages, relative to the base URI.
/// </summary>
public static class AppRoutes
{
    public const string Home = "";

    public const string Settings = "settings";

    /// <summary>
    /// The Repositories page: the Repository Catalogue with each repository's git state.
    /// </summary>
    public const string Repositories = "repositories";

    private const string RepositoryGitPrefix = Repositories + "/git/";

    /// <summary>
    /// Between a workspace's or repository's path and the ID of one of the tabs in its main area.
    /// </summary>
    private const string TabsSegment = "/tabs/";

    /// <summary>
    /// Splits the part of a path after a workspace's or repository's prefix into the escaped owner and the escaped ID of
    /// the tab, if it has one. Returns <see langword="null"/> if there's anything else after the owner. The owner is
    /// escaped, so it has no slash of its own.
    /// </summary>
    private static (string Owner, string? TabId)? SplitTab(string rest)
    {
        var slash = rest.IndexOf('/');
        if (slash < 0)
        {
            return (rest, null);
        }

        var tab = rest[slash..];
        return tab.StartsWith(TabsSegment, StringComparison.Ordinal) && tab.Length > TabsSegment.Length
            && tab.IndexOf('/', TabsSegment.Length) < 0
                ? (rest[..slash], Uri.UnescapeDataString(tab[TabsSegment.Length..]))
                : null;
    }

    /// <summary>
    /// The path of the tab with the git graph of a Local Repository, opened outside any workspace.
    /// </summary>
    public static string RepositoryGit(string repositoryPath) => RepositoryGitPrefix + Uri.EscapeDataString(repositoryPath);

    /// <summary>
    /// Gets the repository whose git tab a path is, or <see langword="null"/> if it isn't one.
    /// </summary>
    /// <param name="path">A path relative to the base URI, without its query or fragment.</param>
    public static string? RepositoryGitPathOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed.StartsWith(RepositoryGitPrefix, StringComparison.Ordinal)
            && SplitTab(trimmed[RepositoryGitPrefix.Length..]) is { Owner.Length: > 0 } split
                ? Uri.UnescapeDataString(split.Owner)
                : null;
    }

    /// <summary>
    /// The path of one of the tabs in a repository's git tab, opened outside any workspace: a terminal, an agent or a git
    /// graph. <see cref="RepositoryGit"/> is the repository's own git graph.
    /// </summary>
    public static string RepositoryGitTab(string repositoryPath, string tabId) =>
        RepositoryGit(repositoryPath) + TabsSegment + Uri.EscapeDataString(tabId);

    /// <summary>
    /// Gets the ID of the tab a repository git path shows, or <see langword="null"/> if it's the repository's own git
    /// graph, or isn't a repository git path.
    /// </summary>
    /// <param name="path">A path relative to the base URI, without its query or fragment.</param>
    public static string? RepositoryGitTabIdOf(string path) =>
        path.TrimEnd('/') is var trimmed && trimmed.StartsWith(RepositoryGitPrefix, StringComparison.Ordinal)
            && SplitTab(trimmed[RepositoryGitPrefix.Length..]) is { Owner.Length: > 0 } split
                ? split.TabId
                : null;

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
        new("Updates", SettingsUpdates),
        new("About", About),
    ];

    public const string SettingsIntegrations = Settings + "/integrations";

    public const string SettingsGit = Settings + "/git";

    public const string SettingsRepositories = Settings + "/repositories";

    public const string SettingsTools = Settings + "/tools";

    public const string SettingsUpdates = Settings + "/updates";

    public const string About = Settings + "/about";

    public const string ThirdPartySoftware = About + "/third-party";

    /// <summary>
    /// The Create Workspace flow, which has its own tab. It isn't under <c>workspaces/</c>, so it can't clash with a workspace's tab.
    /// </summary>
    public const string NewWorkspace = "new-workspace";

    private const string EditWorkspacePrefix = "edit-workspace/";

    /// <summary>
    /// The path of the flow that changes a workspace's tasks and repositories. It is shown in the workspace's tab. It isn't under <c>workspaces/</c>, so it
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
    /// The path of one of the tabs in a workspace's main area: a terminal, an agent, a git graph or History.
    /// <see cref="Workspace"/> is the workspace's own dashboard.
    /// </summary>
    public static string WorkspaceTab(string workspaceId, string tabId) =>
        Workspace(workspaceId) + TabsSegment + Uri.EscapeDataString(tabId);

    /// <summary>
    /// Gets the ID of the main area tab a workspace path shows, or <see langword="null"/> if it's the workspace's
    /// dashboard, or isn't a workspace path.
    /// </summary>
    /// <param name="path">A path relative to the base URI, without its query or fragment.</param>
    public static string? WorkspaceTabIdOf(string path) =>
        path.TrimEnd('/') is var trimmed && trimmed.StartsWith(WorkspacePrefix, StringComparison.Ordinal)
            && SplitTab(trimmed[WorkspacePrefix.Length..]) is { Owner.Length: > 0 } split
                ? split.TabId
                : null;

    /// <summary>
    /// The path of a workspace's tab, starting the Delete Workspace flow in it.
    /// </summary>
    public static string DeleteWorkspace(string workspaceId) => Workspace(workspaceId) + "?delete=true";

    /// <summary>
    /// Gets the workspace a path is shown in the tab of: the workspace's own page or one of its main area tabs, or its
    /// edit flow.
    /// </summary>
    /// <param name="path">A path relative to the base URI, without its query or fragment.</param>
    public static string? WorkspaceIdOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        if (trimmed.StartsWith(EditWorkspacePrefix, StringComparison.Ordinal) && trimmed.Length > EditWorkspacePrefix.Length
            && trimmed.IndexOf('/', EditWorkspacePrefix.Length) < 0)
        {
            return Uri.UnescapeDataString(trimmed[EditWorkspacePrefix.Length..]);
        }

        return trimmed.StartsWith(WorkspacePrefix, StringComparison.Ordinal)
            && SplitTab(trimmed[WorkspacePrefix.Length..]) is { Owner.Length: > 0 } split
                ? Uri.UnescapeDataString(split.Owner)
                : null;
    }
}
