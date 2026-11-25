namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The tabs of the main tab strip that were open, as <see cref="OpenWorkspaceTabsStore"/> keeps them.
/// </summary>
/// <param name="Tabs">
/// The tabs, in tab order, whatever they are: <see cref="SavedTab.WorkspaceKind"/> tabs (by <see cref="SavedTab.Id"/>)
/// and <see cref="SavedTab.RepositoryKind"/> tabs of a Local Repository outside any workspace (by
/// <see cref="SavedTab.Path"/>). A repository's own tabs are the git graphs of its submodules and other worktrees
/// (each by <see cref="SavedTab.Path"/>) besides its own, and its <see cref="SavedTab.Active"/> is the one that was
/// shown, or <c>null</c> if its own graph (or a terminal or agent) was.
/// </param>
/// <param name="Active">The tab that was shown, or <c>null</c> if another page was.</param>
public sealed record OpenWorkspaceTabs(IReadOnlyList<SavedTab> Tabs, SavedTab? Active = null)
{
    /// <summary>
    /// No tabs.
    /// </summary>
    public static OpenWorkspaceTabs None { get; } = new([]);
}
