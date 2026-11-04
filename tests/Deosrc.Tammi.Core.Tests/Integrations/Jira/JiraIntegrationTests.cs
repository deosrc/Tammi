using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Tammi.Core.Integrations;
using Tammi.Core.Integrations.Jira;
using Tammi.Core.Tests.Credentials;

namespace Tammi.Core.Tests.Integrations.Jira;

public class JiraIntegrationTests : IDisposable
{
    private const string Token = "ATATT3xFfGF0test";
    private const string Email = "mia@example.com";
    private const string DataCenterSiteUrl = "https://jira.example.com/jira/";
    private const string IssuePath = "issue/ABC-12?fields=summary,description,updated,status";

    private const string TenantInfoUrl = "https://acme.atlassian.net/_edge/tenant_info";
    private const string TenantInfo = """{"cloudId":"a436116f-02ce-4520-8fbb-7301462a1674"}""";
    private const string GatewayApiUrl =
        "https://api.atlassian.com/ex/jira/a436116f-02ce-4520-8fbb-7301462a1674/rest/api/2/";

    private static readonly string LoginJql = JiraIntegration.TextJql("login");

    private static readonly string DefaultSearchPath = CloudSearchPath(JiraIntegration.AssignedJql);

    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeJiraApi _api = new();
    private readonly HttpClient _httpClient;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));

    public JiraIntegrationTests()
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
    public async Task Cloud_AuthenticatesWithTheEmailAndApiToken()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", FakeJiraApi.Response("myself.json"));

        JiraUser user = await jira.GetAuthenticatedUserAsync(CancellationToken);

        Assert.Equal(new JiraUser("Mia Krystof", Email), user);
        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://acme.atlassian.net/rest/api/2/myself"), request.Url);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Token}"))}",
            request.Authorization);
        Assert.Equal("application/json", request.Headers["Accept"]);
        Assert.StartsWith("Tammi/", request.Headers["User-Agent"]);
    }

    [Fact]
    public async Task DataCenter_AuthenticatesWithThePersonalAccessToken()
    {
        using var api = new FakeJiraApi(DataCenterSiteUrl);
        using var httpClient = new HttpClient(api);
        JiraIntegration jira = await CreateAuthenticatedAsync(DataCenterOptions(), httpClient);
        api.Respond("myself", FakeJiraApi.Response("myself.json"));

        await jira.GetAuthenticatedUserAsync(CancellationToken);

        RecordedRequest request = Assert.Single(api.Requests);
        Assert.Equal(new Uri("https://jira.example.com/jira/rest/api/2/myself"), request.Url);
        Assert.Equal($"Bearer {Token}", request.Authorization);
    }

    [Fact]
    public async Task Credentials_AreStoredUnderTheIntegrationId()
    {
        JiraIntegration jira = Create();

        await jira.Credentials.SaveAsync(Token, CancellationToken);

        Assert.Equal("jira-acme", jira.Id);
        Assert.Equal(Token, _store.Secrets["integration:jira-acme"]);
    }

    [Fact]
    public void Name_DefaultsToTheSiteHost()
    {
        Assert.Equal("JIRA (acme.atlassian.net)", Create().Name);
        Assert.Equal("Work", Create(CloudOptions() with { Name = "Work" }).Name);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WithoutSendingARequest_WhenNotAuthenticated()
    {
        JiraIntegration jira = Create();

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => jira.GetAssignedTasksAsync(CancellationToken));

        Assert.Equal("jira-acme", exception.IntegrationId);
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WhenTheCredentialsAreRejected()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(DefaultSearchPath, "Client must be authenticated to access this resource.",
            HttpStatusCode.Unauthorized);
        _api.Respond(TenantInfoUrl, TenantInfo);
        _api.Respond(GatewayApiUrl + DefaultSearchPath, """{"code":401,"message":"Unauthorized"}""",
            HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => jira.GetAssignedTasksAsync(CancellationToken));

        Assert.Equal("jira-acme", exception.IntegrationId);
        Assert.Contains("email address and API token", exception.Message);
        Assert.Contains("read:jira-work and read:jira-user", exception.Message);
        Assert.Contains("Settings", exception.Message);
    }

    [Fact]
    public async Task Cloud_ThrowsAuthenticationException_WhenTheSiteDoesntSayItsCloudId()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "", HttpStatusCode.Unauthorized);
        _api.Respond(TenantInfoUrl, "", HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Contains("email address and API token", exception.Message);
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task Cloud_UsesTheApiGateway_ForATokenWithScopes()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "", HttpStatusCode.Unauthorized);
        _api.Respond(TenantInfoUrl, TenantInfo);
        _api.Respond(GatewayApiUrl + "myself", FakeJiraApi.Response("myself.json"));
        _api.Respond(GatewayApiUrl + IssuePath, FakeJiraApi.Response("issue.json"));

        JiraUser user = await jira.GetAuthenticatedUserAsync(CancellationToken);
        TaskInfo? task = await jira.GetTaskAsync("ABC-12", CancellationToken);

        Assert.Equal("Mia Krystof", user.DisplayName);
        Assert.Equal(new Uri("https://acme.atlassian.net/browse/ABC-12"), task?.Url);
        Assert.Equal(
            [
                "https://acme.atlassian.net/rest/api/2/myself",
                "https://acme.atlassian.net/_edge/tenant_info",
                GatewayApiUrl + "myself",
                GatewayApiUrl + IssuePath,
            ],
            _api.Requests.Select(request => request.Url.AbsoluteUri));
        Assert.Null(_api.Requests[1].Authorization);
        Assert.Equal(_api.Requests[0].Authorization, _api.Requests[2].Authorization);
    }

    [Fact]
    public async Task Cloud_GoesBackToTheSite_WhenTheGatewayRejectsANewToken()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "", HttpStatusCode.Unauthorized);
        _api.Respond("myself", FakeJiraApi.Response("myself.json"));
        _api.Respond(TenantInfoUrl, TenantInfo);
        _api.Respond(GatewayApiUrl + "myself", FakeJiraApi.Response("myself.json"));
        _api.Respond(GatewayApiUrl + "myself", "", HttpStatusCode.Unauthorized);

        await jira.GetAuthenticatedUserAsync(CancellationToken);
        await jira.GetAuthenticatedUserAsync(CancellationToken);
        await jira.GetAuthenticatedUserAsync(CancellationToken);

        Assert.Equal(
            [
                "https://acme.atlassian.net/rest/api/2/myself",
                "https://acme.atlassian.net/_edge/tenant_info",
                GatewayApiUrl + "myself",
                GatewayApiUrl + "myself",
                "https://acme.atlassian.net/rest/api/2/myself",
                "https://acme.atlassian.net/rest/api/2/myself",
            ],
            _api.Requests.Select(request => request.Url.AbsoluteUri));
    }

    [Fact]
    public async Task Cloud_SaysWhichScopesAreNeeded_WhenATokenLacksThem()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "", HttpStatusCode.Unauthorized);
        _api.Respond(TenantInfoUrl, TenantInfo);
        _api.Respond(GatewayApiUrl + "myself", """{"code":401,"message":"Unauthorized; scope does not match"}""",
            HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Contains("doesn't have the scopes", exception.Message);
        Assert.Contains("read:jira-work and read:jira-user", exception.Message);
    }

    [Fact]
    public async Task DataCenter_ThrowsAuthenticationException_WhenTheTokenIsRejected()
    {
        using var api = new FakeJiraApi(DataCenterSiteUrl);
        using var httpClient = new HttpClient(api);
        JiraIntegration jira = await CreateAuthenticatedAsync(DataCenterOptions(), httpClient);
        api.Respond("myself", "", HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Contains("personal access token", exception.Message);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WhenJiraWantsACaptcha()
    {
        using var api = new FakeJiraApi(DataCenterSiteUrl);
        using var httpClient = new HttpClient(api);
        JiraIntegration jira = await CreateAuthenticatedAsync(DataCenterOptions(), httpClient);
        api.Respond("myself", "", HttpStatusCode.Forbidden,
            ("X-Authentication-Denied-Reason",
                "CAPTCHA_CHALLENGE; login-url=https://jira.example.com/jira/login.jsp"));

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Contains("CAPTCHA", exception.Message);
    }

    [Fact]
    public async Task GetAssignedTasksAsync_ListsTheUsersIssuesThatAreStillToDo()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(DefaultSearchPath, FakeJiraApi.Response("search-cloud-page2.json"));

        IReadOnlyList<TaskInfo> tasks = await jira.GetAssignedTasksAsync(CancellationToken);

        Assert.Equal(["XYZ-3"], tasks.Select(task => task.Key));
        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal("/rest/api/2/search/jql", request.Url.AbsolutePath);
        Assert.Equal("assignee = currentUser() AND statusCategory = \"To Do\" ORDER BY updated DESC",
            request.Query("jql"));
        Assert.Equal("summary,description,updated,status", request.Query("fields"));
    }

    [Fact]
    public async Task GetAssignedTasksAsync_ReadsIssuesAsTasks()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync(CloudOptions() with { MaxSearchResults = 2 });
        _api.Respond(CloudSearchPath(JiraIntegration.AssignedJql, maxResults: 2),
            FakeJiraApi.Response("search-cloud.json"));

        IReadOnlyList<TaskInfo> tasks = await jira.GetAssignedTasksAsync(CancellationToken);

        TaskInfo task = tasks[0];
        Assert.Equal(new TaskReference("jira-acme", "ABC-12"), task.Reference);
        Assert.Equal("Login fails with an expired session", task.Title);
        Assert.Equal(new Uri("https://acme.atlassian.net/browse/ABC-12"), task.Url);
        Assert.StartsWith("Steps to reproduce:\n# Sign in", task.Description);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.FromHours(1)), task.Updated);
        Assert.Null(tasks[1].Description);
        Assert.Null(tasks[1].Updated);
    }

    [Fact]
    public async Task SearchTasksAsync_SearchesTheTextOfIssues()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(CloudSearchPath(JiraIntegration.TextJql("expired session")),
            FakeJiraApi.Response("search-cloud-page2.json"));

        await jira.SearchTasksAsync("  expired session ", CancellationToken);

        Assert.Equal("text ~ \"expired session\" ORDER BY updated DESC", Assert.Single(_api.Requests).Query("jql"));
    }

    [Theory]
    [InlineData("login", "login")]
    [InlineData("  fix   login  ", "fix login")]
    [InlineData("say \"hi\" (now)", "say hi now")]
    [InlineData("a\\b c:d -e +f", "a b c d e f")]
    [InlineData("project = ABC", "project = ABC")]
    public void TextJql_QuotesTheWords_LeavingOutSpecialCharacters(string text, string phrase)
    {
        Assert.Equal($"text ~ \"{phrase}\" ORDER BY updated DESC", JiraIntegration.TextJql(text));
    }

    [Fact]
    public async Task SearchTasksAsync_Throws_ForBlankText()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => jira.SearchTasksAsync(" ", CancellationToken));
    }

    [Fact]
    public async Task SearchTasksAsync_OnCloud_FollowsPageTokens_UntilTheLastPage()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        string first = CloudSearchPath(LoginJql);
        _api.Respond(first, FakeJiraApi.Response("search-cloud.json"))
            .Respond(first + "&nextPageToken=" + Uri.EscapeDataString(
                    "Ch0jU3RyaW5nJlFVSkQmJlNXNTBaWEpsYzNSbFpBPT0lMjM="),
                FakeJiraApi.Response("search-cloud-page2.json"));

        IReadOnlyList<TaskInfo> tasks = await jira.SearchTasksAsync("login", CancellationToken);

        Assert.Equal(["ABC-12", "ABC-9", "XYZ-3"], tasks.Select(task => task.Key));
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task SearchTasksAsync_StopsAtTheMaximumResults()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync(CloudOptions() with { MaxSearchResults = 1 });
        _api.Respond(CloudSearchPath(LoginJql, maxResults: 1), FakeJiraApi.Response("search-cloud.json"));

        IReadOnlyList<TaskInfo> tasks = await jira.SearchTasksAsync("login", CancellationToken);

        Assert.Equal(["ABC-12"], tasks.Select(task => task.Key));
        Assert.Equal("1", Assert.Single(_api.Requests).Query("maxResults"));
    }

    [Fact]
    public async Task SearchTasksAsync_OnCloud_Stops_WhenThePageTokenDoesntChange()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        string first = CloudSearchPath(LoginJql);
        string token = """{"issues":[{"key":"ABC-1","fields":{"summary":"One"}}],"nextPageToken":"t1"}""";
        _api.Respond(first, token).Respond(first + "&nextPageToken=t1", token);

        IReadOnlyList<TaskInfo> tasks = await jira.SearchTasksAsync("login", CancellationToken);

        Assert.Equal(2, tasks.Count);
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task SearchTasksAsync_OnDataCenter_PagesByPosition_UntilTheTotal()
    {
        using var api = new FakeJiraApi(DataCenterSiteUrl);
        using var httpClient = new HttpClient(api);
        JiraIntegration jira = await CreateAuthenticatedAsync(DataCenterOptions() with { PageSize = 2 }, httpClient);
        string first = $"search?jql={Uri.EscapeDataString(LoginJql)}&fields=summary,description,updated,status&maxResults=2";
        api.Respond(first + "&startAt=0", FakeJiraApi.Response("search-dc.json"))
            .Respond(first + "&startAt=2", FakeJiraApi.Response("search-dc-page2.json"));

        IReadOnlyList<TaskInfo> tasks = await jira.SearchTasksAsync("login", CancellationToken);

        Assert.Equal(["ABC-12", "ABC-9", "XYZ-3"], tasks.Select(task => task.Key));
        Assert.Equal(new Uri("https://jira.example.com/jira/browse/ABC-12"), tasks[0].Url);
        Assert.Null(tasks[1].Description);
        Assert.Equal(2, api.Requests.Count);
    }

    [Fact]
    public async Task SearchTasksAsync_OnDataCenter_Stops_WhenAPageIsEmpty()
    {
        using var api = new FakeJiraApi(DataCenterSiteUrl);
        using var httpClient = new HttpClient(api);
        JiraIntegration jira = await CreateAuthenticatedAsync(DataCenterOptions(), httpClient);
        api.Respond($"search?jql={Uri.EscapeDataString(JiraIntegration.TextJql("x"))}&fields=summary,description,updated,status&maxResults=50&startAt=0",
            """{"startAt":0,"maxResults":50,"total":10,"issues":[]}""");

        Assert.Empty(await jira.SearchTasksAsync("x", CancellationToken));
        Assert.Single(api.Requests);
    }

    [Theory]
    [InlineData("ABC-12")]
    [InlineData(" abc-12 ")]
    [InlineData("https://acme.atlassian.net/browse/ABC-12")]
    [InlineData("https://acme.atlassian.net/jira/software/projects/ABC/boards/1?selectedIssue=ABC-12")]
    public void TryParseTaskReference_ReadsAKeyOrUrl(string text)
    {
        Assert.True(Create().TryParseTaskReference(text, out string? key));
        Assert.Equal("ABC-12", key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("login bug")]
    [InlineData("acme/api#12")]
    [InlineData("https://other.atlassian.net/browse/ABC-12")]
    public void TryParseTaskReference_RejectsOtherText(string? text)
    {
        Assert.False(Create().TryParseTaskReference(text, out _));
    }

    [Fact]
    public async Task Errors_IncludeFieldMessages()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(CloudSearchPath(JiraIntegration.TextJql("x")), """{"errorMessages":[],"errors":{"jql":"The JQL is too long."}}""",
            HttpStatusCode.BadRequest);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => jira.SearchTasksAsync("x", CancellationToken));

        Assert.EndsWith(": The JQL is too long.", exception.Message);
    }

    [Fact]
    public async Task GetTaskAsync_ReadsTheIssue()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(IssuePath, FakeJiraApi.Response("issue.json"));

        TaskInfo? task = await jira.GetTaskAsync("ABC-12", CancellationToken);

        Assert.NotNull(task);
        Assert.Equal("Login fails with an expired session", task.Title);
        Assert.Equal("jira-acme", task.Source);
        Assert.EndsWith("{code}401 Unauthorized{code}", task.Description);
        Assert.Equal("In Review", task.Status);
        Assert.Equal(TaskStatusCategory.InProgress, task.StatusCategory);
    }

    [Theory]
    [InlineData("new", TaskStatusCategory.ToDo)]
    [InlineData("indeterminate", TaskStatusCategory.InProgress)]
    [InlineData("done", TaskStatusCategory.Done)]
    [InlineData("undefined", null)]
    public async Task GetTaskAsync_ReadsTheStatusCategory(string key, TaskStatusCategory? category)
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(IssuePath,
            """{"key":"ABC-12","fields":{"summary":"S","status":{"name":"Custom","statusCategory":{"key":"KEY"}}}}"""
                .Replace("KEY", key, StringComparison.Ordinal));

        TaskInfo? task = await jira.GetTaskAsync("ABC-12", CancellationToken);

        Assert.Equal("Custom", task?.Status);
        Assert.Equal(category, task?.StatusCategory);
    }

    [Fact]
    public async Task GetTaskAsync_HasNoStatus_WhenJiraDoesntSay()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(IssuePath, """{"key":"ABC-12","fields":{"summary":"S"}}""");

        TaskInfo? task = await jira.GetTaskAsync("ABC-12", CancellationToken);

        Assert.Null(task?.Status);
        Assert.Null(task?.StatusCategory);
    }

    [Fact]
    public async Task GetTaskAsync_UsesTheNewKey_OfAMovedIssue()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("issue/ABC-1?fields=summary,description,updated,status",
            """{"id":"10103","key":"XYZ-3","fields":{"summary":"Moved","description":null}}""");

        TaskInfo? task = await jira.GetTaskAsync("ABC-1", CancellationToken);

        Assert.Equal("XYZ-3", task?.Key);
        Assert.Equal(new Uri("https://acme.atlassian.net/browse/XYZ-3"), task?.Url);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsNull_WhenTheIssueIsMissing()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond(IssuePath, FakeJiraApi.Response("not-found.json"), HttpStatusCode.NotFound);

        Assert.Null(await jira.GetTaskAsync("ABC-12", CancellationToken));
    }

    [Theory]
    [InlineData("acme/api#1")]
    [InlineData("ABC")]
    [InlineData("ABC-0")]
    [InlineData("1BC-1")]
    public async Task GetTaskAsync_Throws_ForAKeyThatIsntAJiraIssueKey(string key)
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => jira.GetTaskAsync(key, CancellationToken));
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task RateLimit_IsWaitedOut_WhenItResetsSoon()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "", HttpStatusCode.TooManyRequests, ("Retry-After", "5"))
            .Respond("myself", FakeJiraApi.Response("myself.json"));

        Task<JiraUser> call = jira.GetAuthenticatedUserAsync(CancellationToken);
        await AdvanceUntilAsync(() => call.IsCompleted || _api.Requests.Count > 1, TimeSpan.FromSeconds(1));

        Assert.Equal("Mia Krystof", (await call).DisplayName);
        Assert.Equal(2, _api.Requests.Count);
        Assert.True(_time.GetUtcNow() >= new DateTimeOffset(2026, 11, 3, 12, 0, 5, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "1800", null, 1800)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "120", null, 120)]
    [InlineData(HttpStatusCode.TooManyRequests, null, "2026-11-03T12:30:00Z", 1800)]
    [InlineData(HttpStatusCode.TooManyRequests, null, null, 60)]
    public async Task RateLimit_Throws_WhenItResetsLaterThanTheMaximumWait(
        HttpStatusCode status,
        string? retryAfter,
        string? reset,
        int expectedSeconds)
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        (string, string)[] headers =
        [
            .. retryAfter is null ? [] : new[] { ("Retry-After", retryAfter) },
            .. reset is null ? [] : new[] { ("X-RateLimit-Reset", reset) },
        ];
        _api.Respond("myself", "", status, headers);

        var exception = await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal(_time.GetUtcNow().AddSeconds(expectedSeconds), exception.RetryAt);
        Assert.Equal("jira-acme", exception.IntegrationId);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task RateLimit_GivesUp_AfterRetryingAFewTimes()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "", HttpStatusCode.TooManyRequests, ("Retry-After", "0"));

        await Assert.ThrowsAsync<IntegrationRateLimitException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.Equal(4, _api.Requests.Count);
    }

    [Fact]
    public async Task ServiceUnavailable_WithoutRetryAfter_IsAnError()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "<html>Down for maintenance</html>", HttpStatusCode.ServiceUnavailable);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.IsNotType<IntegrationRateLimitException>(exception);
        Assert.Equal("JIRA returned an error (503 Service Unavailable).", exception.Message);
    }

    [Fact]
    public async Task NetworkFailure_ThrowsIntegrationException()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Fail("myself");

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => jira.GetAuthenticatedUserAsync(CancellationToken));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task UnreadableResponse_ThrowsIntegrationException()
    {
        JiraIntegration jira = await CreateAuthenticatedAsync();
        _api.Respond("myself", "<html>Log in</html>");

        await Assert.ThrowsAsync<IntegrationException>(() => jira.GetAuthenticatedUserAsync(CancellationToken));
    }

    [Fact]
    public void Constructor_Throws_ForCloudWithoutAnEmailAddress()
    {
        Assert.Throws<ArgumentException>(() => Create(CloudOptions() with { Email = null }));
        Assert.Equal("jira-dc", Create(DataCenterOptions()).Id);
    }

    [Fact]
    public void Constructor_Throws_ForInvalidOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(CloudOptions() with { PageSize = 101 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(CloudOptions() with { PageSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(CloudOptions() with { MaxSearchResults = 0 }));
        Assert.Throws<ArgumentException>(
            () => Create(CloudOptions() with { SiteUrl = new Uri("ftp://acme.atlassian.net") }));
    }

    private static string CloudSearchPath(string jql, int maxResults = 50) =>
        $"search/jql?jql={Uri.EscapeDataString(jql)}&fields=summary,description,updated,status&maxResults={maxResults}";

    private static JiraIntegrationOptions CloudOptions() => new()
    {
        Id = "jira-acme",
        SiteUrl = new Uri("https://acme.atlassian.net"),
        Email = Email,
    };

    private static JiraIntegrationOptions DataCenterOptions() => new()
    {
        Id = "jira-dc",
        SiteUrl = new Uri("https://jira.example.com/jira"),
        Deployment = JiraDeployment.DataCenter,
    };

    private JiraIntegration Create(JiraIntegrationOptions? options = null, HttpClient? httpClient = null) =>
        new(options ?? CloudOptions(), _store, httpClient ?? _httpClient, _time);

    private async Task<JiraIntegration> CreateAuthenticatedAsync(
        JiraIntegrationOptions? options = null,
        HttpClient? httpClient = null)
    {
        JiraIntegration jira = Create(options, httpClient);
        await jira.Credentials.SaveAsync(Token, CancellationToken);
        return jira;
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
