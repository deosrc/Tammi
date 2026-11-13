using System.Text.Json;
using Deosrc.Tammi.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class ReleaseInfoTests
{
    private const string Json = """
        [
          { "tag_name": "v0.3.0-beta.1", "draft": false, "html_url": "https://example.com/r/3", "assets": [] },
          { "tag_name": "v0.2.0", "draft": false, "html_url": "https://example.com/r/2", "assets": [
            { "name": "tammi-v0.2.0-fd-linux-x64.tar.gz", "browser_download_url": "https://example.com/fd-linux", "size": 10 },
            { "name": "tammi-v0.2.0-sc-linux-x64.tar.gz", "browser_download_url": "https://example.com/sc-linux", "size": 20 },
            { "name": "tammi-v0.2.0-fd-win-x64.zip", "browser_download_url": "https://example.com/fd-win", "size": 30 }
          ] },
          { "tag_name": "v0.4.0", "draft": true, "html_url": "https://example.com/r/4", "assets": [] },
          { "tag_name": "not-a-version", "draft": false, "html_url": "https://example.com/r/5", "assets": [] }
        ]
        """;

    private static IReadOnlyList<ReleaseInfo> Releases()
    {
        using var document = JsonDocument.Parse(Json);
        return GitHubReleaseClient.ParseReleases(document.RootElement);
    }

    [Fact]
    public void ParsingSkipsDraftsAndNonVersionTags() =>
        Assert.Equal(["v0.3.0-beta.1", "v0.2.0"], Releases().Select(release => release.Tag));

    [Fact]
    public void FlagsPreReleases() =>
        Assert.Equal([true, false], Releases().Select(release => release.IsPreRelease));

    [Theory]
    [InlineData("linux-x64", "fd-linux")]
    [InlineData("win-x64", "fd-win")]
    [InlineData("linux-arm64", null)]
    public void FindsTheArchiveForTheTarget(string rid, string? expectedUrlEnd)
    {
        var release = Releases().Single(r => r.Tag == "v0.2.0");
        var archive = release.FindArchive(new ReleaseTarget(rid));
        Assert.Equal(expectedUrlEnd, archive?.DownloadUrl.AbsolutePath.TrimStart('/'));
    }
}
