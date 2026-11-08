namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// How a git graph's details show the selected commit's changed files.
/// </summary>
public enum GitGraphFileView
{
    /// <summary>One line for each file: its name, then its folder.</summary>
    List,

    /// <summary>The files in their folders.</summary>
    Tree,
}
