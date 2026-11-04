namespace TAMMi.Core.Workspaces;

/// <summary>
/// There is no workspace in a folder: the folder, or the metadata in it, is missing.
/// </summary>
public sealed class WorkspaceNotFoundException(string message, string folder, Exception? innerException = null)
    : WorkspaceException(message, folder, innerException);
