using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Short descriptions of what's wrong with a broken Workspace Worktree, as shown in the Workspace View's side panel.
/// </summary>
internal static class WorktreeHealthText
{
    public static string Summary(WorktreeHealth health) => health switch
    {
        WorktreeHealth.Missing => "Worktree folder missing",
        WorktreeHealth.NotAWorktree => "Not a git worktree",
        WorktreeHealth.RepositoryMissing => "Local Repository moved or deleted",
        WorktreeHealth.Unregistered => "Not registered with its Local Repository",
        WorktreeHealth.Moved => "Worktree folder moved",
        _ => "Healthy",
    };
}
