using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// What <see cref="WorkspaceCreator"/> creates: a Workspace Folder holding a Workspace Worktree of each repository, all
/// on a new branch of the same name.
/// </summary>
public sealed record WorkspaceCreationRequest
{
    /// <summary>
    /// The workspace's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Workspace Folder's full path. It must not exist yet. Its parent folders are created if they're missing.
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    /// The workspace's tasks.
    /// </summary>
    public IReadOnlyList<TaskReference> Tasks { get; init; } = [];

    /// <summary>
    /// The branch to create in every repository. It mustn't exist in any of them yet.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// The repositories, in the order they're created and listed.
    /// </summary>
    public IReadOnlyList<WorkspaceCreationRepository> Repositories { get; init; } = [];
}
