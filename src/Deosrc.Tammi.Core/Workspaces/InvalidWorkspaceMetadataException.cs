namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A workspace's metadata is there but can't be read, e.g. it isn't valid JSON or a value isn't valid.
/// </summary>
public sealed class InvalidWorkspaceMetadataException(string message, string folder, Exception? innerException = null)
    : WorkspaceException(message, folder, innerException);
