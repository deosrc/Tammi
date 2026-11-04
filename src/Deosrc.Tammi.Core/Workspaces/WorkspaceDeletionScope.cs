namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// What deleting a workspace removes.
/// </summary>
public enum WorkspaceDeletionScope
{
    /// <summary>
    /// The Workspace Folder, with its worktrees, and the workspace's index entry.
    /// </summary>
    Folder,

    /// <summary>
    /// Only the workspace's index entry. The folder is missing, or no longer holds this workspace, so it's left alone.
    /// </summary>
    IndexEntryOnly,
}
