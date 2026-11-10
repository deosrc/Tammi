using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// What <see cref="WorkspaceEditor"/> changes in an existing workspace: its task list, and repositories to add as new
/// Workspace Worktrees.
/// </summary>
public sealed record WorkspaceEditRequest
{
    /// <summary>
    /// The workspace's tasks after the edit, replacing the current ones. A workspace can have none.
    /// </summary>
    public IReadOnlyList<TaskReference> Tasks { get; init; } = [];

    /// <summary>
    /// The branch to create in each added repository. It's the workspace's own branch when it already has repositories,
    /// and mustn't exist in the added repositories yet. Ignored when no repositories are added.
    /// </summary>
    public string BranchName { get; init; } = string.Empty;

    /// <summary>
    /// The repositories to add, in the order they're created and listed after the current ones.
    /// </summary>
    public IReadOnlyList<WorkspaceCreationRepository> AddedRepositories { get; init; } = [];
}
