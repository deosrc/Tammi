using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceStatusesTests
{
    private static readonly WorkspaceRepository Repository = new("api", "/repos/api", "feature", "origin/main");

    private static WorkspaceGitSummary Summary(params PullRequestInfo[] pullRequests) =>
        new([new HomeRepositoryStatus(
            new WorkspaceRepositoryStatus(Repository, "/ws/api"),
            new WorkspaceRepositoryPullRequests(Repository) { IsLoaded = true, PullRequests = pullRequests })]);

    private static PullRequestInfo Pr(PullRequestState state, PullRequestChecks? checks = null) =>
        new("github", "acme/api", 1, "Title", new Uri("https://example.com/"), state) { Checks = checks };

    [Fact]
    public void Unreadable_requires_attention() =>
        Assert.Equal(WorkspaceStatus.RequiresAttention, WorkspaceStatuses.Of(null, []));

    [Fact]
    public void No_changes_is_planning() =>
        Assert.Equal(WorkspaceStatus.Planning, WorkspaceStatuses.Of(Summary(), []));

    [Fact]
    public void Merged_is_done() =>
        Assert.Equal(WorkspaceStatus.Done, WorkspaceStatuses.Of(Summary(Pr(PullRequestState.Merged)), []));

    [Theory]
    [InlineData(null)]
    [InlineData(PullRequestChecks.Succeeded)]
    [InlineData(PullRequestChecks.InProgress)]
    public void Open_without_failures_is_in_review(PullRequestChecks? checks) =>
        Assert.Equal(WorkspaceStatus.InReview, WorkspaceStatuses.Of(Summary(Pr(PullRequestState.Open, checks)), []));

    [Fact]
    public void Failed_checks_require_attention() =>
        Assert.Equal(
            WorkspaceStatus.RequiresAttention,
            WorkspaceStatuses.Of(Summary(Pr(PullRequestState.Open, PullRequestChecks.Failed)), []));

    [Fact]
    public void Agent_needing_input_requires_attention() =>
        Assert.Equal(WorkspaceStatus.RequiresAttention, WorkspaceStatuses.Of(Summary(), [AgentState.NeedsInput]));

    [Fact]
    public void Working_agent_is_in_development() =>
        Assert.Equal(WorkspaceStatus.InDevelopment, WorkspaceStatuses.Of(Summary(), [AgentState.Working]));

    [Fact]
    public void Order_is_completed_and_deduplicated()
    {
        var order = WorkspaceStatuses.NormalizeOrder([WorkspaceStatus.Done, WorkspaceStatus.Done]);
        Assert.Equal(5, order.Count);
        Assert.Equal(WorkspaceStatus.Done, order[0]);
    }

    [Fact]
    public void Source_is_repository_or_project()
    {
        Assert.Equal("acme/api", WorkspaceSources.Of([new("github", "acme/api#42")], _ => "GitHub"));
        Assert.Equal("JIRA: ABC", WorkspaceSources.Of([new("jira", "ABC-123")], _ => "JIRA"));
        Assert.Equal(WorkspaceSources.NoTasks, WorkspaceSources.Of([], _ => null));
    }
}
