using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.GitHub;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.GitHub;

public sealed class GitHubPullRequestTests : IDisposable
{
    private const string GraphQlPath = "graphql";
    private const string Branch = "api-42-fix-login";

    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeGitHubApi _api = new();
    private readonly HttpClient _httpClient;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public GitHubPullRequestTests()
    {
        _httpClient = new HttpClient(_api);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _httpClient.Dispose();

    [Fact]
    public async Task GetPullRequestsAsync_QueriesTheRemotesRepository_AndReadsEachPullRequest()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, Response("acme/api",
            PullRequest(1, "OPEN", head: "https://github.com/acme/api", checks: "SUCCESS"),
            PullRequest(2, "OPEN", isDraft: true, head: "https://github.com/acme/api", checks: "PENDING"),
            PullRequest(3, "MERGED", head: "https://github.com/acme/api", checks: "FAILURE"),
            PullRequest(4, "CLOSED", head: "https://github.com/acme/api")));

        IReadOnlyList<PullRequestInfo> pullRequests = await github.GetPullRequestsAsync(
            ["git@github.com:acme/api.git"], Branch, CancellationToken);

        Assert.Equal(
            [
                new PullRequestInfo("github", "acme/api", 1, "PR 1", new Uri("https://github.com/acme/api/pull/1"),
                    PullRequestState.Open) { Checks = PullRequestChecks.Succeeded },
                new PullRequestInfo("github", "acme/api", 2, "PR 2", new Uri("https://github.com/acme/api/pull/2"),
                    PullRequestState.Draft) { Checks = PullRequestChecks.InProgress },
                new PullRequestInfo("github", "acme/api", 3, "PR 3", new Uri("https://github.com/acme/api/pull/3"),
                    PullRequestState.Merged) { Checks = PullRequestChecks.Failed },
                new PullRequestInfo("github", "acme/api", 4, "PR 4", new Uri("https://github.com/acme/api/pull/4"),
                    PullRequestState.Closed),
            ],
            pullRequests);

        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.github.com/graphql", request.Url.AbsoluteUri);
        Assert.Equal("Bearer github_pat_test", request.Authorization);
        JsonNode variables = JsonNode.Parse(request.Body!)!["variables"]!;
        Assert.Equal("acme", (string?)variables["owner"]);
        Assert.Equal("api", (string?)variables["name"]);
        Assert.Equal(Branch, (string?)variables["branch"]);
    }

    [Theory]
    [InlineData("ERROR", PullRequestChecks.Failed)]
    [InlineData("EXPECTED", PullRequestChecks.InProgress)]
    public async Task GetPullRequestsAsync_ReadsEveryChecksState(string state, PullRequestChecks checks)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, Response("acme/api",
            PullRequest(1, "OPEN", head: "https://github.com/acme/api", checks: state)));

        PullRequestInfo pullRequest = Assert.Single(await github.GetPullRequestsAsync(
            ["https://github.com/acme/api.git"], Branch, CancellationToken));

        Assert.Equal(checks, pullRequest.Checks);
    }

    [Fact]
    public async Task GetPullRequestsAsync_LeavesOutPullRequestsFromBranchesInOtherRepositories()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, Response("acme/api",
            PullRequest(1, "OPEN", head: "https://github.com/someone-else/api"),
            PullRequest(2, "OPEN", head: null),
            PullRequest(3, "OPEN", head: "https://github.com/acme/api")));

        IReadOnlyList<PullRequestInfo> pullRequests = await github.GetPullRequestsAsync(
            ["https://github.com/acme/api.git"], Branch, CancellationToken);

        Assert.Equal([3], pullRequests.Select(pullRequest => pullRequest.Number));
    }

    [Fact]
    public async Task GetPullRequestsAsync_ForAFork_QueriesEachRepositoryOnce_AndFindsTheForksBranchUpstream()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, Response("me/api"))
            .Respond(GraphQlPath, Response("acme/api", PullRequest(7, "OPEN", head: "https://github.com/me/api")));

        IReadOnlyList<PullRequestInfo> pullRequests = await github.GetPullRequestsAsync(
            ["git@github.com:me/api.git", "https://github.com/me/api", "https://github.com/acme/api.git"],
            Branch,
            CancellationToken);

        PullRequestInfo pullRequest = Assert.Single(pullRequests);
        Assert.Equal(("acme/api", 7), (pullRequest.Repository, pullRequest.Number));
        Assert.Equal(
            [("me", "api"), ("acme", "api")],
            _api.Requests.Select(request => JsonNode.Parse(request.Body!)!["variables"]!)
                .Select(variables => ((string)variables["owner"]!, (string)variables["name"]!)));
    }

    [Theory]
    [InlineData("https://gitlab.com/acme/api.git")]
    [InlineData("/srv/git/acme/api.git")]
    [InlineData("https://github.com/acme")]
    [InlineData("https://github.com/acme/api/more")]
    public async Task GetPullRequestsAsync_RemotesNotOnThisServer_SendsNothing(string remoteUrl)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();

        Assert.Empty(await github.GetPullRequestsAsync([remoteUrl], Branch, CancellationToken));
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task GetPullRequestsAsync_OnAnEnterpriseServer_UsesItsGraphQlApi()
    {
        var api = new FakeHttpApi("https://github.example.com/api/v3/");
        using var httpClient = new HttpClient(api);
        var github = new GitHubIntegration(
            new GitHubIntegrationOptions { Id = "github-github.example.com", ApiUrl = new Uri("https://github.example.com/api/v3/") },
            _store, httpClient, NullLogger.Instance, _time);
        await github.Credentials.SaveAsync("github_pat_test", CancellationToken);
        api.Respond("https://github.example.com/api/graphql", Response("acme/api",
            PullRequest(1, "OPEN", head: "https://github.example.com/acme/api")));

        IReadOnlyList<PullRequestInfo> pullRequests = await github.GetPullRequestsAsync(
            ["git@github.com:acme/api.git", "git@github.example.com:acme/api.git"], Branch, CancellationToken);

        Assert.Equal([1], pullRequests.Select(pullRequest => pullRequest.Number));
        Assert.Equal("https://github.example.com/api/graphql", Assert.Single(api.Requests).Url.AbsoluteUri);
    }

    [Fact]
    public async Task GetPullRequestsAsync_NoRepositoryFound_ThrowsPermissionException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, NotFound("acme/api"));

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => github.GetPullRequestsAsync(["https://github.com/acme/api"], Branch, CancellationToken));

        Assert.Equal("github", exception.IntegrationId);
        Assert.Contains("can't find acme/api", exception.Message);
    }

    [Fact]
    public async Task GetPullRequestsAsync_SomeRepositoryFound_LeavesOutThoseNotFound()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, Response("me/api", PullRequest(3, "OPEN", head: "https://github.com/me/api")))
            .Respond(GraphQlPath, NotFound("acme/api"));

        IReadOnlyList<PullRequestInfo> pullRequests = await github.GetPullRequestsAsync(
            ["https://github.com/me/api", "https://github.com/acme/api"], Branch, CancellationToken);

        Assert.Equal([3], pullRequests.Select(pullRequest => pullRequest.Number));
    }

    [Fact]
    public async Task GetPullRequestsAsync_TokenCantReadPullRequests_ThrowsPermissionException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, """
            {
              "data": {"repository": {"nameWithOwner": "acme/api", "pullRequests": null}},
              "errors": [{"type": "FORBIDDEN", "path": ["repository", "pullRequests"],
                          "message": "Resource not accessible by personal access token"}]
            }
            """);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => github.GetPullRequestsAsync(["https://github.com/acme/api"], Branch, CancellationToken));

        Assert.Equal("github", exception.IntegrationId);
        Assert.Contains("Pull requests", exception.Message);
    }

    [Fact]
    public async Task GetPullRequestsAsync_TokenCantReadChecks_LeavesThemOut()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, """
            {
              "data": {"repository": {"nameWithOwner": "acme/api", "pullRequests": {"nodes": [{
                "number": 1, "title": "PR 1", "url": "https://github.com/acme/api/pull/1", "state": "OPEN",
                "isDraft": false, "headRepository": {"url": "https://github.com/acme/api"},
                "commits": {"nodes": [{"commit": {"statusCheckRollup": null}}]}
              }]}}},
              "errors": [{"type": "FORBIDDEN",
                          "path": ["repository", "pullRequests", "nodes", 0, "commits", "nodes", 0, "commit",
                                   "statusCheckRollup"],
                          "message": "Resource not accessible by personal access token"}]
            }
            """);

        PullRequestInfo pullRequest = Assert.Single(await github.GetPullRequestsAsync(
            ["https://github.com/acme/api"], Branch, CancellationToken));

        Assert.Equal(PullRequestState.Open, pullRequest.State);
        Assert.Null(pullRequest.Checks);
    }

    [Fact]
    public async Task GetPullRequestsAsync_QueryFails_ThrowsWithGitHubsMessage()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, """{"errors": [{"message": "Field 'x' doesn't exist on type 'Query'"}]}""");

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.GetPullRequestsAsync(["https://github.com/acme/api"], Branch, CancellationToken));

        Assert.Contains("Field 'x' doesn't exist", exception.Message);
    }

    [Fact]
    public async Task GetPullRequestsAsync_RateLimited_Throws()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, """
            {"data": null, "errors": [{"type": "RATE_LIMITED", "message": "API rate limit exceeded"}]}
            """);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.GetPullRequestsAsync(["https://github.com/acme/api"], Branch, CancellationToken));

        Assert.Contains("rate limit", exception.Message);
    }

    [Fact]
    public async Task GetPullRequestsAsync_TokenRejected_ThrowsAuthenticationException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(GraphQlPath, """{"message": "Bad credentials"}""", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => github.GetPullRequestsAsync(["https://github.com/acme/api"], Branch, CancellationToken));
    }

    private static string NotFound(string repository) => $$"""
        {
          "data": {"repository": null},
          "errors": [{"type": "NOT_FOUND", "path": ["repository"],
                      "message": "Could not resolve to a Repository with the name '{{repository}}'."}]
        }
        """;

    private static string Response(string repository, params JsonObject[] pullRequests) => new JsonObject
    {
        ["data"] = new JsonObject
        {
            ["repository"] = new JsonObject
            {
                ["nameWithOwner"] = repository,
                ["pullRequests"] = new JsonObject { ["nodes"] = new JsonArray([.. pullRequests]) },
            },
        },
    }.ToJsonString();

    private static JsonObject PullRequest(
        int number,
        string state,
        string? head,
        bool isDraft = false,
        string? checks = null) => new()
    {
        ["number"] = number,
        ["title"] = $"PR {number}",
        ["url"] = $"https://github.com/acme/api/pull/{number}",
        ["state"] = state,
        ["isDraft"] = isDraft,
        ["headRepository"] = head is null ? null : new JsonObject { ["url"] = head },
        ["commits"] = new JsonObject
        {
            ["nodes"] = new JsonArray(new JsonObject
            {
                ["commit"] = new JsonObject
                {
                    ["statusCheckRollup"] = checks is null ? null : new JsonObject { ["state"] = checks },
                },
            }),
        },
    };

    private async Task<GitHubIntegration> CreateAuthenticatedAsync()
    {
        var github = new GitHubIntegration(new GitHubIntegrationOptions(), _store, _httpClient, NullLogger.Instance, _time);
        await github.Credentials.SaveAsync("github_pat_test", CancellationToken);
        return github;
    }
}
