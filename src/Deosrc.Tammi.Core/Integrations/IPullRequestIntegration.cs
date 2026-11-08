namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// An integration that hosts repositories and their Pull Requests, e.g. GitHub.
/// </summary>
public interface IPullRequestIntegration : IIntegration
{
    /// <summary>
    /// Lists the Pull Requests from a branch of a repository, whichever state they're in, e.g. to show them on a
    /// Workspace Worktree's card.
    /// </summary>
    /// <remarks>
    /// Each remote URL on this integration's service is a repository the Pull Requests can be on (e.g. a fork's
    /// <c>origin</c> and the <c>upstream</c> it was forked from) and a repository the branch can be pushed to. Only
    /// Pull Requests whose branch is in one of those repositories are listed, so another fork's branch with the same
    /// name isn't. Remote URLs on other services are ignored.
    /// </remarks>
    /// <param name="remoteUrls">The URLs of the repository's git remotes, fetch and push.</param>
    /// <param name="branch">The branch's name, e.g. <c>api-42-fix-login</c>.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The Pull Requests, oldest first in each repository, or none if no remote is on this integration's service.
    /// </returns>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationPermissionException">The credentials can't read Pull Requests.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(
        IReadOnlyList<string> remoteUrls,
        string branch,
        CancellationToken cancellationToken = default);
}
