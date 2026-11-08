using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.GitHub;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.GitHub;

public sealed class GitHubSshKeyTests : IDisposable
{
    private const string KeysPath = "user/keys?per_page=100";
    private const string Key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz";

    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeGitHubApi _api = new();
    private readonly HttpClient _httpClient;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public GitHubSshKeyTests()
    {
        _httpClient = new HttpClient(_api);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _httpClient.Dispose();

    [Fact]
    public async Task GetSshKeysAsync_ReturnsTheKeys_FromEveryPage()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(KeysPath, $$"""[{"id": 1, "key": "{{Key}}", "title": "laptop"}]""",
                headers: ("Link", $"<{FakeGitHubApi.Url("user/keys?per_page=100&page=2")}>; rel=\"next\""))
            .Respond("user/keys?per_page=100&page=2", """[{"id": 2, "key": "ssh-rsa AAAAB3NzaC1yc2E="}]""");

        IReadOnlyList<string>? keys = await github.GetSshKeysAsync(CancellationToken);

        Assert.Equal([Key, "ssh-rsa AAAAB3NzaC1yc2E="], keys);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetSshKeysAsync_TokenCantReadKeys_ReturnsNull(HttpStatusCode status)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(KeysPath, """{"message": "Resource not accessible by personal access token"}""", status);

        Assert.Null(await github.GetSshKeysAsync(CancellationToken));
    }

    [Fact]
    public async Task GetSshKeysAsync_TokenRejected_ThrowsAuthenticationException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond(KeysPath, """{"message": "Bad credentials"}""", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(() => github.GetSshKeysAsync(CancellationToken));
    }

    [Fact]
    public async Task AddSshKeyAsync_PostsTheTitleAndKey()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/keys", $$"""{"id": 3, "key": "{{Key}}", "title": "laptop"}""", HttpStatusCode.Created);

        await github.AddSshKeyAsync(" laptop ", Key + " ada@example.com", CancellationToken);

        RecordedRequest request = Assert.Single(_api.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("Bearer github_pat_test", request.Authorization);
        JsonNode body = JsonNode.Parse(request.Body!)!;
        Assert.Equal("laptop", (string?)body["title"]);
        Assert.Equal(Key + " ada@example.com", (string?)body["key"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task AddSshKeyAsync_TokenCantWriteKeys_ThrowsPermissionException(HttpStatusCode status)
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/keys", """{"message": "Resource not accessible by personal access token"}""", status);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => github.AddSshKeyAsync("laptop", Key, CancellationToken));

        Assert.Equal("github", exception.IntegrationId);
        Assert.Contains("Git SSH keys", exception.Message);
    }

    [Fact]
    public async Task AddSshKeyAsync_KeyAlreadyOnTheAccount_Succeeds()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/keys", KeyInUse, HttpStatusCode.UnprocessableEntity)
            .Respond(KeysPath, $$"""[{"id": 1, "key": "{{Key}}"}]""");

        await github.AddSshKeyAsync("laptop", Key + " ada@example.com", CancellationToken);
    }

    [Fact]
    public async Task AddSshKeyAsync_KeyInUseElsewhere_ThrowsWithGitHubsMessage()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/keys", KeyInUse, HttpStatusCode.UnprocessableEntity)
            .Respond(KeysPath, "[]");

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => github.AddSshKeyAsync("laptop", Key, CancellationToken));

        Assert.Contains("key is already in use", exception.Message);
    }

    [Fact]
    public async Task AddSshKeyAsync_TokenRejected_ThrowsAuthenticationException()
    {
        GitHubIntegration github = await CreateAuthenticatedAsync();
        _api.Respond("user/keys", """{"message": "Bad credentials"}""", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => github.AddSshKeyAsync("laptop", Key, CancellationToken));
    }

    [Theory]
    [InlineData("https://api.github.com/", "https://github.com/settings/ssh/new")]
    [InlineData("https://github.example.com/api/v3/", "https://github.example.com/settings/ssh/new")]
    public void NewSshKeyUrl_IsOnTheWebHost(string apiUrl, string expected)
    {
        GitHubIntegration github = Create(new GitHubIntegrationOptions { ApiUrl = new Uri(apiUrl) });

        Assert.Equal(expected, github.NewSshKeyUrl.AbsoluteUri);
    }

    private const string KeyInUse = """
        {"message": "Validation Failed", "errors": [{"resource": "PublicKey", "code": "custom", "field": "key",
         "message": "key is already in use"}]}
        """;

    private GitHubIntegration Create(GitHubIntegrationOptions? options = null) =>
        new(options ?? new GitHubIntegrationOptions(), _store, _httpClient, NullLogger.Instance, _time);

    private async Task<GitHubIntegration> CreateAuthenticatedAsync()
    {
        GitHubIntegration github = Create();
        await github.Credentials.SaveAsync("github_pat_test", CancellationToken);
        return github;
    }
}
