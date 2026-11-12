using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Works out a workspace's <see cref="WorkspaceStatus"/>.
/// </summary>
public static class WorkspaceStatuses
{
    /// <summary>
    /// The statuses in the order Home lists them until the user changes it.
    /// </summary>
    public static IReadOnlyList<WorkspaceStatus> DefaultOrder { get; } =
    [
        WorkspaceStatus.Done,
        WorkspaceStatus.RequiresAttention,
        WorkspaceStatus.InReview,
        WorkspaceStatus.InDevelopment,
        WorkspaceStatus.Planning,
    ];

    /// <summary>
    /// Returns the text for a status.
    /// </summary>
    public static string Label(WorkspaceStatus status) => status switch
    {
        WorkspaceStatus.Done => "Done",
        WorkspaceStatus.InReview => "In Review",
        WorkspaceStatus.RequiresAttention => "Requires Attention",
        WorkspaceStatus.InDevelopment => "In Development",
        _ => "Planning",
    };

    /// <summary>
    /// Returns a status from the git status, Pull Requests and agents of a workspace. A workspace that can't be read
    /// requires attention.
    /// </summary>
    /// <param name="summary">The workspace's git summary, or <see langword="null"/> if it can't be read.</param>
    /// <param name="agents">The states of the workspace's agents.</param>
    public static WorkspaceStatus Of(WorkspaceGitSummary? summary, IEnumerable<AgentState> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        if (summary is null)
        {
            return WorkspaceStatus.RequiresAttention;
        }

        AgentState[] states = [.. agents];
        IReadOnlyList<PullRequestInfo> pullRequests = summary.PullRequests;
        PullRequestInfo[] open = [.. pullRequests.Where(pr => pr.State is PullRequestState.Open or PullRequestState.Draft)];

        if (states.Any(state => state is AgentState.NeedsInput or AgentState.Error)
            || open.Any(pr => pr.Checks == PullRequestChecks.Failed))
        {
            return WorkspaceStatus.RequiresAttention;
        }

        if (open.Any(pr => pr.State == PullRequestState.Open))
        {
            return WorkspaceStatus.InReview;
        }

        if (pullRequests.Count > 0 && pullRequests.All(pr => pr.State == PullRequestState.Merged))
        {
            return WorkspaceStatus.Done;
        }

        return summary.LocalChanges.Count > 0 || summary.Unpushed.Count > 0 || pullRequests.Count > 0
            || states.Contains(AgentState.Working)
            ? WorkspaceStatus.InDevelopment
            : WorkspaceStatus.Planning;
    }

    /// <summary>
    /// Returns <paramref name="order"/> with unknown and repeated statuses removed and missing ones added at the end.
    /// </summary>
    public static IReadOnlyList<WorkspaceStatus> NormalizeOrder(IEnumerable<WorkspaceStatus>? order)
    {
        List<WorkspaceStatus> result = [.. (order ?? []).Where(Enum.IsDefined).Distinct()];
        result.AddRange(DefaultOrder.Where(status => !result.Contains(status)));
        return result;
    }
}
