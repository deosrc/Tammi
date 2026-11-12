using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.Bitbucket;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.Bitbucket;

public class BitbucketIntegrationTests : IDisposable
{
    private const string Token = "ATATT3xFfGF0test";
    private const string Email = "mia@example.com";
    private const string AccountId = "557058:7f1a0c1e-0000-4000-8000-000000000001";
    private const string ReposPath = "user/permissions/repositories?pagelen=100";
    private const string ReposPage2Url = "https://api.bitbucket.org/2.0/user/permissions/repositories?pagelen=100&page=2";
    private const string ToDo = "(state = \"new\" OR state = \"open\" OR state = \"on hold\")";

    private static readonly string AssignedFilter = $"assignee.account_id = \"{AccountId}\" AND {ToDo}";

    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeBitbucketApi _api = new();
    private readonly HttpClient _httpClient;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));

    public BitbucketIntegrationTests()
    {
        _httpClient = new HttpClient(_api);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The first page of repositories, without the link to the second: acme/api and acme/web.
    /// </summary>
    private static string SingleReposPage() =>
        FakeBitbucketApi.Response("repos.json").Replace("\"next\": \"" + ReposPage2Url + "\",", "");

    private static string IssuesPath(string repository, string filter) =>
        $"repositories/{repository}/issues?q={Uri.EscapeDataString(filter)}&sort=-updated_on&pagelen=100";

    [Fact]
    public async Task Authenticates_WithTheEmailAndApiToken()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"));

        string name = await bitbucket.GetAuthenticatedUserAsync(CancellationToken);

        Assert.Equal("Mia Krystof", name);
        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://api.bitbucket.org/2.0/user"), request.Url);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Token}"))}",
            request.Authorization);
        Assert.Equal("application/json", request.Headers["Accept"]);
        Assert.StartsWith("Tammi/", request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task Credentials_AreStoredUnderTheIntegrationId()
    {
        BitbucketIntegration bitbucket = Create();

        await bitbucket.Credentials.SaveAsync(Token, CancellationToken);

        Assert.Equal(("bitbucket", "Bitbucket"), (bitbucket.Id, bitbucket.Name));
        Assert.Equal(Token, _store.Secrets["integration:bitbucket"]);
    }

    [Fact]
    public void Constructor_WithoutAnEmailAddress_Throws()
    {
        Assert.Throws<ArgumentException>(() => Create(new BitbucketIntegrationOptions { Email = " " }));
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WithoutSendingARequest_WhenNotAuthenticated()
    {
        BitbucketIntegration bitbucket = Create();

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => bitbucket.GetRepositoriesAsync(CancellationToken));

        Assert.Equal("bitbucket", exception.IntegrationId);
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WhenTheCredentialsAreRejected()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", """{"type":"error","error":{"message":"Invalid credentials"}}""",
            HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => bitbucket.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Contains("email address and API token", exception.Message);
        Assert.Contains("Settings", exception.Message);
    }

    [Fact]
    public async Task Calls_ThrowPermissionException_WhenTheTokenLacksAScope()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(ReposPath, FakeBitbucketApi.Response("forbidden.json"), HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => bitbucket.GetRepositoriesAsync(CancellationToken));

        Assert.Contains("lack one or more required privilege scopes", exception.Message);
        Assert.Contains("read:repository:bitbucket", exception.Message);
    }

    [Fact]
    public async Task GetRepositories_ListsEveryPage_WithCloneUrlsForHttpsAndSsh()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(ReposPath, FakeBitbucketApi.Response("repos.json"))
            .Respond(ReposPage2Url, FakeBitbucketApi.Response("repos-page2.json"));

        IReadOnlyList<IntegrationRepository> repositories = await bitbucket.GetRepositoriesAsync(CancellationToken);

        Assert.Equal(["acme/api", "acme/web", "mia/notes"], repositories.Select(repository => repository.FullName));
        IntegrationRepository api = repositories[0];
        Assert.Equal(("bitbucket", "acme", "api"), (api.Source, api.Owner, api.Name));
        // The user name in Bitbucket's HTTPS URL is left out.
        Assert.Equal(["https://bitbucket.org/acme/api.git", "git@bitbucket.org:acme/api.git"], api.CloneUrls);
        Assert.Equal(new Uri("https://bitbucket.org/acme/api"), api.Url);
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task GetRepositories_DoesNotFollowAPagingLinkToAnotherHost()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        string page = FakeBitbucketApi.Response("repos.json").Replace(
            ReposPage2Url, "https://evil.example.com/steal", StringComparison.Ordinal);
        _api.Respond(ReposPath, page);

        IReadOnlyList<IntegrationRepository> repositories = await bitbucket.GetRepositoriesAsync(CancellationToken);

        Assert.Equal(2, repositories.Count);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task GetAssignedTasks_ReadsTheIssueTrackersOfRepositoriesThatHaveOne()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(ReposPath, FakeBitbucketApi.Response("repos.json"))
            .Respond(ReposPage2Url, FakeBitbucketApi.Response("repos-page2.json"))
            .Respond(IssuesPath("acme/api", AssignedFilter), FakeBitbucketApi.Response("issues-api.json"))
            .Respond(IssuesPath("mia/notes", AssignedFilter), FakeBitbucketApi.Response("issues-notes.json"));

        IReadOnlyList<TaskInfo> tasks = await bitbucket.GetAssignedTasksAsync(CancellationToken);

        // Most recently updated first, across repositories.
        Assert.Equal(["mia/notes#3", "acme/api#42", "acme/api#7"], tasks.Select(task => task.Key));
        TaskInfo login = tasks[1];
        Assert.Equal("Login fails", login.Title);
        Assert.Equal(new Uri("https://bitbucket.org/acme/api/issues/42"), login.Url);
        Assert.Equal("Logging in **fails** with a 500.", login.Description);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), login.Updated);
        Assert.Equal(new Uri("https://bitbucket.org/acme/api"), login.RepositoryUrl);
        Assert.Equal(("Open", TaskStatusCategory.ToDo), (login.Status, login.StatusCategory));
        Assert.Null(tasks[2].Description);
        Assert.Equal("On hold", tasks[2].Status);
        Assert.All(tasks, task => Assert.Equal("bitbucket", task.Source));

        // acme/web has the issue tracker turned off, so it isn't asked.
        Assert.DoesNotContain(_api.Requests, request => request.Url.AbsolutePath.Contains("/acme/web/"));
    }

    [Fact]
    public async Task SearchTasks_SearchesTitlesAndDescriptions_OfIssuesThatAreStillToDo()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        string filter = $"(title ~ \"say \\\"hi\\\"\" OR content.raw ~ \"say \\\"hi\\\"\") AND {ToDo}";
        _api.Respond(ReposPath, SingleReposPage())
            .Respond(IssuesPath("acme/api", filter), FakeBitbucketApi.Response("issues-api.json"));

        IReadOnlyList<TaskInfo> tasks = await bitbucket.SearchTasksAsync(" say \"hi\" ", CancellationToken);

        Assert.Equal(["acme/api#42", "acme/api#7"], tasks.Select(task => task.Key));
    }

    [Fact]
    public async Task SearchTasks_WithBlankText_Throws()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => bitbucket.SearchTasksAsync(" ", CancellationToken));
    }

    [Fact]
    public async Task SearchTasks_ReturnsAtMostTheMaximum_MostRecentlyUpdatedFirst()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync(
            new BitbucketIntegrationOptions { Email = Email, MaxSearchResults = 2 });
        string filter = $"(title ~ \"x\" OR content.raw ~ \"x\") AND {ToDo}";
        string reposPage = FakeBitbucketApi.Response("repos.json");
        _api.Respond(ReposPath, reposPage)
            .Respond(ReposPage2Url, FakeBitbucketApi.Response("repos-page2.json"))
            .Respond($"repositories/acme/api/issues?q={Uri.EscapeDataString(filter)}&sort=-updated_on&pagelen=2",
                FakeBitbucketApi.Response("issues-api.json"))
            .Respond($"repositories/mia/notes/issues?q={Uri.EscapeDataString(filter)}&sort=-updated_on&pagelen=2",
                FakeBitbucketApi.Response("issues-notes.json"));

        IReadOnlyList<TaskInfo> tasks = await bitbucket.SearchTasksAsync("x", CancellationToken);

        Assert.Equal(["mia/notes#3", "acme/api#42"], tasks.Select(task => task.Key));
    }

    [Fact]
    public async Task SearchTasks_RemembersTheRepositoriesBetweenSearches_UntilTheyExpire()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        string filter = $"(title ~ \"x\" OR content.raw ~ \"x\") AND {ToDo}";
        _api.Respond(ReposPath, SingleReposPage())
            .Respond(IssuesPath("acme/api", filter), FakeBitbucketApi.Response("issues-api.json"));

        await bitbucket.SearchTasksAsync("x", CancellationToken);
        await bitbucket.SearchTasksAsync("x", CancellationToken);
        int before = _api.Requests.Count(request => request.Url.AbsolutePath.EndsWith("/repositories"));
        _time.Advance(TimeSpan.FromMinutes(11));
        await bitbucket.SearchTasksAsync("x", CancellationToken);

        Assert.Equal(1, before);
        Assert.Equal(2, _api.Requests.Count(request => request.Url.AbsolutePath.EndsWith("/repositories")));
    }

    [Fact]
    public async Task SearchTasks_SkipsARepositoryWhoseIssueTrackerIsGone()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        string filter = $"(title ~ \"x\" OR content.raw ~ \"x\") AND {ToDo}";
        _api.Respond(ReposPath, FakeBitbucketApi.Response("repos.json"))
            .Respond(ReposPage2Url, FakeBitbucketApi.Response("repos-page2.json"))
            .Respond(IssuesPath("acme/api", filter), FakeBitbucketApi.Response("not-found.json"),
                HttpStatusCode.NotFound)
            .Respond(IssuesPath("mia/notes", filter), FakeBitbucketApi.Response("issues-notes.json"));

        IReadOnlyList<TaskInfo> tasks = await bitbucket.SearchTasksAsync("x", CancellationToken);

        Assert.Equal(["mia/notes#3"], tasks.Select(task => task.Key));
    }

    [Theory]
    [InlineData("acme/api#42", "acme/api#42")]
    [InlineData(" acme/api#42 ", "acme/api#42")]
    [InlineData("https://bitbucket.org/acme/api/issues/42", "acme/api#42")]
    [InlineData("https://bitbucket.org/acme/api/issues/42/login-fails", "acme/api#42")]
    [InlineData("https://BITBUCKET.org/acme/api/issues/42?x=1", "acme/api#42")]
    public void TryParseTaskReference_ReadsReferencesAndUrls(string text, string expected)
    {
        Assert.True(Create().TryParseTaskReference(text, out string? key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC-123")]
    [InlineData("acme/api#0")]
    [InlineData("acme/api#x")]
    [InlineData("acme#42")]
    [InlineData("https://github.com/acme/api/issues/42")]
    [InlineData("https://bitbucket.org/acme/api/pull-requests/42")]
    [InlineData("https://bitbucket.org/acme/api/issues")]
    public void TryParseTaskReference_RejectsOtherText(string? text)
    {
        Assert.False(Create().TryParseTaskReference(text, out string? key));
        Assert.Null(key);
    }

    [Fact]
    public async Task GetTask_ReadsTheIssue_AndMapsDoneStatuses()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("repositories/acme/api/issues/12", FakeBitbucketApi.Response("issue.json"));

        TaskInfo? task = await bitbucket.GetTaskAsync("acme/api#12", CancellationToken);

        Assert.NotNull(task);
        Assert.Equal("acme/api#12", task.Key);
        Assert.Equal(new Uri("https://bitbucket.org/acme/api/issues/12/wont-be-fixed"), task.Url);
        Assert.Equal(("Won't fix", TaskStatusCategory.Done), (task.Status, task.StatusCategory));
    }

    [Fact]
    public async Task GetTask_ForAnIssueThatDoesntExist_ReturnsNull()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("repositories/acme/api/issues/99", FakeBitbucketApi.Response("not-found.json"),
            HttpStatusCode.NotFound);

        Assert.Null(await bitbucket.GetTaskAsync("acme/api#99", CancellationToken));
    }

    [Fact]
    public async Task GetTask_WithAnInvalidKey_Throws()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => bitbucket.GetTaskAsync("ABC-1", CancellationToken));
    }

    private static readonly string[] AcmeApiRemotes =
        ["https://bitbucket.org/acme/api.git", "git@bitbucket.org:acme/api.git"];

    private static readonly string PullRequestsPath =
        "repositories/acme/api/pullrequests?q="
        + Uri.EscapeDataString("source.branch.name = \"api-42-fix-login\"")
        + "&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=created_on&pagelen=100";

    [Fact]
    public async Task GetPullRequests_ReadsStateReviewAndChecks_OfPullRequestsFromTheBranch()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(PullRequestsPath, FakeBitbucketApi.Response("pullrequests.json"))
            .Respond("repositories/acme/api/pullrequests/5/statuses?pagelen=100",
                FakeBitbucketApi.Response("statuses-successful.json"))
            .Respond("repositories/acme/api/pullrequests/6/statuses?pagelen=100",
                FakeBitbucketApi.Response("statuses-failed.json"))
            .Respond("repositories/acme/api/pullrequests/9/statuses?pagelen=100", """{"values":[]}""");

        IReadOnlyList<PullRequestInfo> pullRequests =
            await bitbucket.GetPullRequestsAsync(AcmeApiRemotes, "api-42-fix-login", CancellationToken);

        // The merged one is from another fork's branch of the same name.
        Assert.Collection(
            pullRequests,
            open =>
            {
                Assert.Equal(("bitbucket", "acme/api", 5, "Fix login"),
                    (open.Source, open.Repository, open.Number, open.Title));
                Assert.Equal(new Uri("https://bitbucket.org/acme/api/pull-requests/5"), open.Url);
                Assert.Equal(PullRequestState.Open, open.State);
                Assert.Equal(PullRequestChecks.Succeeded, open.Checks);
                Assert.Equal(PullRequestReview.ReviewRequired, open.Review);
            },
            draft =>
            {
                Assert.Equal(PullRequestState.Draft, draft.State);
                Assert.Equal(PullRequestChecks.Failed, draft.Checks);
                Assert.Equal(PullRequestReview.ChangesRequested, draft.Review);
            },
            declined =>
            {
                Assert.Equal(9, declined.Number);
                Assert.Equal(PullRequestState.Closed, declined.State);
                Assert.Null(declined.Checks);
                Assert.Null(declined.Review);
            });
    }

    [Fact]
    public async Task GetPullRequests_WhenTheTokenCantReadChecks_StillListsThePullRequests()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        string only = """{"values":[{"id":5,"title":"Fix","state":"MERGED","source":{"repository":{"full_name":"acme/api"}},"links":{"html":{"href":"https://bitbucket.org/acme/api/pull-requests/5"}}}]}""";
        _api.Respond(PullRequestsPath, only)
            .Respond("repositories/acme/api/pullrequests/5/statuses?pagelen=100",
                FakeBitbucketApi.Response("forbidden.json"), HttpStatusCode.Forbidden);

        PullRequestInfo pullRequest = Assert.Single(
            await bitbucket.GetPullRequestsAsync(AcmeApiRemotes, "api-42-fix-login", CancellationToken));

        Assert.Equal(PullRequestState.Merged, pullRequest.State);
        Assert.Null(pullRequest.Checks);
    }

    [Fact]
    public async Task GetPullRequests_WhenTheTokenCantReadThem_ThrowsPermissionException()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(PullRequestsPath, FakeBitbucketApi.Response("forbidden.json"), HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => bitbucket.GetPullRequestsAsync(AcmeApiRemotes, "api-42-fix-login", CancellationToken));

        Assert.Contains("read:pullrequest:bitbucket", exception.Message);
    }

    [Fact]
    public async Task GetPullRequests_ForARepositoryItCantFind_ThrowsPermissionException()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(PullRequestsPath, FakeBitbucketApi.Response("not-found.json"), HttpStatusCode.NotFound)
            .Respond("repositories/acme/api", FakeBitbucketApi.Response("not-found.json"), HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => bitbucket.GetPullRequestsAsync(AcmeApiRemotes, "api-42-fix-login", CancellationToken));

        Assert.Contains("acme/api", exception.Message);
    }

    [Fact]
    public async Task GetPullRequests_ForARepositoryWithoutPullRequests_ReturnsNone()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(PullRequestsPath, """{"values":[]}""")
            .Respond("repositories/acme/api", """{"full_name":"acme/api"}""");

        Assert.Empty(await bitbucket.GetPullRequestsAsync(AcmeApiRemotes, "api-42-fix-login", CancellationToken));
    }

    [Fact]
    public async Task GetPullRequests_IgnoresRemotesOnOtherServices()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();

        IReadOnlyList<PullRequestInfo> pullRequests = await bitbucket.GetPullRequestsAsync(
            ["https://github.com/acme/api.git", "/home/mia/api"], "main", CancellationToken);

        Assert.Empty(pullRequests);
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task RateLimit_IsWaitedOut_WhenItResetsSoon()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", "", HttpStatusCode.TooManyRequests, ("Retry-After", "5"))
            .Respond("user", FakeBitbucketApi.Response("user.json"));

        Task<string> call = bitbucket.GetAuthenticatedUserAsync(CancellationToken);
        await AdvanceUntilAsync(() => call.IsCompleted || _api.Requests.Count > 1, TimeSpan.FromSeconds(1));

        Assert.Equal("Mia Krystof", await call);
        Assert.Equal(2, _api.Requests.Count);
        Assert.True(_time.GetUtcNow() >= new DateTimeOffset(2026, 11, 3, 12, 0, 5, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("1800", 1800)]
    [InlineData(null, 60)]
    public async Task RateLimit_Throws_WhenItResetsLaterThanTheMaximumWait(string? retryAfter, int expectedSeconds)
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        (string, string)[] headers = retryAfter is null ? [] : [("Retry-After", retryAfter)];
        _api.Respond("user", "", HttpStatusCode.TooManyRequests, headers);

        var exception = await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => bitbucket.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal(_time.GetUtcNow().AddSeconds(expectedSeconds), exception.RetryAt);
        Assert.Equal("bitbucket", exception.IntegrationId);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task RateLimit_GivesUp_AfterRetryingAFewTimes()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", "", HttpStatusCode.TooManyRequests, ("Retry-After", "0"));

        await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => bitbucket.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal(BitbucketApiClient.MaxRateLimitRetries + 1, _api.Requests.Count);
    }

    [Fact]
    public async Task ServerError_IsAnIntegrationExceptionWithBitbucketsMessage()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", """{"type":"error","error":{"message":"Something broke"}}""",
            HttpStatusCode.InternalServerError);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => bitbucket.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal("Bitbucket returned an error (500 Internal Server Error): Something broke", exception.Message);
    }

    [Fact]
    public async Task NetworkFailure_ThrowsIntegrationException()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Fail("user");

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => bitbucket.GetAuthenticatedUserAsync(CancellationToken));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task UnreadableResponse_ThrowsIntegrationException()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", "<html>Log in</html>");

        await Assert.ThrowsAsync<IntegrationException>(() => bitbucket.GetAuthenticatedUserAsync(CancellationToken));
    }

    private BitbucketIntegration Create(BitbucketIntegrationOptions? options = null) =>
        new(options ?? new BitbucketIntegrationOptions { Email = Email }, _store, _httpClient, NullLogger.Instance,
            _time);

    private async Task<BitbucketIntegration> CreateAuthenticatedAsync(BitbucketIntegrationOptions? options = null)
    {
        BitbucketIntegration bitbucket = Create(options);
        await bitbucket.Credentials.SaveAsync(Token, CancellationToken);
        return bitbucket;
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
}
