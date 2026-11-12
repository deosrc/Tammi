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
    private const string WorkspacesPath = "user/workspaces?pagelen=100";
    private const string ReposPath = "repositories/acme?role=member&pagelen=100";
    private const string ReposPage2Url = "https://api.bitbucket.org/2.0/repositories/acme?role=member&pagelen=100&page=2";
    private const string MiaReposPath = "repositories/mia?role=member&pagelen=100";
    private const string OnlyAcmeWorkspace = """{"values":[{"workspace":{"slug":"acme"}}]}""";

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
    /// Answers the user's workspaces, acme and mia, and their repositories: acme/api, acme/web and acme/docs (over two
    /// pages) and mia/notes.
    /// </summary>
    private FakeBitbucketApi RespondWithRepositories() =>
        (FakeBitbucketApi)_api
            .Respond(WorkspacesPath, FakeBitbucketApi.Response("workspaces.json"))
            .Respond(ReposPath, FakeBitbucketApi.Response("repos.json"))
            .Respond(ReposPage2Url, FakeBitbucketApi.Response("repos-page2.json"))
            .Respond(MiaReposPath, FakeBitbucketApi.Response("repos-mia.json"));

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
        _api.Respond(WorkspacesPath, FakeBitbucketApi.Response("forbidden.json"), HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => bitbucket.GetRepositoriesAsync(CancellationToken));

        Assert.Contains("lack one or more required privilege scopes", exception.Message);
        Assert.Contains("read:workspace:bitbucket", exception.Message);
    }

    [Fact]
    public async Task GetRepositories_ListsEveryPage_WithCloneUrlsForHttpsAndSsh()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        RespondWithRepositories();

        IReadOnlyList<IntegrationRepository> repositories = await bitbucket.GetRepositoriesAsync(CancellationToken);

        Assert.Equal(["acme/api", "acme/web", "acme/docs", "mia/notes"],
            repositories.Select(repository => repository.FullName));
        IntegrationRepository api = repositories[0];
        Assert.Equal(("bitbucket", "acme", "api"), (api.Source, api.Owner, api.Name));
        // The user name in Bitbucket's HTTPS URL is left out.
        Assert.Equal(["https://bitbucket.org/acme/api.git", "git@bitbucket.org:acme/api.git"], api.CloneUrls);
        Assert.Equal(new Uri("https://bitbucket.org/acme/api"), api.Url);
        Assert.Equal(
            [WorkspacesPath, ReposPath, "repositories/acme?role=member&pagelen=100&page=2", MiaReposPath],
            _api.Requests.Select(request => request.Url.AbsoluteUri.Replace("https://api.bitbucket.org/2.0/", "")));
    }

    [Fact]
    public async Task GetRepositories_ReadsAWorkspaceListedAsItself()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(WorkspacesPath, """{"values":[{"slug":"acme"}]}""")
            .Respond(ReposPath, """{"values":[]}""");

        Assert.Empty(await bitbucket.GetRepositoriesAsync(CancellationToken));
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task GetRepositories_DoesNotFollowAPagingLinkToAnotherHost()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        string page = FakeBitbucketApi.Response("repos.json").Replace(
            ReposPage2Url, "https://evil.example.com/steal", StringComparison.Ordinal);
        _api.Respond(WorkspacesPath, OnlyAcmeWorkspace).Respond(ReposPath, page);

        IReadOnlyList<IntegrationRepository> repositories = await bitbucket.GetRepositoriesAsync(CancellationToken);

        Assert.Equal(2, repositories.Count);
        Assert.Equal(2, _api.Requests.Count);
    }

    private static readonly string[] AcmeApiRemotes =
        ["https://bitbucket.org/acme/api.git", "git@bitbucket.org:acme/api.git"];

    private static readonly string PullRequestsPath =
        "repositories/acme/api/pullrequests?q="
        + Uri.EscapeDataString("source.branch.name = \"api-42-fix-login\"")
        + "&state=OPEN&state=MERGED&state=DECLINED&state=SUPERSEDED&sort=created_on&pagelen=50&fields=%2Bvalues.participants";

    [Fact]
    public async Task GetPullRequests_ReadsStateReviewAndChecks_OfPullRequestsFromTheBranch()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond(PullRequestsPath, FakeBitbucketApi.Response("pullrequests.json"))
            .Respond("repositories/acme/api/pullrequests/5/statuses?pagelen=50",
                FakeBitbucketApi.Response("statuses-successful.json"))
            .Respond("repositories/acme/api/pullrequests/6/statuses?pagelen=50",
                FakeBitbucketApi.Response("statuses-failed.json"))
            .Respond("repositories/acme/api/pullrequests/9/statuses?pagelen=50", """{"values":[]}""");

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
            .Respond("repositories/acme/api/pullrequests/5/statuses?pagelen=50",
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
