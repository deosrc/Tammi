using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// How the git graph writes commits and their details.
/// </summary>
public static class GitGraphText
{
    /// <summary>
    /// A commit id shortened to its first 7 characters, as git writes it.
    /// </summary>
    public static string ShortSha(string sha) => sha.Length > 7 ? sha[..7] : sha;

    /// <summary>
    /// How long ago something happened, e.g. "5 minutes ago", or the date for anything more than a month ago.
    /// </summary>
    public static string When(DateTimeOffset when, DateTimeOffset now)
    {
        var age = now - when;
        return age switch
        {
            { TotalMinutes: < 1 } => "Just now",
            { TotalHours: < 1 } => Ago((int)age.TotalMinutes, "minute"),
            { TotalDays: < 1 } => Ago((int)age.TotalHours, "hour"),
            { TotalDays: < 31 } => Ago((int)age.TotalDays, "day"),
            _ => when.ToLocalTime().ToString("d"),
        };
    }

    /// <summary>
    /// The full date and time, e.g. for a tooltip.
    /// </summary>
    public static string FullDate(DateTimeOffset when) => when.ToLocalTime().ToString("f");

    /// <summary>
    /// A commit's message without its subject line, or an empty string if it has nothing else.
    /// </summary>
    public static string Body(CommitInfo commit)
    {
        var newline = commit.Message.IndexOf('\n');
        return newline < 0 ? "" : commit.Message[(newline + 1)..].Trim();
    }

    /// <summary>
    /// A changed file's kind as git's one-letter status, e.g. "M" for modified.
    /// </summary>
    public static string Letter(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Added => "A",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => "R",
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.Conflicted => "U",
        _ => "M",
    };

    /// <summary>
    /// A changed file's kind in words, e.g. for a tooltip.
    /// </summary>
    public static string Label(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Added => "Added",
        FileChangeKind.Deleted => "Deleted",
        FileChangeKind.Renamed => "Renamed",
        FileChangeKind.TypeChanged => "Type changed",
        FileChangeKind.Conflicted => "Conflicted",
        _ => "Modified",
    };

    /// <summary>
    /// The CSS class of a branch or tag label.
    /// </summary>
    public static string ReferenceClass(GitReferenceKind kind) => kind switch
    {
        GitReferenceKind.LocalBranch => "git-graph-ref-local",
        GitReferenceKind.RemoteBranch => "git-graph-ref-remote",
        _ => "git-graph-ref-tag",
    };

    /// <summary>
    /// Groups a commit's labels so a local branch and its remote-tracking branch (e.g. <c>main</c> and
    /// <c>origin/main</c>) share one badge. Other labels stay on their own. Order follows the first label of each group.
    /// </summary>
    public static IReadOnlyList<GitReferenceBadge> CombineBranches(IEnumerable<GitReference> references)
    {
        var list = references.ToList();
        var badges = new List<GitReferenceBadge>();
        var used = new HashSet<GitReference>();
        foreach (var reference in list)
        {
            if (used.Contains(reference))
            {
                continue;
            }

            if (reference.Kind == GitReferenceKind.LocalBranch)
            {
                var remotes = list.Where(other => other.Kind == GitReferenceKind.RemoteBranch
                                                  && other.Name.EndsWith("/" + reference.Name, StringComparison.Ordinal)
                                                  && !used.Contains(other)).ToList();
                used.UnionWith(remotes);
                badges.Add(new GitReferenceBadge(reference, remotes));
            }
            else if (reference.Kind == GitReferenceKind.RemoteBranch)
            {
                var local = list.FirstOrDefault(other => other.Kind == GitReferenceKind.LocalBranch
                                                         && reference.Name.EndsWith("/" + other.Name, StringComparison.Ordinal));
                if (local is not null)
                {
                    continue; // Rendered with its local branch.
                }

                badges.Add(new GitReferenceBadge(reference, []));
            }
            else
            {
                badges.Add(new GitReferenceBadge(reference, []));
            }
        }

        return badges;
    }

    private static string Ago(int count, string unit) => count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";
}
