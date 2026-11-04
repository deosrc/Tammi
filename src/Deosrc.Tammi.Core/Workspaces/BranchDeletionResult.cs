namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The outcome of deleting one of a deleted workspace's branches.
/// </summary>
/// <param name="Branch">The branch.</param>
/// <param name="Error">Why the branch couldn't be deleted, or <see langword="null"/> if it was.</param>
public sealed record BranchDeletionResult(WorkspaceBranch Branch, string? Error)
{
    /// <summary>
    /// Whether the branch was deleted.
    /// </summary>
    public bool Succeeded => Error is null;
}
