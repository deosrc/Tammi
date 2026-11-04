using TAMMi.Core.Integrations;
using TAMMi.Core.Tests.Credentials;

namespace TAMMi.Core.Tests.Integrations;

public class IntegrationCredentialsTests
{
    private readonly InMemoryCredentialStore _store = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachIntegration_HasItsOwnSecret()
    {
        var github = new IntegrationCredentials(_store, "github");
        var jira = new IntegrationCredentials(_store, "jira");

        await github.SaveAsync("github-token", CancellationToken);
        await jira.SaveAsync("jira-token", CancellationToken);

        Assert.Equal("github-token", await github.GetAsync(CancellationToken));
        Assert.Equal("jira-token", await jira.GetAsync(CancellationToken));
        Assert.Equal("github-token", _store.Secrets["integration:github"]);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyThatIntegrationsSecret()
    {
        var github = new IntegrationCredentials(_store, "github");
        var jira = new IntegrationCredentials(_store, "jira");
        await github.SaveAsync("github-token", CancellationToken);
        await jira.SaveAsync("jira-token", CancellationToken);

        Assert.True(await github.DeleteAsync(CancellationToken));
        Assert.False(await github.DeleteAsync(CancellationToken));

        Assert.Null(await github.GetAsync(CancellationToken));
        Assert.Equal("jira-token", await jira.GetAsync(CancellationToken));
    }

    [Fact]
    public async Task GetRequiredAsync_ThrowsAuthenticationException_WhenNothingIsSaved()
    {
        var credentials = new IntegrationCredentials(_store, "github");

        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => credentials.GetRequiredAsync(CancellationToken));

        Assert.Equal("github", exception.IntegrationId);
    }

    [Fact]
    public void Constructor_Throws_ForBlankId()
    {
        Assert.ThrowsAny<ArgumentException>(() => new IntegrationCredentials(_store, " "));
    }
}
