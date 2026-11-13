using Deosrc.Tammi.Installation;
using Deosrc.Tammi.Installer;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class InstallerOptionsTests
{
    [Fact]
    public void DefaultsToTheLatestFrameworkDependentRelease()
    {
        var options = InstallerOptions.Parse([], out var error)!;

        Assert.Null(error);
        Assert.False(options.IncludePreReleases);
        Assert.Null(options.Tag);
        Assert.Equal(ReleaseFlavour.FrameworkDependent, options.Flavour);
    }

    [Fact]
    public void ReadsOptions()
    {
        var options = InstallerOptions.Parse(["--prerelease", "--tag", "v1.0.0", "--self-contained"], out _)!;

        Assert.True(options.IncludePreReleases);
        Assert.Equal("v1.0.0", options.Tag);
        Assert.Equal(ReleaseFlavour.SelfContained, options.Flavour);
    }

    [Theory]
    [InlineData("--nope")]
    [InlineData("--tag")]
    public void RejectsBadArguments(string argument)
    {
        Assert.Null(InstallerOptions.Parse([argument], out var error));
        Assert.NotNull(error);
    }
}
