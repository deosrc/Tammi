namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Something shown as a tab in the main area of a workspace's Workspace View, after the dashboard: a terminal, an
/// agent, a git graph or the History tab.
/// </summary>
public interface IMainAreaTab
{
    /// <summary>
    /// The ID of the workspace the tab belongs to.
    /// </summary>
    string WorkspaceId { get; }

    /// <summary>
    /// The name its tab shows.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Orders the tabs by when they started or opened, across terminals, agents and git graphs: a later tab has a
    /// higher number. Taken from <see cref="MainAreaTabOrder.Next"/>.
    /// </summary>
    long StartOrder { get; }
}
