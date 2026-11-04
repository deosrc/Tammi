using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Tests.Integrations;

public sealed class TaskFinderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTaskSource _github = new("github", "GH");
    private readonly FakeTaskSource _jira = new("jira", "ABC");
    private readonly TaskFinder _finder;

    public TaskFinderTests()
    {
        _finder = new TaskFinder(new IntegrationRegistry([_github, _jira]));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindAsync_WithNothingEntered_CombinesEverySourcesAssignedTasks(string? text)
    {
        _github.Add(1, "Old", Now.AddDays(-3));
        _jira.Add(1, "Newest", Now);
        _github.Add(2, "Middle", Now.AddDays(-1));

        TaskSearchResults results = await _finder.FindAsync(text, new HashSet<TaskReference>(), CancellationToken);

        Assert.Equal(TaskSearchKind.Assigned, results.Kind);
        Assert.Equal(["Newest", "Middle", "Old"], results.Tasks.Select(task => task.Title));
        Assert.Empty(results.Errors);
        Assert.Equal(["assigned"], _github.Calls);
        Assert.Equal(["assigned"], _jira.Calls);
    }

    [Fact]
    public async Task FindAsync_WithNothingEntered_LeavesOutTasksInWorkspaces()
    {
        TaskInfo inWorkspace = _github.Add(1, "In a workspace");
        _jira.Add(1, "Free");

        TaskSearchResults results = await _finder.FindAsync(
            null, new HashSet<TaskReference> { inWorkspace.Reference }, CancellationToken);

        Assert.Equal(["Free"], results.Tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task FindAsync_WithText_SearchesEverySource_KeepingTasksInWorkspaces()
    {
        TaskInfo inWorkspace = _github.Add(1, "Fix login", Now.AddHours(-1));
        _jira.Add(1, "Login page: fix login", Now);
        _jira.Add(2, "Unrelated");

        TaskSearchResults results = await _finder.FindAsync(
            " login ", new HashSet<TaskReference> { inWorkspace.Reference }, CancellationToken);

        Assert.Equal(TaskSearchKind.Text, results.Kind);
        Assert.Equal(["ABC-1", "GH-1"], results.Tasks.Select(task => task.Key));
        Assert.Equal(["search:login"], _github.Calls);
        Assert.Equal(["search:login"], _jira.Calls);
    }

    [Theory]
    [InlineData("ABC-2")]
    [InlineData("https://jira.example/ABC-2")]
    public async Task FindAsync_WithReference_AsksOnlyTheSourcesThatRecogniseIt(string text)
    {
        _jira.Add(2, "Linked");

        TaskSearchResults results = await _finder.FindAsync(text, new HashSet<TaskReference>(), CancellationToken);

        Assert.Equal(TaskSearchKind.Reference, results.Kind);
        Assert.Equal("Linked", Assert.Single(results.Tasks).Title);
        Assert.Empty(_github.Calls);
        Assert.Equal(["get:ABC-2"], _jira.Calls);
    }

    [Fact]
    public async Task FindAsync_WithReferenceToAMissingTask_FindsNothing()
    {
        TaskSearchResults results = await _finder.FindAsync("ABC-9", new HashSet<TaskReference>(), CancellationToken);

        Assert.Equal(TaskSearchKind.Reference, results.Kind);
        Assert.Empty(results.Tasks);
        Assert.False(results.IsUnrecognisedLink);
    }

    [Fact]
    public async Task FindAsync_WithReferenceThatSeveralSourcesRecognise_AsksThemAll()
    {
        var otherJira = new FakeTaskSource("jira2", "ABC");
        otherJira.Add(1, "On the other site");
        _jira.Add(1, "On this site");
        var finder = new TaskFinder(new IntegrationRegistry([_jira, otherJira]));

        TaskSearchResults results = await finder.FindAsync("ABC-1", new HashSet<TaskReference>(), CancellationToken);

        Assert.Equal(["On this site", "On the other site"], results.Tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task FindAsync_WithLinkNoSourceRecognises_DoesntSearch()
    {
        TaskSearchResults results = await _finder.FindAsync(
            "https://elsewhere.example/issues/1", new HashSet<TaskReference>(), CancellationToken);

        Assert.True(results.IsUnrecognisedLink);
        Assert.Empty(results.Tasks);
        Assert.Empty(_github.Calls);
        Assert.Empty(_jira.Calls);
    }

    [Fact]
    public async Task FindAsync_WhenASourceFails_ReturnsTheOthersResultsAndTheError()
    {
        _github.Failure = new IntegrationAuthenticationException("github", "The token was rejected.");
        _jira.Add(1, "Still found");

        TaskSearchResults results = await _finder.FindAsync(null, new HashSet<TaskReference>(), CancellationToken);

        Assert.Equal("Still found", Assert.Single(results.Tasks).Title);
        TaskSourceError error = Assert.Single(results.Errors);
        Assert.Equal("github", error.Source);
        Assert.Equal("Source github", error.SourceName);
        Assert.True(error.IsAuthentication);
    }

    [Fact]
    public async Task FindAsync_ReportsOtherFailures_AsNotAuthentication()
    {
        _jira.Failure = new IntegrationException("JIRA couldn't be reached.");

        TaskSearchResults results = await _finder.FindAsync("x", new HashSet<TaskReference>(), CancellationToken);

        Assert.False(Assert.Single(results.Errors).IsAuthentication);
    }

    [Fact]
    public async Task FindAsync_WithoutSources_FindsNothing()
    {
        var finder = new TaskFinder(new IntegrationRegistry([]));

        TaskSearchResults results = await finder.FindAsync(null, new HashSet<TaskReference>(), CancellationToken);

        Assert.Empty(results.Tasks);
        Assert.Empty(results.Errors);
    }

    [Fact]
    public async Task FindAsync_KeepsEachSourcesOrder_ForTasksWithoutUpdateTimes()
    {
        _github.Add(2, "Second");
        _github.Add(1, "First");

        TaskSearchResults results = await _finder.FindAsync(null, new HashSet<TaskReference>(), CancellationToken);

        Assert.Equal(["Second", "First"], results.Tasks.Select(task => task.Title));
    }
}
