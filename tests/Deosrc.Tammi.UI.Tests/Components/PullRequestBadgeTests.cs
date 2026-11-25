using Bunit;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.UI.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class PullRequestBadgeTests : BunitContext
{
    private readonly FakeLauncher _launcher = new();

    public PullRequestBadgeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddSingleton<IProcessLauncher>(_launcher);
    }

    private static PullRequestInfo PullRequest(PullRequestState state, PullRequestChecks? checks = null) =>
        new("github", "acme/api", 42, "Fix it", new Uri("https://example.test/pull/42"), state) { Checks = checks };

    [Fact]
    public void ShowsItsNumber_AndOpensItInTheBrowserWhenClicked()
    {
        var badge = Render<PullRequestBadge>(p => p.Add(b => b.PullRequest, PullRequest(PullRequestState.Open)));

        Assert.Contains("#42", badge.Find("fluent-badge").TextContent);
        badge.Find("button").Click();

        Assert.Equal(["https://example.test/pull/42"], _launcher.Opened);
    }

    [Theory]
    [InlineData(PullRequestState.Open, PullRequestChecks.Succeeded, 1)]
    [InlineData(PullRequestState.Draft, PullRequestChecks.Failed, 1)]
    [InlineData(PullRequestState.Merged, PullRequestChecks.Succeeded, 0)]
    [InlineData(PullRequestState.Closed, PullRequestChecks.Failed, 0)]
    public void ShowsADotForChecksOnlyWhileItCanStillChange(PullRequestState state, PullRequestChecks checks, int dots)
    {
        var badge = Render<PullRequestBadge>(p => p.Add(b => b.PullRequest, PullRequest(state, checks)));

        Assert.Single(badge.FindAll("fluent-badge"));
        Assert.Equal(dots, badge.FindAll(".pull-request-checks").Count);
    }

    [Theory]
    [InlineData(PullRequestState.Open, "side-panel-card-info")]
    [InlineData(PullRequestState.Draft, "side-panel-card-neutral")]
    [InlineData(PullRequestState.Merged, "side-panel-card-success")]
    [InlineData(PullRequestState.Closed, "side-panel-card-danger")]
    public void Card_TakesItsToneFromTheState_AndOpensThePullRequestWhenClicked(PullRequestState state, string toneClass)
    {
        var card = Render<PullRequestCard>(p => p.Add(c => c.PullRequest, PullRequest(state)));

        Assert.Contains(toneClass, card.Find("li").ClassList);
        Assert.Contains("Fix it", card.Find(".pull-request-card-title").TextContent);
        card.Find(".fluent-card").Click();

        Assert.Equal(["https://example.test/pull/42"], _launcher.Opened);
    }

    [Fact]
    public void BadgesShowsNothingWithoutPullRequestsOrAProblem()
    {
        Assert.Empty(Render<PullRequestBadges>().FindAll(".pull-request-badges"));

        var withProblem = Render<PullRequestBadges>(p => p.Add(b => b.Problem, "Couldn't read them"));

        Assert.Single(withProblem.FindAll(".pull-request-problem"));
    }

    [Fact]
    public void SyncChips_ShowOnlyTheNonZeroCounts()
    {
        Assert.Empty(Render<GitSyncChips>().FindAll("fluent-badge"));

        var chips = Render<GitSyncChips>(p => p.Add(c => c.Ahead, 3).Add(c => c.Behind, 0));

        var badge = Assert.Single(chips.FindAll("fluent-badge"));
        Assert.Contains("3", badge.TextContent);
    }

    private sealed class FakeLauncher : IProcessLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string target) => Opened.Add(target);

        public string? FindExecutable(string name) => null;

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int Start(ProcessRequest request) => throw new NotSupportedException();

        public void StartDetached(ProcessRequest request) => throw new NotSupportedException();
    }
}
