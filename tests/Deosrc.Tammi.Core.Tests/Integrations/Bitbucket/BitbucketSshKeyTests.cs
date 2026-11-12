using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.Bitbucket;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.Bitbucket;

public sealed class BitbucketSshKeyTests : IDisposable
{
    private const string Key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIC/6GX6scN2SURHcbmVqb+8JHF2L4wHY7OOkS1KoNAYz";
    private const string AddPath = "users/%7Bc5a0a8a2-0000-4000-8000-000000000001%7D/ssh-keys";
    private const string KeysPath = AddPath + "?pagelen=100";

    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeBitbucketApi _api = new();
    private readonly HttpClient _httpClient;

    public BitbucketSshKeyTests()
    {
        _httpClient = new HttpClient(_api);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _httpClient.Dispose();

    [Fact]
    public async Task HostAndPage_AreBitbucketCloud()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();

        Assert.Equal("bitbucket.org", bitbucket.SshHost);
        Assert.Equal("bitbucket.org", bitbucket.NewSshKeyUrl.Host);
    }

    [Fact]
    public async Task GetSshKeysAsync_ReturnsTheKeys_FromEveryPage()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(KeysPath, $$"""
            {"values":[{"key":"{{Key}}","label":"laptop"}],
             "next":"https://api.bitbucket.org/2.0/{{AddPath}}?pagelen=100&page=2"}
            """)
            .Respond(AddPath + "?pagelen=100&page=2", """{"values":[{"key":"ssh-rsa AAAAB3NzaC1yc2E="}]}""");

        IReadOnlyList<string>? keys = await bitbucket.GetSshKeysAsync(CancellationToken);

        Assert.Equal([Key, "ssh-rsa AAAAB3NzaC1yc2E="], keys);
    }

    [Fact]
    public async Task GetSshKeysAsync_TokenLacksTheScope_ReturnsNull()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(KeysPath, FakeBitbucketApi.Response("forbidden.json"), HttpStatusCode.Forbidden);

        Assert.Null(await bitbucket.GetSshKeysAsync(CancellationToken));
    }

    [Fact]
    public async Task GetSshKeysAsync_TokenRejected_ThrowsAuthenticationException()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(KeysPath, "{}", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(() => bitbucket.GetSshKeysAsync(CancellationToken));
    }

    [Fact]
    public async Task AddSshKeyAsync_PostsTheLabelAndKey_ToTheUsersKeys()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(AddPath, $$"""{"key":"{{Key}}","label":"laptop"}""", HttpStatusCode.Created);

        await bitbucket.AddSshKeyAsync(" laptop ", Key + " mia@example.com ", CancellationToken);

        RecordedRequest request = _api.Requests.Last();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.bitbucket.org/2.0/" + AddPath), request.Url);
        JsonNode body = JsonNode.Parse(request.Body!)!;
        Assert.Equal("laptop", (string?)body["label"]);
        Assert.Equal(Key + " mia@example.com", (string?)body["key"]);
    }

    [Fact]
    public async Task AddSshKeyAsync_TokenLacksTheScope_ThrowsPermissionExceptionNamingIt()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(AddPath, FakeBitbucketApi.Response("forbidden.json"), HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<IntegrationPermissionException>(
            () => bitbucket.AddSshKeyAsync("laptop", Key, CancellationToken));

        Assert.Equal("bitbucket", exception.IntegrationId);
        Assert.Contains("write:ssh-key:bitbucket", exception.Message);
        Assert.Contains("read:ssh-key:bitbucket", exception.Message);
    }

    [Fact]
    public async Task AddSshKeyAsync_KeyAlreadyOnTheAccount_Succeeds()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(AddPath, """{"type":"error","error":{"message":"Someone has already added that key."}}""",
                HttpStatusCode.BadRequest)
            .Respond(KeysPath, $$"""{"values":[{"key":"{{Key}} other-comment"}]}""");

        await bitbucket.AddSshKeyAsync("laptop", Key + " mia@example.com", CancellationToken);
    }

    [Fact]
    public async Task AddSshKeyAsync_KeyInUseElsewhere_ThrowsWithBitbucketsMessage()
    {
        BitbucketIntegration bitbucket = await CreateAuthenticatedAsync();
        _api.Respond("user", FakeBitbucketApi.Response("user.json"))
            .Respond(AddPath, """{"type":"error","error":{"message":"Someone has already added that key."}}""",
                HttpStatusCode.BadRequest)
            .Respond(KeysPath, """{"values":[]}""");

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => bitbucket.AddSshKeyAsync("laptop", Key, CancellationToken));

        Assert.Contains("already added", exception.Message);
    }

    private async Task<BitbucketIntegration> CreateAuthenticatedAsync()
    {
        var bitbucket = new BitbucketIntegration(
            new BitbucketIntegrationOptions { Email = "mia@example.com" }, _store, _httpClient,
            NullLogger.Instance, new FakeTimeProvider());
        await bitbucket.Credentials.SaveAsync("ATATT3xFfGF0test", CancellationToken);
        return bitbucket;
    }
}
