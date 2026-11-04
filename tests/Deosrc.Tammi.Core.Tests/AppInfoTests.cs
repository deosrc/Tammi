namespace TAMMi.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsTammi()
    {
        Assert.Equal("TAMMi", AppInfo.Name);
    }

    [Fact]
    public void Version_IsProductVersionWithoutBuildMetadata()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", AppInfo.Version);
    }
}
