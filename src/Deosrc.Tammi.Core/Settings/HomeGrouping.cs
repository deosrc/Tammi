namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// What Home splits workspaces by, for its tabs or for the groups in a tab.
/// </summary>
public enum HomeGrouping
{
    /// <summary>
    /// Not split.
    /// </summary>
    None,

    /// <summary>
    /// By the source of the workspace's tasks.
    /// </summary>
    Source,

    /// <summary>
    /// By the workspace's status.
    /// </summary>
    Status,
}
