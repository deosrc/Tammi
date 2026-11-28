namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// The text in Home's search box. It's kept here, not in the page, so it's still there when the user comes back to
/// Home.
/// </summary>
public sealed class HomeSearchState
{
    /// <summary>
    /// The search text, matched with <see cref="Core.Workspaces.WorkspaceSearch"/>.
    /// </summary>
    public string Text { get; set; } = "";
}
