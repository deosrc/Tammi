namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// Which commits the git graph shows.
/// </summary>
public enum GitGraphScope
{
    /// <summary>
    /// The workspace branch and what it depends on: HEAD, the workspace branch, its upstream and its base branch. A
    /// submodule shows HEAD and its branch's upstream.
    /// </summary>
    WorkspaceBranch,

    /// <summary>
    /// Every local and remote-tracking branch and tag.
    /// </summary>
    Everything,
}
