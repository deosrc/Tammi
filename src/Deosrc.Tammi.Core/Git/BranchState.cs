namespace TAMMi.Core.Git;

/// <summary>
/// Whether a branch can be deleted without losing work, as offered when deleting a workspace.
/// </summary>
/// <param name="Exists">Whether the local branch exists.</param>
/// <param name="IsMerged">Whether the branch's tip is reachable from the merge target, e.g. <c>origin/main</c>.</param>
/// <param name="IsPushed">
/// Whether every commit on the branch is on a remote-tracking branch. Like
/// <see cref="RepositoryStatus.UnpushedCommitCount"/>, this doesn't depend on the branch's upstream. A branch with no
/// commits of its own counts as pushed.
/// </param>
public sealed record BranchState(bool Exists, bool IsMerged, bool IsPushed)
{
    /// <summary>
    /// A branch that doesn't exist.
    /// </summary>
    public static BranchState Missing { get; } = new(false, false, false);

    /// <summary>
    /// Whether deleting the branch loses no work, so it's selected for deletion by default.
    /// </summary>
    public bool IsSafeToDelete => Exists && (IsMerged || IsPushed);
}
