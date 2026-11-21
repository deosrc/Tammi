using System.Globalization;

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
    /// The name its tab shows: <see cref="Name"/>, unless the tab's program has renamed it, as a terminal or agent
    /// does by setting its terminal's title.
    /// </summary>
    string DisplayName => Name;

    /// <summary>
    /// Orders the tabs by when they started or opened, across terminals, agents and git graphs: a later tab has a
    /// higher number. Taken from <see cref="MainAreaTabOrder.Next"/>.
    /// </summary>
    long StartOrder { get; }

    /// <summary>
    /// The ID of the tab, for its address: it's unique among all the main area tabs while the app runs, so no two tabs
    /// of any workspace share one. It isn't kept between runs, as the tabs aren't: they're restored as new tabs.
    /// </summary>
    string Id => StartOrder.ToString(CultureInfo.InvariantCulture);
}
