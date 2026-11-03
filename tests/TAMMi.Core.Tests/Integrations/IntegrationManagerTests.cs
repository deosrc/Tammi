using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using TAMMi.Core.Credentials;
using TAMMi.Core.Integrations;
using TAMMi.Core.Integrations.GitHub;
using TAMMi.Core.Integrations.Jira;
using TAMMi.Core.Settings;
using TAMMi.Core.Tests.Credentials;
using TAMMi.Core.Tests.Integrations.GitHub;
using TAMMi.Core.Tests.Integrations.Jira;

namespace TAMMi.Core.Tests.Integrations;

public sealed class IntegrationManagerTests : IDisposable
{
    private const string Token = "token-1";
    private const string NewToken = "token-2";
    private const string Email = "mia@example.com";
    private const string GitHubKey = "integration:github";
    private const string JiraId = "jira-acme.atlassian.net";
    private const string JiraKey = "integration:" + JiraId;

    private static readonly GitHubIntegrationSettings GitHubCom =
        GitHubIntegrationSettings.For(GitHubIntegrationOptions.GitHubComApiUrl);

    private static readonly JiraIntegrationSettings AcmeJira =
        JiraIntegrationSettings.For(new Uri(FakeJiraApi.CloudSiteUrl), JiraDeployment.Cloud, Email);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryCredentialStore _credentials = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<HttpClient> _httpClients = [];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (HttpClient client in _httpClients)
        {
            client.Dispose();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Constructor_AddsTheConfiguredIntegrationsToTheRegistry()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi(), await WithGitHubAndJira());

        Assert.Collection(
            manager.Registry.All,
            github => Assert.Equal(("github", "GitHub"), (github.Id, github.Name)),
            jira => Assert.Equal((JiraId, "JIRA (acme.atlassian.net)"), (jira.Id, jira.Name)));
        Assert.All(manager.Registry.All, integration =>
            Assert.Equal(IntegrationStatus.NotChecked, manager.GetStatus(integration.Id)));
    }

    [Fact]
    public async Task AddGitHubAsync_ChecksTheToken_ThenSavesTheIntegrationAndToken()
    {
        var api = new FakeGitHubApi().Respond("user", FakeGitHubApi.Recorded("user.json"));
        SettingsStore settings = CreateSettingsStore();
        IntegrationManager manager = CreateManager(api, settings);
        int changes = 0;
        manager.Changed += () => changes++;

        IIntegration integration = await manager.AddGitHubAsync(GitHubCom, $" {Token} ", CancellationToken);

        Assert.Equal($"Bearer {Token}", Assert.Single(api.Requests).Authorization);
        Assert.Equal(Token, _credentials.Secrets[GitHubKey]);
        Assert.Same(integration, Assert.Single(manager.Registry.All));
        Assert.Equal(GitHubCom, Assert.Single(CreateSettingsStore().Current.Integrations.GitHub));
        Assert.Equal(
            new IntegrationStatus(IntegrationConnectionState.Connected, "deosrc"),
            manager.GetStatus("github"));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task AddGitHubAsync_WithARejectedToken_SavesNothing()
    {
        var api = new FakeGitHubApi()
            .Respond("user", """{"message":"Bad credentials"}""", HttpStatusCode.Unauthorized);
        SettingsStore settings = CreateSettingsStore();
        IntegrationManager manager = CreateManager(api, settings);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => manager.AddGitHubAsync(GitHubCom, Token, CancellationToken));

        Assert.Empty(_credentials.Secrets);
        Assert.Empty(manager.Registry.All);
        Assert.Empty(settings.Current.Integrations.GitHub);
    }

    [Fact]
    public async Task AddGitHubAsync_WhenGitHubCantBeReached_SavesNothing()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi().Fail("user"));

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => manager.AddGitHubAsync(GitHubCom, Token, CancellationToken));

        Assert.Contains("couldn't be reached", exception.Message);
        Assert.Empty(_credentials.Secrets);
        Assert.Empty(manager.Registry.All);
    }

    [Fact]
    public async Task AddGitHubAsync_ForAnIntegrationAlreadyConnected_Fails()
    {
        var api = new FakeGitHubApi().Respond("user", FakeGitHubApi.Recorded("user.json"));
        IntegrationManager manager = CreateManager(api);
        await manager.AddGitHubAsync(GitHubCom, Token, CancellationToken);

        var exception = await Assert.ThrowsAsync<IntegrationException>(
            () => manager.AddGitHubAsync(GitHubCom, NewToken, CancellationToken));

        Assert.Equal("GitHub is already connected.", exception.Message);
        Assert.Single(api.Requests);
        Assert.Equal(Token, _credentials.Secrets[GitHubKey]);
    }

    [Fact]
    public async Task AddGitHubAsync_WithInvalidSettings_Throws()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi());

        await Assert.ThrowsAsync<ArgumentException>(
            () => manager.AddGitHubAsync(GitHubCom with { Id = "" }, Token, CancellationToken));
    }

    [Fact]
    public async Task AddJiraAsync_ChecksTheTokenWithTheEmailAddress_ThenSavesIt()
    {
        var api = new FakeJiraApi().Respond("myself", FakeJiraApi.Response("myself.json"));
        IntegrationManager manager = CreateManager(api);

        await manager.AddJiraAsync(AcmeJira, Token, CancellationToken);

        Assert.Equal(BasicAuthorization(Email, Token), Assert.Single(api.Requests).Authorization);
        Assert.Equal(Token, _credentials.Secrets[JiraKey]);
        Assert.IsType<JiraIntegration>(Assert.Single(manager.Registry.All));
        Assert.Equal(AcmeJira, Assert.Single(CreateSettingsStore().Current.Integrations.Jira));
        Assert.Equal("Mia Krystof (mia@example.com)", manager.GetStatus(JiraId).Account);
    }

    [Fact]
    public async Task AddJiraAsync_ForCloudWithoutAnEmailAddress_Throws()
    {
        IntegrationManager manager = CreateManager(new FakeJiraApi());

        await Assert.ThrowsAsync<ArgumentException>(
            () => manager.AddJiraAsync(AcmeJira with { Email = null }, Token, CancellationToken));
    }

    [Fact]
    public async Task CheckAsync_WithoutAToken_IsNotAuthenticated()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi(), await WithGitHub());

        IntegrationStatus status = await manager.CheckAsync("github", CancellationToken);

        Assert.Equal(IntegrationConnectionState.NotAuthenticated, status.State);
        Assert.Equal(status, manager.GetStatus("github"));
    }

    [Fact]
    public async Task CheckAsync_WithAnAcceptedToken_IsConnected()
    {
        var api = new FakeGitHubApi().Respond("user", FakeGitHubApi.Recorded("user.json"));
        IntegrationManager manager = CreateManager(api, await WithGitHub());
        await _credentials.SaveAsync(GitHubKey, Token, CancellationToken);
        var states = new List<IntegrationConnectionState>();
        manager.Changed += () => states.Add(manager.GetStatus("github").State);

        IntegrationStatus status = await manager.CheckAsync("github", CancellationToken);

        Assert.Equal(new IntegrationStatus(IntegrationConnectionState.Connected, "deosrc"), status);
        Assert.Equal([IntegrationConnectionState.Checking, IntegrationConnectionState.Connected], states);
    }

    [Fact]
    public async Task CheckAsync_WithARejectedToken_IsAuthenticationFailed()
    {
        var api = new FakeGitHubApi()
            .Respond("user", """{"message":"Bad credentials"}""", HttpStatusCode.Unauthorized);
        IntegrationManager manager = CreateManager(api, await WithGitHub());
        await _credentials.SaveAsync(GitHubKey, Token, CancellationToken);

        IntegrationStatus status = await manager.CheckAsync("github", CancellationToken);

        Assert.Equal(IntegrationConnectionState.AuthenticationFailed, status.State);
        Assert.NotNull(status.Message);
    }

    [Fact]
    public async Task CheckAsync_WhenTheServiceCantBeReached_IsFailed()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi().Fail("user"), await WithGitHub());
        await _credentials.SaveAsync(GitHubKey, Token, CancellationToken);

        IntegrationStatus status = await manager.CheckAsync("github", CancellationToken);

        Assert.Equal(IntegrationConnectionState.Failed, status.State);
        Assert.Contains("No such host is known.", status.Message);
    }

    [Fact]
    public async Task CheckAsync_WithoutACredentialStore_IsFailedWithTheStoresMessage()
    {
        IntegrationManager manager = CreateManager(
            new FakeGitHubApi(), await WithGitHub(), new UnavailableCredentialStore());

        IntegrationStatus status = await manager.CheckAsync("github", CancellationToken);

        Assert.Equal(new IntegrationStatus(IntegrationConnectionState.Failed, Message: UnavailableCredentialStore.Message),
            status);
    }

    [Fact]
    public async Task CheckAllAsync_ChecksEveryIntegration()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi(), await WithGitHubAndJira());

        await manager.CheckAllAsync(CancellationToken);

        Assert.Equal(IntegrationConnectionState.NotAuthenticated, manager.GetStatus("github").State);
        Assert.Equal(IntegrationConnectionState.NotAuthenticated, manager.GetStatus(JiraId).State);
    }

    [Fact]
    public async Task CheckAsync_ForAnUnknownIntegration_Throws()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi());

        await Assert.ThrowsAsync<ArgumentException>(() => manager.CheckAsync("github", CancellationToken));
    }

    [Fact]
    public async Task AuthenticateAsync_ChecksTheNewToken_ThenReplacesTheOldOne()
    {
        var api = new FakeGitHubApi().Respond("user", FakeGitHubApi.Recorded("user.json"));
        IntegrationManager manager = CreateManager(api, await WithGitHub());
        await _credentials.SaveAsync(GitHubKey, Token, CancellationToken);

        await manager.AuthenticateAsync("github", NewToken, cancellationToken: CancellationToken);

        Assert.Equal($"Bearer {NewToken}", Assert.Single(api.Requests).Authorization);
        Assert.Equal(NewToken, _credentials.Secrets[GitHubKey]);
        Assert.Equal(IntegrationConnectionState.Connected, manager.GetStatus("github").State);
    }

    [Fact]
    public async Task AuthenticateAsync_WithARejectedToken_KeepsTheOldOne()
    {
        var api = new FakeGitHubApi()
            .Respond("user", """{"message":"Bad credentials"}""", HttpStatusCode.Unauthorized);
        IntegrationManager manager = CreateManager(api, await WithGitHub());
        await _credentials.SaveAsync(GitHubKey, Token, CancellationToken);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => manager.AuthenticateAsync("github", NewToken, cancellationToken: CancellationToken));

        Assert.Equal(Token, _credentials.Secrets[GitHubKey]);
    }

    [Fact]
    public async Task AuthenticateAsync_WithANewEmailAddress_UpdatesTheJiraSite()
    {
        const string newEmail = "mia@acme.example";
        var api = new FakeJiraApi().Respond("myself", FakeJiraApi.Response("myself.json"));
        SettingsStore settings = await WithJira();
        IntegrationManager manager = CreateManager(api, settings);

        await manager.AuthenticateAsync(JiraId, NewToken, newEmail, CancellationToken);
        await manager.CheckAsync(JiraId, CancellationToken);

        Assert.All(api.Requests, request => Assert.Equal(BasicAuthorization(newEmail, NewToken), request.Authorization));
        Assert.Equal(newEmail, Assert.Single(CreateSettingsStore().Current.Integrations.Jira).Email);
        Assert.Single(manager.Registry.All);
    }

    [Fact]
    public async Task RemoveAsync_RemovesTheIntegrationAndItsToken()
    {
        var api = new FakeGitHubApi().Respond("user", FakeGitHubApi.Recorded("user.json"));
        IntegrationManager manager = CreateManager(api, await WithGitHubAndJira());
        await _credentials.SaveAsync(GitHubKey, Token, CancellationToken);
        await _credentials.SaveAsync(JiraKey, Token, CancellationToken);
        await manager.CheckAsync("github", CancellationToken);
        int changes = 0;
        manager.Changed += () => changes++;

        bool removed = await manager.RemoveAsync("github", CancellationToken);

        Assert.True(removed);
        Assert.Equal([JiraKey], _credentials.Secrets.Keys);
        Assert.Equal(JiraId, Assert.Single(manager.Registry.All).Id);
        IntegrationsSettings saved = CreateSettingsStore().Current.Integrations;
        Assert.Empty(saved.GitHub);
        Assert.Single(saved.Jira);
        Assert.Equal(IntegrationStatus.NotChecked, manager.GetStatus("github"));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task RemoveAsync_WithoutACredentialStore_StillRemovesTheIntegration()
    {
        SettingsStore settings = await WithGitHub();
        IntegrationManager manager = CreateManager(new FakeGitHubApi(), settings, new UnavailableCredentialStore());

        Assert.True(await manager.RemoveAsync("github", CancellationToken));

        Assert.Empty(manager.Registry.All);
        Assert.Empty(settings.Current.Integrations.GitHub);
    }

    [Fact]
    public async Task RemoveAsync_ForAnUnknownIntegration_ReturnsFalse()
    {
        IntegrationManager manager = CreateManager(new FakeGitHubApi());

        Assert.False(await manager.RemoveAsync("github", CancellationToken));
    }

    [Fact]
    public async Task AnIntegrationAddedAgain_KeepsItsId()
    {
        var api = new FakeGitHubApi().Respond("user", FakeGitHubApi.Recorded("user.json"));
        IntegrationManager manager = CreateManager(api);
        IIntegration first = await manager.AddGitHubAsync(GitHubCom, Token, CancellationToken);
        await manager.RemoveAsync(first.Id, CancellationToken);

        IIntegration second = await manager.AddGitHubAsync(
            GitHubIntegrationSettings.For(GitHubIntegrationSettings.ParseApiUrl("github.com")!),
            Token,
            CancellationToken);

        Assert.Equal(first.Id, second.Id);
    }

    private static string BasicAuthorization(string email, string token) =>
        $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"))}";

    private SettingsStore CreateSettingsStore() => new(_directory, []);

    private Task<SettingsStore> WithGitHub() => With(new IntegrationsSettings { GitHub = [GitHubCom] });

    private Task<SettingsStore> WithJira() => With(new IntegrationsSettings { Jira = [AcmeJira] });

    private Task<SettingsStore> WithGitHubAndJira() =>
        With(new IntegrationsSettings { GitHub = [GitHubCom], Jira = [AcmeJira] });

    private async Task<SettingsStore> With(IntegrationsSettings integrations)
    {
        SettingsStore store = CreateSettingsStore();
        await store.UpdateAsync(settings => settings with { Integrations = integrations }, CancellationToken);
        return store;
    }

    private IntegrationManager CreateManager(
        FakeHttpApi api,
        SettingsStore? settings = null,
        ICredentialStore? credentials = null)
    {
        var httpClient = new HttpClient(api);
        _httpClients.Add(httpClient);
        return new IntegrationManager(
            settings ?? CreateSettingsStore(),
            credentials ?? _credentials,
            httpClient,
            _time);
    }

    private sealed class UnavailableCredentialStore : ICredentialStore
    {
        public const string Message = "No Secret Service is running.";

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            throw new CredentialStoreUnavailableException(Message);

        public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default) =>
            throw new CredentialStoreUnavailableException(Message);

        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new CredentialStoreUnavailableException(Message);
    }
}
