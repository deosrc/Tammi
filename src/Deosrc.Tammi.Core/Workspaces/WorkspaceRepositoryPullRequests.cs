using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The Pull Requests from one of a workspace's repositories' workspace branch, as last read from its integrations.
/// </summary>
/// <param name="Repository">The repository in the workspace's metadata.</param>
public sealed record WorkspaceRepositoryPullRequests(WorkspaceRepository Repository)
{
    /// <summary>
    /// Whether they've been read at least once, whether or not that worked.
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// The Pull Requests, from every integration the repository's remotes are on. When reading them fails, these are
    /// the ones read before.
    /// </summary>
    public IReadOnlyList<PullRequestInfo> PullRequests { get; init; } = [];

    /// <summary>
    /// Why they couldn't be read the last time, or <see langword="null"/> if they could.
    /// </summary>
    public string? Problem { get; init; }
}
