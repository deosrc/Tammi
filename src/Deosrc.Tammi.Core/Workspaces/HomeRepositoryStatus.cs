namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// One of a workspace's repositories, as summarised on Home: its git status and its Pull Requests.
/// </summary>
/// <param name="Git">The repository's git status.</param>
/// <param name="PullRequests">The Pull Requests from the repository's workspace branch.</param>
public sealed record HomeRepositoryStatus(WorkspaceRepositoryStatus Git, WorkspaceRepositoryPullRequests PullRequests);
