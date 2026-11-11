namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// The GraphQL query for the Pull Requests from a branch of a repository, with the combined state of each one's checks.
/// GitHub matches the branch by name only, whichever repository it's in, so each Pull Request's head repository is
/// read too.
/// </summary>
internal static class GitHubPullRequestsQuery
{
    /// <summary>
    /// The most Pull Requests read from one branch of one repository.
    /// </summary>
    public const int MaxPullRequests = 20;

    public const string Text = """
        query($owner: String!, $name: String!, $branch: String!, $first: Int!) {
          repository(owner: $owner, name: $name) {
            nameWithOwner
            pullRequests(headRefName: $branch, first: $first, orderBy: {field: CREATED_AT, direction: ASC}) {
              nodes {
                number
                title
                url
                state
                isDraft
                reviewDecision
                headRepository { url }
                commits(last: 1) { nodes { commit { statusCheckRollup { state } } } }
              }
            }
          }
        }
        """;

    /// <param name="Owner">The repository's owner, e.g. <c>acme</c>.</param>
    /// <param name="Name">The repository's name, e.g. <c>api</c>.</param>
    /// <param name="Branch">The head branch's name.</param>
    /// <param name="First">How many Pull Requests to read.</param>
    public sealed record Variables(string Owner, string Name, string Branch, int First = MaxPullRequests);

    /// <param name="Repository">The repository, or <see langword="null"/> if it doesn't exist or can't be read.</param>
    public sealed record Data(Repository? Repository);

    /// <param name="PullRequests">
    /// Its Pull Requests from the branch, or <see langword="null"/> if the token can't read them.
    /// </param>
    public sealed record Repository(string NameWithOwner, Connection<PullRequest>? PullRequests);

    public sealed record Connection<TNode>(IReadOnlyList<TNode?>? Nodes);

    /// <param name="State"><c>OPEN</c>, <c>CLOSED</c> or <c>MERGED</c>.</param>
    /// <param name="ReviewDecision">
    /// <c>APPROVED</c>, <c>CHANGES_REQUESTED</c> or <c>REVIEW_REQUIRED</c>, or <see langword="null"/> when no review is
    /// required.
    /// </param>
    /// <param name="HeadRepository">
    /// The repository the branch is in, or <see langword="null"/> if it was deleted.
    /// </param>
    /// <param name="Commits">Its latest commit.</param>
    public sealed record PullRequest(
        int Number,
        string Title,
        string Url,
        string State,
        bool IsDraft,
        string? ReviewDecision,
        HeadRepository? HeadRepository,
        Connection<CommitNode>? Commits);

    public sealed record HeadRepository(string Url);

    public sealed record CommitNode(Commit? Commit);

    /// <param name="StatusCheckRollup">
    /// The combined state of its checks and commit statuses, or <see langword="null"/> when it has none or the token
    /// can't read them.
    /// </param>
    public sealed record Commit(StatusCheckRollup? StatusCheckRollup);

    /// <param name="State">
    /// The most severe state of its checks: <c>ERROR</c>, <c>FAILURE</c>, <c>PENDING</c>, <c>EXPECTED</c> or
    /// <c>SUCCESS</c>.
    /// </param>
    public sealed record StatusCheckRollup(string State);
}
