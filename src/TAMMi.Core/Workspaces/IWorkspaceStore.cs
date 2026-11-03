namespace TAMMi.Core.Workspaces;

/// <summary>
/// Loads and saves workspaces, and keeps the index of known workspaces that Home lists.
/// </summary>
/// <remarks>
/// Each workspace describes itself in its <c>.tammi/</c> folder. The app's data folder keeps only an index of the
/// known workspaces' folders.
/// </remarks>
public interface IWorkspaceStore
{
    /// <summary>
    /// Why the index couldn't be read, or <see langword="null"/> if it was read (or doesn't exist yet). When it couldn't
    /// be read, no workspaces are known until some are saved or added.
    /// </summary>
    string? IndexProblem { get; }

    /// <summary>
    /// Raised after a workspace is saved, added or removed from the index. It can be raised on any thread.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Lists the known workspaces, in the order they were first saved or added, reading each one's metadata. Workspaces
    /// whose folder was moved or deleted are listed too, as <see cref="WorkspaceAvailability.Missing"/>.
    /// </summary>
    Task<IReadOnlyList<WorkspaceListing>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the workspace in a Workspace Folder. It doesn't need to be in the index.
    /// </summary>
    /// <exception cref="WorkspaceNotFoundException">The folder, or the metadata in it, is missing.</exception>
    /// <exception cref="InvalidWorkspaceMetadataException">The metadata can't be read.</exception>
    Task<Workspace> LoadAsync(string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a workspace's metadata to its folder, creating the folder if needed, and adds it to the index (or updates
    /// its entry).
    /// </summary>
    /// <returns>The saved workspace, with its folder as a full path.</returns>
    /// <exception cref="ArgumentException">A value isn't valid, e.g. the name is blank.</exception>
    /// <exception cref="WorkspaceException">
    /// The folder holds a different workspace, or the metadata can't be written.
    /// </exception>
    Task<Workspace> SaveAsync(Workspace workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the workspace in a folder and adds it to the index. If the workspace is already known (e.g. its folder was
    /// moved), its entry is pointed at the new folder.
    /// </summary>
    /// <exception cref="WorkspaceNotFoundException">The folder, or the metadata in it, is missing.</exception>
    /// <exception cref="InvalidWorkspaceMetadataException">The metadata can't be read.</exception>
    Task<Workspace> AddExistingAsync(string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a workspace from the index. Its folder and metadata are left alone.
    /// </summary>
    /// <returns>Whether the workspace was in the index.</returns>
    Task<bool> RemoveFromIndexAsync(string id, CancellationToken cancellationToken = default);
}
