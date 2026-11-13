using Deosrc.Tammi.Installer;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class InstallerOptionsTests
{
    [Fact]
    public void DefaultsToTheLatestStableRelease()
    {
        var options = InstallerOptions.Parse([], out var error)!;

        Assert.Null(error);
        Assert.False(options.IncludePreReleases);
        Assert.Null(options.Tag);
    }

    [Fact]
    public void ReadsOptions()
    {
        var options = InstallerOptions.Parse(["--prerelease", "--tag", "v1.0.0"], out _)!;

        Assert.True(options.IncludePreReleases);
        Assert.Equal("v1.0.0", options.Tag);
    }

    [Theory]
    [InlineData("--nope")]
    [InlineData("--tag")]
    [InlineData("--self-contained")]
    public void RejectsBadArguments(string argument)
    {
        Assert.Null(InstallerOptions.Parse([argument], out var error));
        Assert.NotNull(error);
    }
}
