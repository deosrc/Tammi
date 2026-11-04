using Tammi.Core.Integrations.Jira;

namespace Tammi.Core.Tests.Integrations.Jira;

public class JiraIssueKeyTests
{
    private static readonly Uri CloudSite = new("https://acme.atlassian.net/");
    private static readonly Uri DataCenterSite = new("https://jira.example.com/jira/");

    [Theory]
    [InlineData("ABC-123", "ABC-123")]
    [InlineData(" abc-1 ", "ABC-1")]
    [InlineData("A1_B-42", "A1_B-42")]
    public void TryParse_ReadsIssueKeys_InUpperCase(string text, string expected)
    {
        Assert.True(JiraIssueKey.TryParse(text, out string? key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABC-")]
    [InlineData("ABC-0")]
    [InlineData("ABC-012")]
    [InlineData("1ABC-1")]
    [InlineData("_ABC-1")]
    [InlineData("ABC-1 OR project = XYZ")]
    [InlineData("acme/api#1")]
    public void TryParse_Rejects_TextThatIsntAnIssueKey(string? text)
    {
        Assert.False(JiraIssueKey.TryParse(text, out _));
    }

    [Theory]
    [InlineData("https://acme.atlassian.net/browse/ABC-12", "ABC-12")]
    [InlineData("https://ACME.atlassian.net/browse/abc-12/", "ABC-12")]
    [InlineData("https://acme.atlassian.net/browse/ABC-12?focusedCommentId=1", "ABC-12")]
    [InlineData("https://acme.atlassian.net/jira/software/c/projects/ABC/boards/3?selectedIssue=ABC-12", "ABC-12")]
    [InlineData("https://acme.atlassian.net/issues/?jql=project%3DABC&selectedIssue=ABC-12", "ABC-12")]
    public void TryParseUrl_ReadsIssueUrlsOnTheSite(string url, string expected)
    {
        Assert.True(JiraIssueKey.TryParseUrl(url, CloudSite, out string? key));
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("https://other.atlassian.net/browse/ABC-12")]
    [InlineData("https://acme.atlassian.net:8443/browse/ABC-12")]
    [InlineData("ftp://acme.atlassian.net/browse/ABC-12")]
    [InlineData("https://acme.atlassian.net/browse/not-a-key")]
    [InlineData("https://acme.atlassian.net/projects/ABC")]
    [InlineData("project = ABC")]
    public void TryParseUrl_Rejects_OtherUrls(string url)
    {
        Assert.False(JiraIssueKey.TryParseUrl(url, CloudSite, out _));
    }

    [Fact]
    public void TryParseUrl_NeedsTheSitePath_ForASiteServedUnderAPath()
    {
        Assert.True(JiraIssueKey.TryParseUrl("https://jira.example.com/jira/browse/ABC-1", DataCenterSite,
            out string? key));
        Assert.Equal("ABC-1", key);
        Assert.False(JiraIssueKey.TryParseUrl("https://jira.example.com/browse/ABC-1", DataCenterSite, out _));
    }
}
