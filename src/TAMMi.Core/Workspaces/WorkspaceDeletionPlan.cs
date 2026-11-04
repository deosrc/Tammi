namespace TAMMi.Core.Workspaces;

/// <summary>
/// What deleting a workspace would remove, and the work that would be lost, for the user to confirm. Made by
/// <see cref="WorkspaceDeleter.PlanAsync"/>.
/// </summary>
/// <param name="WorkspaceId">The workspace's <see cref="Workspace.Id"/>.</param>
/// <param name="Name">The workspace's name.</param>
/// <param name="Folder">The Workspace Folder.</param>
/// <param name="Scope">What deleting the workspace removes.</param>
public sealed record WorkspaceDeletionPlan(string WorkspaceId, string Name, string Folder, WorkspaceDeletionScope Scope)
{
    /// <summary>
    /// Why only the index entry is removed, when <see cref="Scope"/> is
    /// <see cref="WorkspaceDeletionScope.IndexEntryOnly"/>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Whether the workspace's metadata could be read. When it can't, the worktrees are found by looking in the
    /// Workspace Folder, and their branches aren't selected for deletion by default.
    /// </summary>
    public bool MetadataRead { get; init; }

    /// <summary>
    /// The worktrees to remove, in folder name order.
    /// </summary>
    public IReadOnlyList<WorktreeToDelete> Worktrees { get; init; } = [];

    /// <summary>
    /// The full paths of everything else in the Workspace Folder that would be deleted with it, other than TAMMi's own
    /// <c>.tammi/</c> folder: files and folders that aren't Workspace Worktrees.
    /// </summary>
    public IReadOnlyList<string> OtherContent { get; init; } = [];

    /// <summary>
    /// Whether deleting the workspace could lose work, so the user must confirm the worktrees and other content listed.
    /// </summary>
    public bool CouldLoseWork => OtherContent.Count > 0 || Worktrees.Any(worktree => worktree.NeedsConfirmation);
}
