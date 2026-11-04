using Deosrc.Tammi.Core.Integrations.GitHub;

namespace Deosrc.Tammi.Core.Tests.Integrations.GitHub;

public class GitHubIssueKeyTests
{
    [Theory]
    [InlineData("acme/api#42", "acme", "api", 42)]
    [InlineData(" Acme-Corp/my_repo.js#7 ", "Acme-Corp", "my_repo.js", 7)]
    public void TryParse_ReadsOwnerRepositoryAndNumber(string text, string owner, string repository, int number)
    {
        Assert.True(GitHubIssueKey.TryParse(text, out GitHubIssueKey? key));
        Assert.Equal(new GitHubIssueKey(owner, repository, number), key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("acme/api")]
    [InlineData("api#42")]
    [InlineData("acme/api#0")]
    [InlineData("acme/api#-1")]
    [InlineData("acme/api#4x")]
    [InlineData("acme/api#99999999999")]
    [InlineData("acme/..#1")]
    [InlineData("ac me/api#1")]
    [InlineData("acme/api/x#1")]
    [InlineData("ac.me/api#1")]
    [InlineData("ABC-123")]
    public void TryParse_RejectsOtherText(string? text)
    {
        Assert.False(GitHubIssueKey.TryParse(text, out _));
    }

    [Fact]
    public void ToString_IsTheTaskKey()
    {
        Assert.Equal("acme/api#42", new GitHubIssueKey("acme", "api", 42).ToString());
    }

    [Theory]
    [InlineData("https://github.com/acme/api/issues/42")]
    [InlineData("https://GitHub.com/acme/api/issues/42/")]
    [InlineData("https://github.com/acme/api/issues/42?q=1#issuecomment-123")]
    public void TryParseUrl_ReadsAnIssuePage(string url)
    {
        Assert.True(GitHubIssueKey.TryParseUrl(url, "github.com", out GitHubIssueKey? key));
        Assert.Equal(new GitHubIssueKey("acme", "api", 42), key);
    }

    [Theory]
    [InlineData("https://github.com/acme/api/pull/42")]
    [InlineData("https://github.com/acme/api")]
    [InlineData("https://gitlab.com/acme/api/issues/42")]
    [InlineData("ftp://github.com/acme/api/issues/42")]
    [InlineData("github.com/acme/api/issues/42")]
    [InlineData("https://github.com/acme/%2E%2E/issues/42")]
    public void TryParseUrl_RejectsOtherUrls(string url)
    {
        Assert.False(GitHubIssueKey.TryParseUrl(url, "github.com", out _));
    }

    [Theory]
    [InlineData("https://api.github.com/repos/acme/api")]
    [InlineData("https://github.example.com/api/v3/repos/acme/api")]
    public void ParseRepositoryApiUrl_ReadsOwnerAndName(string url)
    {
        Assert.Equal(("acme", "api"), GitHubIssueKey.ParseRepositoryApiUrl(url));
    }

    [Theory]
    [InlineData("https://api.github.com/users/acme")]
    [InlineData("not a url")]
    public void ParseRepositoryApiUrl_ReturnsNull_ForOtherUrls(string url)
    {
        Assert.Null(GitHubIssueKey.ParseRepositoryApiUrl(url));
    }
}
