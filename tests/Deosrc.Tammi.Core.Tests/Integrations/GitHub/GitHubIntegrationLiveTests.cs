using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Integrations.GitHub;
using Deosrc.Tammi.Core.Tests.Credentials;

namespace Deosrc.Tammi.Core.Tests.Integrations.GitHub;

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
        IReadOnlyList<TaskInfo> assigned = await github.GetAssignedTasksAsync(CancellationToken);

        TestContext.Current.SendDiagnosticMessage(
            $"{user.Login}: {repositories.Count} repositories, {assigned.Count} assigned issues");
        Assert.False(string.IsNullOrEmpty(user.Login));
        Assert.All(repositories, repository => Assert.Equal(2, repository.CloneUrls.Count));
        Assert.Equal(repositories.Count, repositories.DistinctBy(repository => repository.FullName).Count());
    }

    [Fact]
    public async Task SearchesTheUsersOwnIssues_AndFindsAPublicIssueByKey()
    {
        GitHubIntegration github = await CreateAsync();

        // The search runs, scoped to the user's repositories, whether or not it finds anything.
        IReadOnlyList<TaskInfo> found = await github.SearchTasksAsync("the", CancellationToken);
        TaskInfo? byKey = await github.GetTaskAsync("deosrc/Condement#3", CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"{found.Count} issues found by text");
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
