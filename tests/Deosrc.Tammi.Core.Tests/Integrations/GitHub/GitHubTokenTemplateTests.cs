using System.Web;
using Deosrc.Tammi.Core.Integrations.GitHub;

namespace Deosrc.Tammi.Core.Tests.Integrations.GitHub;

public sealed class GitHubTokenTemplateTests
{
    [Fact]
    public void GitHubComUrl_OpensTheNewFineGrainedTokenPage()
    {
        Uri url = GitHubTokenTemplate.GitHubComUrl;

        Assert.Equal("https://github.com/settings/personal-access-tokens/new", url.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public void NewTokenUrl_FillsInNameDescriptionAndReadOnlyPermissions()
    {
        var query = HttpUtility.ParseQueryString(GitHubTokenTemplate.GitHubComUrl.Query);

        Assert.Equal("Tammi", query["name"]);
        Assert.Equal("Lets Tammi read your issues to build workspaces.", query["description"]);
        Assert.Equal("read", query["issues"]);
        Assert.Equal("read", query["contents"]);
        Assert.Equal("read", query["metadata"]);
        Assert.Equal(5, query.Count);
    }

    [Fact]
    public void NewTokenUrl_KeepsWithinGitHubsLimits()
    {
        var query = HttpUtility.ParseQueryString(GitHubTokenTemplate.GitHubComUrl.Query);

        Assert.True(query["name"]!.Length <= 40);
        Assert.True(query["description"]!.Length <= 1024);
    }

    [Theory]
    [InlineData("https://github.example.com/")]
    [InlineData("https://github.example.com/some/path?x=1")]
    public void NewTokenUrl_OnAnEnterpriseServer_UsesItsHost(string webUrl)
    {
        Uri url = GitHubTokenTemplate.NewTokenUrl(new Uri(webUrl));

        Assert.Equal("https://github.example.com/settings/personal-access-tokens/new", url.GetLeftPart(UriPartial.Path));
        Assert.Equal("read", HttpUtility.ParseQueryString(url.Query)["contents"]);
    }
}
