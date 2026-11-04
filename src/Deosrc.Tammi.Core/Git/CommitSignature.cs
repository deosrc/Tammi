namespace TAMMi.Core.Git;

/// <summary>
/// The author or committer of a commit.
/// </summary>
public sealed record CommitSignature(string Name, string Email, DateTimeOffset When);
