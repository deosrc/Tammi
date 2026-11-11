namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// How Home arranges workspaces into tabs and groups.
/// </summary>
public enum HomeLayout
{
    /// <summary>
    /// A tab for each source, and a group for each status.
    /// </summary>
    TabBySourceGroupByStatus,

    /// <summary>
    /// A tab for each status, and a group for each source.
    /// </summary>
    TabByStatusGroupBySource,
}
