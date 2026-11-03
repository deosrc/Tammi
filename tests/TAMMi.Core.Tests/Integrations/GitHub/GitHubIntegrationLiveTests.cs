using TAMMi.Core.Integrations;
using TAMMi.Core.Integrations.GitHub;
using TAMMi.Core.Tests.Credentials;

namespace TAMMi.Core.Tests.Integrations.GitHub;

/// <summary>
/// Checks the integration against the real GitHub API. They only run when <c>TAMMI_GITHUB_TOKEN</c> holds an access
/// token, e.g. <c>TAMMI_GITHUB_TOKEN=$(gh auth token) dotnet test</c>. They only read.
/// </summary>
public sealed class GitHubIntegrationLiveTests : IDisposable
{
    private const string TokenVariable = "TAMMI_GITHUB_TOKEN";

    private readonly HttpClient _httpClient = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _httpClient.Dispose();

    [Fact]
    public async Task ReadsTheUserTasksAndRepositories()
    {
        GitHubIntegration github = await CreateAsync();

        GitHubUser user = await github.GetAuthenticatedUserAsync(CancellationToken);
        IReadOnlyList<IntegrationRepository> repositories = await github.GetRepositoriesAsync(CancellationToken);
        IReadOnlyList<TaskInfo> assigned = await github.SearchTasksAsync(null, CancellationToken);

        TestContext.Current.SendDiagnosticMessage(
            $"{user.Login}: {repositories.Count} repositories, {assigned.Count} assigned issues");
        Assert.False(string.IsNullOrEmpty(user.Login));
        Assert.All(repositories, repository => Assert.Equal(2, repository.CloneUrls.Count));
        Assert.Equal(repositories.Count, repositories.DistinctBy(repository => repository.FullName).Count());
    }

    [Fact]
    public async Task FindsAPublicIssueBySearchAndByKey()
    {
        GitHubIntegration github = await CreateAsync();

        IReadOnlyList<TaskInfo> found = await github.SearchTasksAsync(
            "repo:deosrc/Condement \"Add Remote Desktop support\"", CancellationToken);
        TaskInfo? byKey = await github.GetTaskAsync("deosrc/Condement#3", CancellationToken);

        Assert.Contains(found, task => task.Key == "deosrc/Condement#3");
        Assert.Equal("Add Remote Desktop support", byKey?.Title);
        Assert.Null(await github.GetTaskAsync("deosrc/Condement#15", CancellationToken));
    }

    private async Task<GitHubIntegration> CreateAsync()
    {
        string? token = Environment.GetEnvironmentVariable(TokenVariable);
        if (string.IsNullOrWhiteSpace(token))
        {
            Assert.Skip($"Set {TokenVariable} to an access token to check the integration against GitHub.");
        }

        var github = new GitHubIntegration(
            new GitHubIntegrationOptions(), new InMemoryCredentialStore(), _httpClient, TimeProvider.System);
        await github.Credentials.SaveAsync(token, CancellationToken);
        return github;
    }
}
