using TAMMi.Core.Integrations;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// A named collection of tasks and repositories in a Workspace Folder. Its metadata is kept in the folder's
/// <c>.tammi/</c> folder (see <see cref="WorkspaceStore"/>).
/// </summary>
public sealed record Workspace
{
    /// <summary>
    /// The name of the folder in the Workspace Folder that holds TAMMi's files for the workspace.
    /// </summary>
    public const string MetadataFolderName = ".tammi";

    /// <summary>
    /// Identifies the workspace. It doesn't change when the workspace is renamed or its folder is moved.
    /// </summary>
    /// <remarks>
    /// It is made of letters, digits, <c>-</c> and <c>_</c>, so it can be used in routes. Use <see cref="NewId"/> for a
    /// new workspace.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// The workspace's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Workspace Folder's full path. It isn't kept in the metadata: it is wherever the metadata was read from.
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    /// The workspace's tasks. A workspace can have none.
    /// </summary>
    public IReadOnlyList<TaskReference> Tasks { get; init; } = [];

    /// <summary>
    /// The workspace's repositories, each a Workspace Worktree in the Workspace Folder.
    /// </summary>
    public IReadOnlyList<WorkspaceRepository> Repositories { get; init; } = [];

    /// <summary>
    /// The full path of the workspace's <c>.tammi/</c> folder.
    /// </summary>
    public string MetadataFolder => Path.Combine(Folder, MetadataFolderName);

    /// <summary>
    /// Returns a new, unique <see cref="Id"/>.
    /// </summary>
    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Returns the full path of a repository's Workspace Worktree.
    /// </summary>
    public string GetWorktreePath(WorkspaceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Path.Combine(Folder, repository.FolderName);
    }
}
