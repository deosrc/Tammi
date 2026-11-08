using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Wording for Pull Requests in the Workspace View's side panel.
/// </summary>
internal static class PullRequestText
{
    public static string State(PullRequestState state) => state switch
    {
        PullRequestState.Draft => "Draft",
        PullRequestState.Open => "Open",
        PullRequestState.Merged => "Merged",
        _ => "Closed",
    };

    public static string Checks(PullRequestChecks checks) => checks switch
    {
        PullRequestChecks.Succeeded => "Checks passed",
        PullRequestChecks.InProgress => "Checks in progress",
        _ => "Checks failed",
    };

    /// <summary>
    /// The badge's tooltip, e.g. "acme/api#12: Fix login (Open · Checks failed). Open it in the browser".
    /// </summary>
    public static string Title(PullRequestInfo pullRequest)
    {
        string status = pullRequest.Checks is { } checks
            ? $"{State(pullRequest.State)} · {Checks(checks)}"
            : State(pullRequest.State);
        return $"{pullRequest.Repository}#{pullRequest.Number}: {pullRequest.Title} ({status}). Open it in the browser";
    }
}
