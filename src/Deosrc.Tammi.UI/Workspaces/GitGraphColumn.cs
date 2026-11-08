namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// A column of the git graph's commit list, in the order they're shown.
/// </summary>
public enum GitGraphColumn
{
    /// <summary>The commit's lane and dot.</summary>
    Graph,

    /// <summary>The commit's branch and tag labels, and its subject. It's always shown.</summary>
    Description,

    /// <summary>The commit's author.</summary>
    Author,

    /// <summary>When the commit was written.</summary>
    Date,

    /// <summary>The commit's short id.</summary>
    Commit,
}
