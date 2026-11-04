namespace TAMMi.Core.Git;

/// <summary>
/// A worktree wasn't removed because it holds work that would be lost: uncommitted changes or unpushed commits, in the
/// worktree or its submodules. Ask the user to confirm, then remove it with <c>force</c>.
/// </summary>
public sealed class WorktreeChangedException(RepositoryStatus status)
    : GitException($"'{status.Path}' has uncommitted changes or unpushed commits.")
{
    /// <summary>
    /// The worktree's status, listing the changes.
    /// </summary>
    public RepositoryStatus Status { get; } = status;
}
