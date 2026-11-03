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
