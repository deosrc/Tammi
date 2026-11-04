using Tammi.Core.Integrations;
using Tammi.Core.Integrations.Jira;
using Tammi.Core.Tests.Credentials;

namespace Tammi.Core.Tests.Integrations.Jira;

/// <summary>
/// Checks the integration against a real JIRA site. They only run when <c>TAMMI_JIRA_URL</c> and
/// <c>TAMMI_JIRA_TOKEN</c> are set. For JIRA Cloud, also set <c>TAMMI_JIRA_EMAIL</c>; without it the site is treated
/// as Server or Data Center and the token as a personal access token. They only read.
/// </summary>
public sealed class JiraIntegrationLiveTests : IDisposable
{
    private const string UrlVariable = "TAMMI_JIRA_URL";
    private const string EmailVariable = "TAMMI_JIRA_EMAIL";
    private const string TokenVariable = "TAMMI_JIRA_TOKEN";

    private readonly HttpClient _httpClient = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _httpClient.Dispose();

    [Fact]
    public async Task ReadsTheUserAndTheirTasks()
    {
        JiraIntegration jira = await CreateAsync();

        JiraUser user = await jira.GetAuthenticatedUserAsync(CancellationToken);
        IReadOnlyList<TaskInfo> assigned = await jira.GetAssignedTasksAsync(CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"{user.DisplayName}: {assigned.Count} issues to do");
        Assert.False(string.IsNullOrEmpty(user.DisplayName));
        Assert.Equal(assigned.Count, assigned.DistinctBy(task => task.Key).Count());
    }

    [Fact]
    public async Task FindsAnIssueBySearchKeyAndUrl()
    {
        JiraIntegration jira = await CreateAsync();
        IReadOnlyList<TaskInfo> recent = await jira.SearchTasksAsync("the", CancellationToken);
        if (recent.Count == 0)
        {
            Assert.Skip("The user can't see any issue that mentions 'the'.");
        }

        TaskInfo found = recent[0];
        TaskInfo? byKey = await jira.GetTaskAsync(found.Key, CancellationToken);
        Assert.True(jira.TryParseTaskReference(found.Url.AbsoluteUri, out string? keyFromUrl));

        Assert.Equal(found, byKey);
        Assert.Equal(found.Key, keyFromUrl);
        Assert.Null(await jira.GetTaskAsync(found.Key.Split('-')[0] + "-999999999", CancellationToken));
    }

    private async Task<JiraIntegration> CreateAsync()
    {
        string? url = Environment.GetEnvironmentVariable(UrlVariable);
        string? token = Environment.GetEnvironmentVariable(TokenVariable);
        string? email = Environment.GetEnvironmentVariable(EmailVariable);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
        {
            Assert.Skip($"Set {UrlVariable} and {TokenVariable} (and {EmailVariable} for JIRA Cloud) to check the "
                + "integration against a JIRA site.");
        }

        var jira = new JiraIntegration(
            new JiraIntegrationOptions
            {
                Id = "jira-live",
                SiteUrl = new Uri(url),
                Deployment = string.IsNullOrWhiteSpace(email) ? JiraDeployment.DataCenter : JiraDeployment.Cloud,
                Email = email,
            },
            new InMemoryCredentialStore(),
            _httpClient,
            TimeProvider.System);
        await jira.Credentials.SaveAsync(token, CancellationToken);
        return jira;
    }
}
