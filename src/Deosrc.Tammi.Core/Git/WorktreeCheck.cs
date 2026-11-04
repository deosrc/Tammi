namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// The result of <see cref="IGitWorktreeService.CheckWorktree"/>.
/// </summary>
/// <param name="Path">The worktree folder that was checked.</param>
/// <param name="Health">Whether the worktree is connected to its Local Repository.</param>
/// <param name="GitDirectory">
/// The worktree's git directory in the Local Repository (e.g. <c>&lt;repo&gt;/.git/worktrees/api</c>), as the
/// worktree's <c>.git</c> file records it. It may not exist. <see langword="null"/> when the folder isn't a worktree.
/// </param>
/// <param name="RepositoryPath">
/// The Local Repository's working directory, worked out from <paramref name="GitDirectory"/>. It may not exist.
/// </param>
public sealed record WorktreeCheck(string Path, WorktreeHealth Health, string? GitDirectory, string? RepositoryPath)
{
    /// <summary>
    /// Whether the worktree needs repairing (or recreating) before git can use it.
    /// </summary>
    public bool IsBroken => Health != WorktreeHealth.Healthy;
}
