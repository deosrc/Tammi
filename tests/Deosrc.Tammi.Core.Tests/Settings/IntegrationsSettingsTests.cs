using System.Text.Json.Nodes;
using Tammi.Core.Integrations.GitHub;
using Tammi.Core.Integrations.Jira;
using Tammi.Core.Settings;

namespace Tammi.Core.Tests.Settings;

public sealed class IntegrationsSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("github.com", "https://api.github.com/")]
    [InlineData("https://github.com/acme/api", "https://api.github.com/")]
    [InlineData("https://api.github.com", "https://api.github.com/")]
    [InlineData("github.example.com", "https://github.example.com/api/v3/")]
    [InlineData(" https://github.example.com/ ", "https://github.example.com/api/v3/")]
    [InlineData("https://github.example.com/api/v3", "https://github.example.com/api/v3/")]
    [InlineData("http://github.example.com:8080/api/v3/?x=1", "http://github.example.com:8080/api/v3/")]
    public void GitHubParseApiUrl_AcceptsServerAndApiUrls(string input, string expected)
    {
        Assert.Equal(new Uri(expected), GitHubIntegrationSettings.ParseApiUrl(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ftp://github.example.com")]
    [InlineData("https://")]
    public void GitHubParseApiUrl_RejectsOtherInput(string? input)
    {
        Assert.Null(GitHubIntegrationSettings.ParseApiUrl(input));
    }

    [Fact]
    public void GitHubFor_GivesIdsAndNames()
    {
        GitHubIntegrationSettings github = GitHubIntegrationSettings.For(GitHubIntegrationOptions.GitHubComApiUrl);
        GitHubIntegrationSettings enterprise =
            GitHubIntegrationSettings.For(new Uri("https://GitHub.Example.com:8443/api/v3"));

        Assert.Equal(("github", "GitHub"), (github.Id, github.Name));
        Assert.Equal(
            ("github-github.example.com-8443", "GitHub (github.example.com:8443)", "https://github.example.com:8443/api/v3/"),
            (enterprise.Id, enterprise.Name, enterprise.ApiUrl.AbsoluteUri));
        Assert.Equal(new Uri("https://github.example.com:8443/api/v3/"), enterprise.ToOptions().ApiUrl);
    }

    [Theory]
    [InlineData("acme.atlassian.net", "https://acme.atlassian.net/")]
    [InlineData("https://acme.atlassian.net/browse/ABC-1?focused=1", "https://acme.atlassian.net/")]
    [InlineData("https://jira.example.com", "https://jira.example.com/")]
    [InlineData("https://jira.example.com/jira/", "https://jira.example.com/jira/")]
    public void JiraParseSiteUrl_AcceptsSiteUrls(string input, string expected)
    {
        Assert.Equal(new Uri(expected), JiraIntegrationSettings.ParseSiteUrl(input));
    }

    [Theory]
    [InlineData("https://acme.atlassian.net/", JiraDeployment.Cloud)]
    [InlineData("https://jira.example.com/", JiraDeployment.DataCenter)]
    public void JiraGuessDeployment_RecognisesCloudSites(string siteUrl, JiraDeployment expected)
    {
        Assert.Equal(expected, JiraIntegrationSettings.GuessDeployment(new Uri(siteUrl)));
    }

    [Fact]
    public void JiraFor_GivesIdsAndNames()
    {
        JiraIntegrationSettings cloud = JiraIntegrationSettings.For(
            new Uri("https://acme.atlassian.net/"), JiraDeployment.Cloud, " mia@example.com ");
        JiraIntegrationSettings dataCenter = JiraIntegrationSettings.For(
            new Uri("https://jira.example.com/jira/"), JiraDeployment.DataCenter, "ignored@example.com");

        Assert.Equal(
            ("jira-acme.atlassian.net", "JIRA (acme.atlassian.net)", "mia@example.com"),
            (cloud.Id, cloud.Name, cloud.Email));
        Assert.Equal(
            ("jira-jira.example.com-jira", "JIRA (jira.example.com/jira)", (string?)null),
            (dataCenter.Id, dataCenter.Name, dataCenter.Email));
    }

    [Fact]
    public void Normalize_DropsInvalidAndDuplicateIntegrations()
    {
        GitHubIntegrationSettings github = GitHubIntegrationSettings.For(GitHubIntegrationOptions.GitHubComApiUrl);
        JiraIntegrationSettings jira = JiraIntegrationSettings.For(
            new Uri("https://acme.atlassian.net/"), JiraDeployment.Cloud, "mia@example.com");

        IntegrationsSettings normalized = new IntegrationsSettings
        {
            GitHub = [github, github with { ApiUrl = new Uri("https://example.com/") }, github with { Id = " " }, null!],
            Jira =
            [
                jira,
                jira with { Id = "github" },
                jira with { Id = "jira-2", Email = null },
                jira with { Id = "jira-3", SiteUrl = null },
                jira with { Id = "jira-4", Deployment = JiraDeployment.DataCenter, Email = null },
            ],
        }.Normalize();

        Assert.Equal([github], normalized.GitHub);
        Assert.Equal(["jira-acme.atlassian.net", "jira-4"], normalized.Jira.Select(j => j.Id));
    }

    [Fact]
    public void Equals_ComparesTheIntegrations()
    {
        GitHubIntegrationSettings github = GitHubIntegrationSettings.For(GitHubIntegrationOptions.GitHubComApiUrl);

        Assert.Equal(new IntegrationsSettings { GitHub = [github] }, new IntegrationsSettings { GitHub = [github] });
        Assert.NotEqual(new IntegrationsSettings { GitHub = [github] }, new IntegrationsSettings());
        Assert.Equal(new AppSettings(), new AppSettings().Normalize());
    }

    [Fact]
    public async Task Integrations_AreSavedAndLoaded_WithoutTheirNames()
    {
        var store = new SettingsStore(_directory, []);
        GitHubIntegrationSettings github = GitHubIntegrationSettings.For(new Uri("https://github.example.com/api/v3/"));
        JiraIntegrationSettings jira = JiraIntegrationSettings.For(
            new Uri("https://jira.example.com/"), JiraDeployment.DataCenter, null);

        await store.UpdateAsync(
            settings => settings with { Integrations = new IntegrationsSettings { GitHub = [github], Jira = [jira] } },
            TestContext.Current.CancellationToken);

        Assert.Equal(new IntegrationsSettings { GitHub = [github], Jira = [jira] },
            new SettingsStore(_directory, []).Current.Integrations);
        var json = JsonNode.Parse(File.ReadAllText(store.FilePath))!["integrations"]!;
        Assert.Equal("DataCenter", json["jira"]![0]!["deployment"]!.GetValue<string>());
        Assert.Null(json["jira"]![0]!["name"]);
        Assert.Null(json["gitHub"]![0]!["name"]);
        Assert.Null(json["gitHub"]![0]!["isGitHubCom"]);
    }
}
