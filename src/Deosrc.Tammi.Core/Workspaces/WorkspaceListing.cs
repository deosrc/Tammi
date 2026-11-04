namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A known workspace, as listed on Home.
/// </summary>
/// <param name="Id">The workspace's <see cref="Workspace.Id"/>.</param>
/// <param name="Name">
/// The workspace's name: from its metadata when it could be read, otherwise as last seen.
/// </param>
/// <param name="Folder">The Workspace Folder, as last seen.</param>
/// <param name="Availability">Whether the workspace's metadata could be read.</param>
public sealed record WorkspaceListing(string Id, string Name, string Folder, WorkspaceAvailability Availability)
{
    /// <summary>
    /// The workspace, when it is <see cref="WorkspaceAvailability.Available"/>.
    /// </summary>
    public Workspace? Workspace { get; init; }

    /// <summary>
    /// Why the workspace isn't available, or <see langword="null"/> when it is.
    /// </summary>
    public string? Problem { get; init; }
}
