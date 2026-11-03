namespace TAMMi.Core.Git;

/// <summary>
/// A commit in a repository's history.
/// </summary>
/// <param name="Sha">The full commit id.</param>
/// <param name="ParentShas">The parent commit ids. Merge commits have more than one.</param>
/// <param name="Author">Who wrote the change.</param>
/// <param name="Committer">Who created the commit.</param>
/// <param name="Subject">The first line of the commit message.</param>
/// <param name="Message">The full commit message.</param>
public sealed record CommitInfo(
    string Sha,
    IReadOnlyList<string> ParentShas,
    CommitSignature Author,
    CommitSignature Committer,
    string Subject,
    string Message);

/// <summary>
/// The author or committer of a commit.
/// </summary>
public sealed record CommitSignature(string Name, string Email, DateTimeOffset When);

/// <summary>
/// Which commits <see cref="IGitRepositoryReader.ReadHistory"/> returns.
/// </summary>
public sealed record HistoryOptions
{
    /// <summary>
    /// The most commits to return, or <see langword="null"/> for all of them.
    /// </summary>
    public int? MaxCount { get; init; } = 500;

    /// <summary>
    /// Include commits reachable from every local and remote-tracking branch and tag, not just HEAD. Use for the
    /// git graph.
    /// </summary>
    public bool AllRefs { get; init; }
}
