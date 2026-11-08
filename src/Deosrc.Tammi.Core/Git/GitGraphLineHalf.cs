namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Which half of a <see cref="GitGraphRow"/> a <see cref="GitGraphLine"/> is drawn in.
/// </summary>
public enum GitGraphLineHalf
{
    /// <summary>From the row's top edge to its middle, where the commit's dot is.</summary>
    Top,

    /// <summary>From the row's middle to its bottom edge.</summary>
    Bottom,
}
