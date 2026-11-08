using Microsoft.Extensions.Logging;
using Deosrc.Tammi.Core.Logging;

namespace Deosrc.Tammi.Core.Tests.Logging;

public class LogLevelSwitchTests
{
    private const string TammiCategory = "Deosrc.Tammi.Core.Git.GitCli";

    [Fact]
    public void MinimumLevel_StartsAtInformation()
    {
        Assert.Equal(LogLevel.Information, new LogLevelSwitch().MinimumLevel);
    }

    [Theory]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Error, true)]
    public void IsEnabled_LogsFromTheMinimumLevelUp(LogLevel level, bool expected)
    {
        Assert.Equal(expected, new LogLevelSwitch().IsEnabled(TammiCategory, level));
    }

    [Fact]
    public void IsEnabled_AppliesAChangedLevelStraightAway()
    {
        var levelSwitch = new LogLevelSwitch();

        levelSwitch.MinimumLevel = LogLevel.Debug;

        Assert.True(levelSwitch.IsEnabled(TammiCategory, LogLevel.Debug));
    }

    [Fact]
    public void IsEnabled_NeverLogsNone()
    {
        var levelSwitch = new LogLevelSwitch { MinimumLevel = LogLevel.Trace };

        Assert.False(levelSwitch.IsEnabled(TammiCategory, LogLevel.None));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Components.RenderTree.Renderer")]
    [InlineData("System.Net.Http.HttpClient.Default.LogicalHandler")]
    public void IsEnabled_LogsFrameworkMessagesFromWarningUp_UnlessTheLevelIsTrace(string category)
    {
        var levelSwitch = new LogLevelSwitch { MinimumLevel = LogLevel.Debug };

        Assert.False(levelSwitch.IsEnabled(category, LogLevel.Information));
        Assert.True(levelSwitch.IsEnabled(category, LogLevel.Warning));

        levelSwitch.MinimumLevel = LogLevel.Trace;

        Assert.True(levelSwitch.IsEnabled(category, LogLevel.Trace));
    }

    [Fact]
    public void IsEnabled_LogsFrameworkErrorsOnlyFromTheMinimumLevel()
    {
        var levelSwitch = new LogLevelSwitch { MinimumLevel = LogLevel.Critical };

        Assert.False(levelSwitch.IsEnabled("Microsoft.Extensions.Hosting", LogLevel.Error));
    }
}
