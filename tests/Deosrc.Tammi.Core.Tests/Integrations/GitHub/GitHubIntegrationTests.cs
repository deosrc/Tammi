using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.GitHub;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.GitHub;

public class GitHubIntegrationTests : IDisposable
{
    private const string Token = "github_pat_test";

    private static readonly string DefaultSearchPath = SearchPath(
        "is:issue is:open assignee:@me archived:false", sort: "&sort=updated&order=desc");

    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeGitHubApi _api = new();
    private readonly HttpClient _httpClient;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));

    public GitHubIntegrationTests()
    {
        _httpClient = new HttpClient(_api);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Requests_AreAuthenticatedWithTheStoredToken()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeGitHubApi.Recorded("user.json"));

        GitHubUser user = await github.GetAuthenticatedUserAsync(CancellationToken);

        Assert.Equal("deosrc", user.Login);
        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"Bearer {Token}", request.Authorization);
        Assert.Equal("application/vnd.github+json", request.Headers["Accept"]);
        Assert.Equal("2022-11-28", request.Headers["X-GitHub-Api-Version"]);
        Assert.StartsWith("Tammi/", request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task Credentials_AreStoredUnderTheIntegrationId()
    {
        GitHubIntegration github = Create(new GitHubIntegrationOptions { Id = "github-work" });

        await github.Credentials.SaveAsync(Token, CancellationToken);

        Assert.Equal("github-work", github.Id);
        Assert.Equal(Token, _store.Secrets["integration:github-work"]);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WithoutSendingARequest_WhenNotAuthenticated()
    {
        GitHubIntegration github = Create();

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => github.GetAssignedTasksAsync(CancellationToken));

        Assert.Equal("github", exception.IntegrationId);
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WhenTheTokenIsRejected()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/repos?sort=full_name&per_page=100",
            """{"message":"Bad credentials","documentation_url":"https://docs.github.com/rest","status":"401"}""",
            HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => github.GetRepositoriesAsync(CancellationToken));

        Assert.Equal("github", exception.IntegrationId);
        Assert.Contains("Settings", exception.Message);
    }

    [Fact]
    public async Task GetAssignedTasksAsync_ListsTheUsersOpenIssues()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(DefaultSearchPath, FakeGitHubApi.Recorded("search.json"));

        IReadOnlyList<TaskInfo> tasks = await github.GetAssignedTasksAsync(CancellationToken);

        Assert.Equal(["deosrc/Condement#1", "deosrc/Condement#2", "deosrc/Condement#3"], tasks.Select(t => t.Key));
        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal("is:issue is:open assignee:@me archived:false", request.Query("q"));
        Assert.Equal("updated", request.Query("sort"));
    }

    [Fact]
    public async Task GetAssignedTasksAsync_ReadsIssuesAsTasks()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(DefaultSearchPath, FakeGitHubApi.Recorded("search.json"));

        IReadOnlyList<TaskInfo> tasks = await github.GetAssignedTasksAsync(CancellationToken);

        TaskInfo task = tasks[2];
        Assert.Equal(new TaskReference("github", "deosrc/Condement#3"), task.Reference);
        Assert.Equal("Add Remote Desktop support", task.Title);
        Assert.Equal(new Uri("https://github.com/deosrc/Condement/issues/3"), task.Url);
        Assert.Equal(new Uri("https://github.com/deosrc/Condement"), task.RepositoryUrl);
        Assert.StartsWith("Using remote desktop should avoid", task.Description);
        Assert.NotNull(task.Updated);
        Assert.Null(tasks[0].Description);
    }

    [Fact]
    public async Task GetAssignedTasksAsync_LeavesOutPullRequests()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        var results = new JsonObject
        {
            ["total_count"] = 2,
            ["incomplete_results"] = false,
            ["items"] = new JsonArray(
                JsonNode.Parse(FakeGitHubApi.Recorded("pull-request.json")),
                JsonNode.Parse(FakeGitHubApi.Recorded("issue.json"))),
        };
        _api.Respond(DefaultSearchPath, results.ToJsonString());

        IReadOnlyList<TaskInfo> tasks = await github.GetAssignedTasksAsync(CancellationToken);

        Assert.Equal(["deosrc/Condement#3"], tasks.Select(task => task.Key));
    }

    [Fact]
    public async Task GetAssignedTasksAsync_FollowsPages_UpToTheMaximumResults()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync(
            new GitHubIntegrationOptions { PageSize = 3, MaxSearchResults = 5 });
        string first = SearchPath("is:issue " + GitHubIntegration.AssignedSearch, perPage: 3,
            sort: "&sort=updated&order=desc");
        string second = first + "&page=2";
        _api.Respond(first, FakeGitHubApi.Recorded("search.json"), HttpStatusCode.OK,
                ("Link", $"<{FakeGitHubApi.Url(second)}>; rel=\"next\", <{FakeGitHubApi.Url(first)}&page=7>; rel=last"))
            .Respond(second, FakeGitHubApi.Recorded("search-page2.json"), HttpStatusCode.OK,
                ("Link", $"<{FakeGitHubApi.Url(first)}&page=3>; rel=\"next\""));

        IReadOnlyList<TaskInfo> tasks = await github.GetAssignedTasksAsync(CancellationToken);

        Assert.Equal(
            ["deosrc/Condement#1", "deosrc/Condement#2", "deosrc/Condement#3", "deosrc/Condement#4",
                "deosrc/Condement#5"],
            tasks.Select(task => task.Key));
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task GetAssignedTasksAsync_AsksForOnlyTheResultsItNeeds()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync(
            new GitHubIntegrationOptions { MaxSearchResults = 20 });
        _api.Respond(SearchPath("is:issue " + GitHubIntegration.AssignedSearch, perPage: 20,
            sort: "&sort=updated&order=desc"), FakeGitHubApi.Recorded("search.json"));

        await github.GetAssignedTasksAsync(CancellationToken);

        Assert.Equal("20", Assert.Single(_api.Requests).Query("per_page"));
    }

    [Fact]
    public async Task SearchTasksAsync_SearchesOnlyTheUsersOrganisationsAndCollaborations()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithScope(
            ("deosrc", "User", "ansible"),
            ("acme", "Organization", "api"),
            ("acme", "Organization", "web"),
            ("friend", "User", "tool"));
        const string search = "is:issue archived:false user:deosrc org:acme repo:friend/tool login bug";
        _api.Respond(SearchPath(search), FakeGitHubApi.Recorded("search.json"));

        IReadOnlyList<TaskInfo> tasks = await github.SearchTasksAsync("  login bug ", CancellationToken);

        Assert.Equal(3, tasks.Count);
        RecordedRequest request = _api.Requests[^1];
        Assert.Equal(search, request.Query("q"));
        Assert.Null(request.Query("sort"));
    }

    [Fact]
    public async Task SearchTasksAsync_RemembersTheScope_ForItsLifetime()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithScope(("acme", "Organization", "api"));
        _api.Respond(SearchPath("is:issue archived:false user:deosrc org:acme bug"), FakeGitHubApi.Recorded("search.json"));

        await github.SearchTasksAsync("bug", CancellationToken);
        await github.SearchTasksAsync("bug", CancellationToken);
        Assert.Equal(4, _api.Requests.Count);

        _time.Advance(new GitHubIntegrationOptions().SearchScopeLifetime);
        await github.SearchTasksAsync("bug", CancellationToken);

        Assert.Equal(7, _api.Requests.Count);
    }

    [Fact]
    public async Task SearchTasksAsync_SplitsALargeScope_IntoSeveralRequests()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync(new GitHubIntegrationOptions { MaxScopeQualifiers = 2 });
        RespondWithScope(("acme", "Organization", "api"), ("other", "Organization", "web"));
        _api.Respond(SearchPath("is:issue archived:false user:deosrc org:acme bug"), FakeGitHubApi.Recorded("search.json"))
            .Respond(SearchPath("is:issue archived:false org:other bug"), FakeGitHubApi.Recorded("search-page2.json"));

        IReadOnlyList<TaskInfo> tasks = await github.SearchTasksAsync("bug", CancellationToken);

        Assert.Equal(
            ["deosrc/Condement#1", "deosrc/Condement#2", "deosrc/Condement#3", "deosrc/Condement#4",
                "deosrc/Condement#5", "deosrc/Condement#6"],
            tasks.Select(task => task.Key));
    }

    [Fact]
    public async Task SearchTasksAsync_Throws_ForBlankText()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => github.SearchTasksAsync(" ", CancellationToken));
    }

    [Fact]
    public async Task SearchTasksAsync_ThrowsWithGitHubsMessage_WhenTheSearchFails()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithScope();
        _api.Respond(SearchPath("is:issue archived:false user:deosrc x"), FakeGitHubApi.Recorded("invalid-query.json"),
            HttpStatusCode.UnprocessableEntity);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.SearchTasksAsync("x", CancellationToken));

        Assert.Equal(
            "GitHub returned an error (422 Unprocessable Entity): Validation Failed. The listed users and repositories "
            + "cannot be searched either because the resources do not exist or you do not have permission to view "
            + "them.",
            exception.Message);
    }

    [Theory]
    [InlineData("deosrc/Condement#3")]
    [InlineData(" https://github.com/deosrc/Condement/issues/3#issuecomment-1 ")]
    public void TryParseTaskReference_ReadsAKeyOrUrl(string text)
    {
        GitHubIntegration github = Create();

        Assert.True(github.TryParseTaskReference(text, out string? key));
        Assert.Equal("deosrc/Condement#3", key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("login bug")]
    [InlineData("ABC-123")]
    [InlineData("https://github.example.com/deosrc/Condement/issues/3")]
    [InlineData("https://github.com/deosrc/Condement/pull/3")]
    public void TryParseTaskReference_RejectsOtherText(string? text)
    {
        Assert.False(Create().TryParseTaskReference(text, out _));
    }

    [Fact]
    public async Task CheckTokenAccessAsync_DoesntCheckClassicTokens()
    {
        GitHubIntegration github = Create();
        await github.Credentials.SaveAsync("ghp_classic", CancellationToken);

        Assert.Null(await github.CheckTokenAccessAsync(CancellationToken));
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task CheckTokenAccessAsync_IsQuiet_WhenTheTokenCanReadAPrivateRepositorysContents()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithPrivateRepositories(("deosrc", "secret", true));
        _api.Respond(CommitsPath("deosrc", "secret"), "[]");

        Assert.Null(await github.CheckTokenAccessAsync(CancellationToken));
        Assert.Equal("6", _api.Requests[0].Query("per_page"));
        Assert.Equal("private", _api.Requests[0].Query("visibility"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CheckTokenAccessAsync_Warns_WhenTheTokenCantReadContents(HttpStatusCode status)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithPrivateRepositories(("deosrc", "secret", true));
        _api.Respond(CommitsPath("deosrc", "secret"),
            """{"message":"Resource not accessible by personal access token","status":"403"}""", status);

        string? warning = await github.CheckTokenAccessAsync(CancellationToken);

        Assert.NotNull(warning);
        Assert.Contains("deosrc/secret", warning);
        Assert.Contains("Contents", warning);
    }

    [Fact]
    public async Task CheckTokenAccessAsync_LooksPastEmptyRepositories()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithPrivateRepositories(("deosrc", "empty", true), ("deosrc", "secret", true));
        _api.Respond(CommitsPath("deosrc", "empty"), """{"message":"Git Repository is empty."}""",
                HttpStatusCode.Conflict)
            .Respond(CommitsPath("deosrc", "secret"), "{}", HttpStatusCode.Forbidden);

        Assert.NotNull(await github.CheckTokenAccessAsync(CancellationToken));
    }

    [Fact]
    public async Task CheckTokenAccessAsync_SkipsPublicRepositories_AndIsQuietWithoutPrivateOnes()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithPrivateRepositories(("deosrc", "open", false));

        Assert.Null(await github.CheckTokenAccessAsync(CancellationToken));
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task CheckTokenAccessAsync_ThrowsAuthenticationException_WhenTheTokenIsRejected()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        RespondWithPrivateRepositories(("deosrc", "secret", true));
        _api.Respond(CommitsPath("deosrc", "secret"), """{"message":"Bad credentials"}""", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => github.CheckTokenAccessAsync(CancellationToken));
    }

    [Fact]
    public async Task GetTaskAsync_ReadsTheIssue()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("repos/deosrc/Condement/issues/3", FakeGitHubApi.Recorded("issue.json"));

        TaskInfo? task = await github.GetTaskAsync("deosrc/Condement#3", CancellationToken);

        Assert.NotNull(task);
        Assert.Equal("Add Remote Desktop support", task.Title);
        Assert.Equal("github", task.Source);
        Assert.Equal("Closed", task.Status);
        Assert.Equal(TaskStatusCategory.Done, task.StatusCategory);
    }

    [Theory]
    [InlineData("open", null, "Open", TaskStatusCategory.ToDo)]
    [InlineData("open", "reopened", "Open", TaskStatusCategory.ToDo)]
    [InlineData("closed", "completed", "Closed", TaskStatusCategory.Done)]
    [InlineData("closed", null, "Closed", TaskStatusCategory.Done)]
    [InlineData("closed", "not_planned", "Not planned", TaskStatusCategory.Done)]
    [InlineData("closed", "duplicate", "Duplicate", TaskStatusCategory.Done)]
    public async Task GetTaskAsync_ReadsTheStatus(
        string state, string? reason, string status, TaskStatusCategory category)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        var issue = JsonNode.Parse(FakeGitHubApi.Recorded("issue.json"))!;
        issue["state"] = state;
        issue["state_reason"] = reason;
        _api.Respond("repos/deosrc/Condement/issues/3", issue.ToJsonString());

        TaskInfo? task = await github.GetTaskAsync("deosrc/Condement#3", CancellationToken);

        Assert.Equal(status, task?.Status);
        Assert.Equal(category, task?.StatusCategory);
    }

    [Fact]
    public async Task GetTaskAsync_HasNoStatus_WhenGitHubDoesntSay()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        var issue = JsonNode.Parse(FakeGitHubApi.Recorded("issue.json"))!.AsObject();
        issue.Remove("state");
        _api.Respond("repos/deosrc/Condement/issues/3", issue.ToJsonString());

        TaskInfo? task = await github.GetTaskAsync("deosrc/Condement#3", CancellationToken);

        Assert.Null(task?.Status);
        Assert.Null(task?.StatusCategory);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsNull_ForAPullRequest()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("repos/deosrc/Condement/issues/15", FakeGitHubApi.Recorded("pull-request.json"));

        Assert.Null(await github.GetTaskAsync("deosrc/Condement#15", CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task GetTaskAsync_ReturnsNull_WhenTheIssueIsMissing(HttpStatusCode status)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("repos/deosrc/Condement/issues/9999", FakeGitHubApi.Recorded("not-found.json"), status);

        Assert.Null(await github.GetTaskAsync("deosrc/Condement#9999", CancellationToken));
    }

    [Fact]
    public async Task GetTaskAsync_Throws_ForAKeyThatIsntAGitHubIssue()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => github.GetTaskAsync("ABC-123", CancellationToken));
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task GetRepositoriesAsync_ReadsEveryPage()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        const string first = "user/repos?sort=full_name&per_page=100";
        const string second = first + "&page=2";
        _api.Respond(first, FakeGitHubApi.Recorded("repos.json"), HttpStatusCode.OK,
                ("Link", $"<{FakeGitHubApi.Url(second)}>; rel=\"next\", <{FakeGitHubApi.Url(second)}>; rel=\"last\""))
            .Respond(second, FakeGitHubApi.Recorded("repos-page2.json"), HttpStatusCode.OK,
                ("Link", $"<{FakeGitHubApi.Url(first)}>; rel=\"prev\", <{FakeGitHubApi.Url(first)}>; rel=\"first\""));

        IReadOnlyList<IntegrationRepository> repositories = await github.GetRepositoriesAsync(CancellationToken);

        Assert.Equal(
            ["deosrc/ansible", "deosrc/ansible-community.healthchecksio", "deosrc/ansible-role-1password",
                "deosrc/ansible-role-syncthing"],
            repositories.Select(repository => repository.FullName));
        IntegrationRepository ansible = repositories[0];
        Assert.Equal("github", ansible.Source);
        Assert.Equal(["https://github.com/deosrc/ansible.git", "git@github.com:deosrc/ansible.git"],
            ansible.CloneUrls);
        Assert.Equal(new Uri("https://github.com/deosrc/ansible"), ansible.Url);
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task Paging_Throws_WhenTheNextPageIsOnAnotherHost()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/repos?sort=full_name&per_page=100", FakeGitHubApi.Recorded("repos.json"),
            HttpStatusCode.OK, ("Link", "<https://evil.example/user/repos?page=2>; rel=\"next\""));

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.GetRepositoriesAsync(CancellationToken));

        Assert.Contains("evil.example", exception.Message);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task RateLimit_IsWaitedOut_WhenItResetsSoon()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user", RateLimitBody, HttpStatusCode.Forbidden,
                ("x-ratelimit-remaining", "0"),
                ("x-ratelimit-reset", _time.GetUtcNow().AddSeconds(5).ToUnixTimeSeconds().ToString()))
            .Respond("user", FakeGitHubApi.Recorded("user.json"));

        Task<GitHubUser> call = github.GetAuthenticatedUserAsync(CancellationToken);
        await AdvanceUntilAsync(() => call.IsCompleted || _api.Requests.Count > 1, TimeSpan.FromSeconds(1));

        Assert.Equal("deosrc", (await call).Login);
        Assert.Equal(2, _api.Requests.Count);
        Assert.True(_time.GetUtcNow() >= new DateTimeOffset(2026, 11, 3, 12, 0, 5, TimeSpan.Zero));
    }

    [Fact]
    public async Task RateLimit_Throws_WhenItResetsLaterThanTheMaximumWait()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        DateTimeOffset reset = _time.GetUtcNow().AddMinutes(40);
        _api.Respond(DefaultSearchPath, RateLimitBody, HttpStatusCode.Forbidden,
            ("x-ratelimit-remaining", "0"), ("x-ratelimit-reset", reset.ToUnixTimeSeconds().ToString()));

        var exception = await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => github.GetAssignedTasksAsync(CancellationToken));

        Assert.Equal(reset, exception.RetryAt);
        Assert.Equal("github", exception.IntegrationId);
        Assert.Single(_api.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "120", 120)]
    [InlineData(HttpStatusCode.TooManyRequests, "30", 30)]
    [InlineData(HttpStatusCode.TooManyRequests, null, 60)]
    [InlineData(HttpStatusCode.Forbidden, null, 60)]
    public async Task SecondaryRateLimit_UsesRetryAfter_OrWaitsAMinute(
        HttpStatusCode status,
        string? retryAfter,
        int expectedSeconds)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user", SecondaryRateLimitBody, status,
            retryAfter is null ? [] : [("Retry-After", retryAfter)]);

        var exception = await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => github.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal(_time.GetUtcNow().AddSeconds(expectedSeconds), exception.RetryAt);
    }

    [Fact]
    public async Task RateLimit_GivesUp_AfterRetryingAFewTimes()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user", SecondaryRateLimitBody, HttpStatusCode.TooManyRequests, ("Retry-After", "0"));

        await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => github.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal(4, _api.Requests.Count);
    }

    [Fact]
    public async Task Forbidden_ThatIsntARateLimit_ThrowsWithGitHubsMessage()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("repos/acme/api/issues/1",
            """{"message":"Resource protected by organization SAML enforcement.","status":"403"}""",
            HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.GetTaskAsync("acme/api#1", CancellationToken));

        Assert.IsNotType<IntegrationRateLimitException>(exception);
        Assert.Contains("SAML enforcement", exception.Message);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task NetworkFailure_ThrowsIntegrationException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Fail("user");

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.GetAuthenticatedUserAsync(CancellationToken));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task UnreadableResponse_ThrowsIntegrationException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user", "<html>Unicorn!</html>");

        await Assert.ThrowsAsync<IntegrationException>(() => github.GetAuthenticatedUserAsync(CancellationToken));
    }

    [Fact]
    public async Task EnterpriseServer_UsesItsApiUrl_AndItsHostForIssueUrls()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var github = new GitHubIntegration(
            new GitHubIntegrationOptions { ApiUrl = new Uri("https://github.example.com/api/v3") },
            _store, httpClient, _time);
        await github.Credentials.SaveAsync(Token, CancellationToken);

        Assert.True(github.TryParseTaskReference("https://github.example.com/acme/api/issues/7", out string? key));
        await github.GetTaskAsync(key, CancellationToken);

        Assert.Equal(new Uri("https://github.example.com/api/v3/repos/acme/api/issues/7"), handler.LastUrl);
    }

    [Fact]
    public void Constructor_Throws_ForAPageSizeGitHubDoesntAllow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(new GitHubIntegrationOptions { PageSize = 101 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(new GitHubIntegrationOptions { PageSize = 0 }));
    }

    private const string RateLimitBody =
        """{"message":"API rate limit exceeded for user ID 1.","documentation_url":"https://docs.github.com/rest"}""";

    private const string SecondaryRateLimitBody =
        """{"message":"You have exceeded a secondary rate limit. Please wait a few minutes before you try again."}""";

    /// <summary>
    /// Answers the requests that read a text search's scope: the user (<c>deosrc</c>) and their repositories.
    /// </summary>
    private void RespondWithScope(params (string Owner, string Type, string Name)[] repositories)
    {
        _api.Respond("user", FakeGitHubApi.Recorded("user.json"));
        var page = new JsonArray([.. repositories.Select(repository => (JsonNode)new JsonObject
        {
            ["name"] = repository.Name,
            ["owner"] = new JsonObject { ["login"] = repository.Owner, ["type"] = repository.Type },
            ["html_url"] = $"https://github.com/{repository.Owner}/{repository.Name}",
            ["clone_url"] = $"https://github.com/{repository.Owner}/{repository.Name}.git",
            ["ssh_url"] = $"git@github.com:{repository.Owner}/{repository.Name}.git",
        })]);
        _api.Respond("user/repos?per_page=100", page.ToJsonString());
    }

    private void RespondWithPrivateRepositories(params (string Owner, string Name, bool Private)[] repositories)
    {
        var page = new JsonArray([.. repositories.Select(repository => (JsonNode)new JsonObject
        {
            ["name"] = repository.Name,
            ["owner"] = new JsonObject { ["login"] = repository.Owner, ["type"] = "User" },
            ["html_url"] = $"https://github.com/{repository.Owner}/{repository.Name}",
            ["clone_url"] = $"https://github.com/{repository.Owner}/{repository.Name}.git",
            ["ssh_url"] = $"git@github.com:{repository.Owner}/{repository.Name}.git",
            ["private"] = repository.Private,
        })]);
        _api.Respond("user/repos?visibility=private&per_page=6", page.ToJsonString());
    }

    private static string CommitsPath(string owner, string repository) =>
        $"repos/{owner}/{repository}/commits?per_page=1";

    private static string SearchPath(string query, int perPage = 100, string sort = "") =>
        $"search/issues?q={Uri.EscapeDataString(query)}{sort}&per_page={perPage}";

    private GitHubIntegration Create(GitHubIntegrationOptions? options = null) =>
        new(options ?? new GitHubIntegrationOptions(), _store, _httpClient, _time);

    private async Task<GitHubIntegration> CreateAuthenticatedAsync(GitHubIntegrationOptions? options = null)
    {
        GitHubIntegration github = Create(options);
        await github.Credentials.SaveAsync(Token, CancellationToken);
        return github;
    }

    /// <summary>
    /// Moves the fake clock on until <paramref name="done"/>, giving the call under test time to run in between.
    /// </summary>
    private async Task AdvanceUntilAsync(Func<bool> done, TimeSpan step)
    {
        for (int i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(10, CancellationToken);
            _time.Advance(step);
        }
    }

    /// <summary>
    /// Records the URL of each request and answers with a 404.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
