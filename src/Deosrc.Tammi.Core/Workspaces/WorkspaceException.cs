namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A workspace's metadata couldn't be read or written.
/// </summary>
public class WorkspaceException : Exception
{
    public WorkspaceException(string message, string folder, Exception? innerException = null)
        : base(message, innerException)
    {
        Folder = folder;
    }

    /// <summary>
    /// The Workspace Folder.
    /// </summary>
    public string Folder { get; }
}
