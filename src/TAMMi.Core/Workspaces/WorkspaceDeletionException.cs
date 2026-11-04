namespace TAMMi.Core.Workspaces;

/// <summary>
/// A workspace couldn't be deleted, or was only partly deleted. What's left can be deleted by trying again, which
/// checks it again first.
/// </summary>
public sealed class WorkspaceDeletionException(string message, string folder, Exception? innerException = null)
    : WorkspaceException(message, folder, innerException);
