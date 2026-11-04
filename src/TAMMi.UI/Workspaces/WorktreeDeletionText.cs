using TAMMi.Core.Git;
using TAMMi.Core.Workspaces;

namespace TAMMi.UI.Workspaces;

/// <summary>
/// Describes what deleting a workspace removes, for the Delete Workspace flow.
/// </summary>
internal static class WorktreeDeletionText
{
    /// <summary>
    /// What deleting the workspace removes, as a sentence.
    /// </summary>
    public static string DescribeRemoval(WorkspaceDeletionPlan plan) => plan.Worktrees.Count switch
    {
        0 => $"This deletes the workspace folder '{plan.Folder}'.",
        1 => $"This deletes the workspace folder '{plan.Folder}' and removes its worktree from its local repository.",
        var count => $"This deletes the workspace folder '{plan.Folder}' and removes its {count} worktrees from their "
            + "local repositories.",
    };

    /// <summary>
    /// The work in a worktree that would be lost, e.g. "2 uncommitted changes · 1 unpushed commit".
    /// </summary>
    public static string Describe(WorktreeToDelete worktree)
    {
        if (worktree.Problem is not null)
        {
            return worktree.Problem;
        }

        if (worktree.Status is not { } status)
        {
            return "";
        }

        var parts = new List<string>();
        var uncommitted = status.Changes.Count
            + status.Submodules.Count(submodule => submodule.CommitChange != SubmoduleCommitChange.None);
        if (uncommitted > 0)
        {
            parts.Add(Count(uncommitted, "uncommitted change"));
        }

        if (status.UnpushedCommitCount > 0)
        {
            parts.Add(Count(status.UnpushedCommitCount, "unpushed commit"));
        }

        if (status.Submodules.Any(submodule => submodule.Status?.IsChanged == true))
        {
            parts.Add("changes in submodules");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Whether a branch is merged or pushed, e.g. "Merged" or "Not pushed".
    /// </summary>
    public static string Describe(WorkspaceBranch branch)
    {
        var state = branch.State switch
        {
            { IsMerged: true } => "Merged",
            { IsPushed: true } => "Pushed",
            _ => "Not merged or pushed",
        };
        return branch.IsWorkspaceBranch ? state : $"{state} · not in the workspace's metadata";
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
