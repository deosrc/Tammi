using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Wording for git status in the Workspace View's side panel.
/// </summary>
internal static class GitStatusText
{
    /// <summary>
    /// The number of uncommitted changes in a repository: changed files and changed submodule commits. Changes inside
    /// submodules are shown on the submodules.
    /// </summary>
    public static int UncommittedChangeCount(RepositoryStatus status) =>
        status.Changes.Count
            + status.Submodules.Count(submodule => submodule.CommitChange != SubmoduleCommitChange.None);

    /// <summary>
    /// Whether a repository has work that isn't pushed (uncommitted changes or unpushed commits) or is out of sync
    /// with its upstream (behind it), in the repository itself or in any of its submodules.
    /// </summary>
    public static bool NeedsAttention(RepositoryStatus status) =>
        status.IsChanged
            || status.IsBehind
            || status.Submodules.Any(submodule => submodule.Status is { } inner && NeedsAttention(inner));

    /// <summary>
    /// The changes that could be lost, e.g. "3 uncommitted changes · 1 unpushed commit", or <see langword="null"/>
    /// when there are none in the repository itself.
    /// </summary>
    public static string? Summary(RepositoryStatus status)
    {
        var parts = new List<string>();
        int changes = UncommittedChangeCount(status);
        if (changes > 0)
        {
            parts.Add(Count(changes, "uncommitted change"));
        }

        if (status.UnpushedCommitCount > 0)
        {
            parts.Add(Count(status.UnpushedCommitCount, "unpushed commit"));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// What HEAD is: the branch's name, or the commit's short SHA when HEAD is detached.
    /// </summary>
    public static string Head(RepositoryStatus status) => status switch
    {
        { Branch: { } branch } => branch,
        { HeadSha: { } sha } => sha[..Math.Min(7, sha.Length)],
        _ => "No commits",
    };

    /// <summary>
    /// The tooltip for an ahead or behind count, e.g. "2 commits ahead of origin/main".
    /// </summary>
    public static string AheadBehindTitle(int count, string relation, string upstream) =>
        $"{Count(count, "commit")} {relation} {upstream}";

    public static string CommitChange(SubmoduleCommitChange change) => change switch
    {
        SubmoduleCommitChange.Staged => "Commit changed (staged)",
        SubmoduleCommitChange.Unstaged => "Commit changed",
        _ => "Commit changed (staged and unstaged)",
    };

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
