namespace TAMMi.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsTammi()
    {
        Assert.Equal("TAMMi", AppInfo.Name);
    }
}
