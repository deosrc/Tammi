namespace TAMMi.Core.Workspaces;

/// <summary>
/// Why a workspace's task couldn't be read.
/// </summary>
public enum WorkspaceTaskProblem
{
    /// <summary>
    /// The task's source isn't connected, e.g. its integration was removed.
    /// </summary>
    SourceNotConnected,

    /// <summary>
    /// The source says the task doesn't exist, or the user can't see it.
    /// </summary>
    NotFound,

    /// <summary>
    /// The source couldn't be reached, rejected the credentials or returned an error.
    /// </summary>
    ReadFailed,
}
