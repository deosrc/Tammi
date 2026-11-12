using Microsoft.Extensions.Logging.Abstractions;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.Bitbucket;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.Bitbucket;

/// <summary>
/// Checks the integration against a real Bitbucket Cloud account. They only run when <c>TAMMI_BITBUCKET_EMAIL</c> and
/// <c>TAMMI_BITBUCKET_TOKEN</c> (an API token with the scopes in <see cref="BitbucketIntegration.RequiredScopes"/>)
/// are set. They only read.
/// </summary>
public sealed class BitbucketIntegrationLiveTests : IDisposable
{
    private const string EmailVariable = "TAMMI_BITBUCKET_EMAIL";
    private const string TokenVariable = "TAMMI_BITBUCKET_TOKEN";

    private readonly HttpClient _httpClient = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _httpClient.Dispose();

    [Fact]
    public async Task ReadsTheUserTheirRepositoriesAndTasks()
    {
        BitbucketIntegration bitbucket = await CreateAsync();

        string user = await bitbucket.GetAuthenticatedUserAsync(CancellationToken);
        IReadOnlyList<IntegrationRepository> repositories = await bitbucket.GetRepositoriesAsync(CancellationToken);
        IReadOnlyList<TaskInfo> assigned = await bitbucket.GetAssignedTasksAsync(CancellationToken);

        TestContext.Current.SendDiagnosticMessage(
            $"{user}: {repositories.Count} repositories, {assigned.Count} issues to do");
        Assert.False(string.IsNullOrEmpty(user));
        Assert.All(repositories, repository => Assert.Equal(2, repository.CloneUrls.Count));
        Assert.Equal(assigned.Count, assigned.DistinctBy(task => task.Key).Count());
    }

    [Fact]
    public async Task FindsAnIssueBySearchKeyAndUrl()
    {
        BitbucketIntegration bitbucket = await CreateAsync();
        IReadOnlyList<TaskInfo> found = await bitbucket.SearchTasksAsync("the", CancellationToken);
        if (found.Count == 0)
        {
            Assert.Skip("The user can't see any open issue that mentions 'the'.");
        }

        TaskInfo task = found[0];
        TaskInfo? byKey = await bitbucket.GetTaskAsync(task.Key, CancellationToken);
        Assert.True(bitbucket.TryParseTaskReference(task.Url.AbsoluteUri, out string? keyFromUrl));

        Assert.Equal(task.Key, byKey?.Key);
        Assert.Equal(task.Key, keyFromUrl);
    }

    private async Task<BitbucketIntegration> CreateAsync()
    {
        string? email = Environment.GetEnvironmentVariable(EmailVariable);
        string? token = Environment.GetEnvironmentVariable(TokenVariable);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            Assert.Skip($"Set {EmailVariable} and {TokenVariable} to check the integration against Bitbucket Cloud.");
        }

        var bitbucket = new BitbucketIntegration(
            new BitbucketIntegrationOptions { Email = email },
            new InMemoryCredentialStore(),
            _httpClient,
            NullLogger.Instance,
            TimeProvider.System);
        await bitbucket.Credentials.SaveAsync(token, CancellationToken);
        return bitbucket;
    }
}
