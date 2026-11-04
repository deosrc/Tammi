using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitRemoteUrlsTests
{
    [Theory]
    [InlineData("https://github.com/acme/api.git", "acme")]
    [InlineData("https://github.com/acme/api", "acme")]
    [InlineData("https://github.com/acme/api/", "acme")]
    [InlineData("https://user@github.example.com:8443/scm/acme/api.git?x=1", "acme")]
    [InlineData("ssh://git@github.com/acme/api.git", "acme")]
    [InlineData("ssh://git@github.com:2222/acme/api.git", "acme")]
    [InlineData("git@github.com:acme/api.git", "acme")]
    [InlineData("github.com:acme/api", "acme")]
    [InlineData("/srv/git/acme/api.git", "acme")]
    [InlineData("../acme/api", "acme")]
    [InlineData(@"C:\repos\acme\api", "acme")]
    [InlineData("file:///srv/git/acme/api.git", "acme")]
    public void GetOwner_ReadsPartBeforeRepositoryName(string url, string owner)
    {
        Assert.Equal(owner, GitRemoteUrls.GetOwner(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("api")]
    [InlineData("git@github.com:api.git")]
    [InlineData("https://github.com/api")]
    [InlineData("https://github.com")]
    public void GetOwner_NoOwner_IsNull(string? url)
    {
        Assert.Null(GitRemoteUrls.GetOwner(url));
    }

    [Theory]
    [InlineData("https://github.com/acme/api.git")]
    [InlineData("https://github.com/acme/api")]
    [InlineData("https://github.com/Acme/API/")]
    [InlineData("https://user:token@github.com/acme/api.git")]
    [InlineData("git@github.com:acme/api.git")]
    [InlineData("git@github.com:acme/api")]
    [InlineData("ssh://git@github.com/acme/api.git")]
    [InlineData("ssh://git@github.com:22/acme/api.git")]
    public void IsSameRepository_SameHostAndPath_Matches(string url)
    {
        Assert.True(GitRemoteUrls.IsSameRepository(url, "https://github.com/acme/api"));
    }

    [Theory]
    [InlineData("https://github.com/acme/web.git")]
    [InlineData("https://github.com/other/api.git")]
    [InlineData("https://gitlab.com/acme/api.git")]
    [InlineData("git@github.example.com:acme/api.git")]
    [InlineData("/srv/git/acme/api.git")]
    [InlineData("file:///github.com/acme/api.git")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSameRepository_Different_DoesNotMatch(string? url)
    {
        Assert.False(GitRemoteUrls.IsSameRepository(url, "https://github.com/acme/api"));
    }
}
