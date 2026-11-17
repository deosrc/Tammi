namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads the output of <c>git worktree list --porcelain</c>.
/// </summary>
public static class GitWorktreeList
{
    private const string WorktreePrefix = "worktree ";
    private const string BranchPrefix = "branch refs/heads/";

    /// <summary>
    /// Parses the output's lines: a block for each worktree, starting with its path and separated by blank lines.
    /// The first block is the main working tree.
    /// </summary>
    public static IReadOnlyList<GitWorktreeEntry> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = new List<GitWorktreeEntry>();
        string? path = null;
        string? branch = null;

        void Finish()
        {
            if (path is not null)
            {
                entries.Add(new GitWorktreeEntry(path, branch, entries.Count == 0));
            }

            path = null;
            branch = null;
        }

        foreach (var line in lines)
        {
            if (line.StartsWith(WorktreePrefix, StringComparison.Ordinal))
            {
                Finish();
                path = line[WorktreePrefix.Length..];
            }
            else if (line.StartsWith(BranchPrefix, StringComparison.Ordinal))
            {
                branch = line[BranchPrefix.Length..];
            }
        }

        Finish();
        return entries;
    }
}
