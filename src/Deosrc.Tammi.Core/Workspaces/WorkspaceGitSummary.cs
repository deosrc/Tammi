using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The git status of a workspace's repositories, as summarised on Home.
/// </summary>
/// <param name="Repositories">The workspace's repositories, in the workspace's order.</param>
public sealed record WorkspaceGitSummary(IReadOnlyList<HomeRepositoryStatus> Repositories)
{
    /// <summary>
    /// Whether every repository's status has been read (or found to be unreadable).
    /// </summary>
    public bool IsLoaded => Repositories.All(repository => repository.Git.IsLoaded);

    /// <summary>
    /// The repositories with uncommitted changes (staged, unstaged or untracked), in the repository or a submodule.
    /// </summary>
    public IReadOnlyList<WorkspaceRepository> LocalChanges =>
        [.. Repositories.Where(repository => repository.Git.Status is { } status && HasLocalChanges(status))
            .Select(repository => repository.Git.Repository)];

    /// <summary>
    /// The repositories with commits that aren't on any remote-tracking branch, in the repository or a submodule.
    /// </summary>
    public IReadOnlyList<WorkspaceRepository> Unpushed =>
        [.. Repositories.Where(repository => repository.Git.Status is { } status && HasUnpushedCommits(status))
            .Select(repository => repository.Git.Repository)];

    /// <summary>
    /// The repositories whose upstream has commits they don't. Separate from the changes above: being behind doesn't
    /// put work at risk.
    /// </summary>
    public IReadOnlyList<WorkspaceRepository> Behind =>
        [.. Repositories.Where(repository => repository.Git.Status?.IsBehind == true)
            .Select(repository => repository.Git.Repository)];

    /// <summary>
    /// Whether the workspace has Pull Requests and every one of them is merged.
    /// </summary>
    public bool AllPullRequestsMerged
    {
        get
        {
            var pullRequests = Repositories.SelectMany(repository => repository.PullRequests.PullRequests).ToArray();
            return pullRequests.Length > 0 && pullRequests.All(pullRequest => pullRequest.State == PullRequestState.Merged);
        }
    }

    /// <summary>
    /// The Pull Requests whose checks are still running.
    /// </summary>
    public IReadOnlyList<PullRequestInfo> ChecksRunning => PullRequestsWith(PullRequestChecks.InProgress);

    /// <summary>
    /// The Pull Requests whose checks failed.
    /// </summary>
    public IReadOnlyList<PullRequestInfo> ChecksFailed => PullRequestsWith(PullRequestChecks.Failed);

    private IReadOnlyList<PullRequestInfo> PullRequestsWith(PullRequestChecks checks) =>
        [.. Repositories.SelectMany(repository => repository.PullRequests.PullRequests)
            .Where(pullRequest => pullRequest.Checks == checks)];

    private static bool HasLocalChanges(RepositoryStatus status) =>
        status.HasUncommittedChanges
            || status.Submodules.Any(submodule => submodule.Status is { } inner && HasLocalChanges(inner));

    private static bool HasUnpushedCommits(RepositoryStatus status) =>
        status.HasUnpushedCommits
            || status.Submodules.Any(submodule => submodule.Status is { } inner && HasUnpushedCommits(inner));
}
