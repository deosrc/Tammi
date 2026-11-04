namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Whether a known workspace's metadata could be read when the workspaces were listed.
/// </summary>
public enum WorkspaceAvailability
{
    /// <summary>
    /// The metadata was read.
    /// </summary>
    Available,

    /// <summary>
    /// The Workspace Folder, or the metadata in it, is missing: the folder was moved or deleted. If it was moved, the
    /// user can add it again from its new location (<see cref="IWorkspaceStore.AddExistingAsync"/>).
    /// </summary>
    Missing,

    /// <summary>
    /// The metadata is there but can't be read, e.g. it isn't valid or it describes a different workspace.
    /// </summary>
    Unreadable,
}
