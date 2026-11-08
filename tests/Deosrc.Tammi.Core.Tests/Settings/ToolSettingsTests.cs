using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Settings;

public class ToolSettingsTests
{
    [Fact]
    public void Tools_ArePredefinedThenCustom_AllPredefinedEnabledByDefault()
    {
        var custom = new ToolDefinition { Name = "Zed", Executable = "zed" };

        var tools = new ToolSettings { CustomTools = [custom] }.Tools;

        Assert.Equal([.. PredefinedTools.All.Select(tool => tool.Id), null], tools.Select(tool => tool.Id));
        Assert.All(tools, tool => Assert.True(tool.Enabled));
        Assert.Same(custom, tools[^1]);
    }

    [Fact]
    public void WithEnabled_DisablesAndEnablesPredefinedTool()
    {
        var disabled = new ToolSettings().WithEnabled(PredefinedTools.Rider, false);

        Assert.False(disabled.IsEnabled(PredefinedTools.Rider));
        Assert.False(disabled.Tools.Single(tool => tool.Id == PredefinedTools.Rider.Id).Enabled);
        Assert.True(disabled.IsEnabled(PredefinedTools.VsCode));
        Assert.Equal(["rider"], disabled.WithEnabled(PredefinedTools.Rider, false).DisabledPredefinedTools);

        var enabled = disabled.WithEnabled(PredefinedTools.Rider, true);
        Assert.True(enabled.IsEnabled(PredefinedTools.Rider));
        Assert.Empty(enabled.DisabledPredefinedTools);
    }

    [Fact]
    public void Terminal_IsFirstAndAlwaysEnabled()
    {
        var settings = new ToolSettings { DisabledPredefinedTools = [PredefinedTools.Terminal.Id!] };

        Assert.True(settings.IsEnabled(PredefinedTools.Terminal));
        Assert.True(settings.Tools[0].OpensTerminal);
        Assert.True(settings.Tools[0].Enabled);
        Assert.Throws<ArgumentException>(() => settings.WithEnabled(PredefinedTools.Terminal, false));
    }

    [Fact]
    public void SingleRepositoryTarget_IsTheRepositoryByDefault_AndUnknownValuesAreReplaced()
    {
        Assert.Equal(SingleRepositoryTarget.Repository, new ToolSettings().SingleRepositoryTarget);
        Assert.Equal(
            SingleRepositoryTarget.Repository,
            new ToolSettings { SingleRepositoryTarget = (SingleRepositoryTarget)9 }.Normalize().SingleRepositoryTarget);
        Assert.NotEqual(
            new ToolSettings(),
            new ToolSettings { SingleRepositoryTarget = SingleRepositoryTarget.Workspace });
    }

    [Fact]
    public void WithEnabled_ForCustomTool_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ToolSettings().WithEnabled(new ToolDefinition(), false));
    }

    [Fact]
    public void Equals_ComparesToolsByValue()
    {
        ToolSettings Create() => new ToolSettings
        {
            CustomTools = [new ToolDefinition { Name = "Zed", Executable = "zed" }],
        }.WithEnabled(PredefinedTools.Rider, false);

        Assert.Equal(Create(), Create());
        Assert.NotEqual(Create(), Create().WithEnabled(PredefinedTools.Rider, true));
        Assert.NotEqual(Create(), Create() with { CustomTools = [] });
    }

    [Fact]
    public void Normalize_DropsMissingValues()
    {
        var settings = new ToolSettings
        {
            DisabledPredefinedTools = ["rider", null!, "", "rider"],
            CustomTools = [null!, new ToolDefinition { Executable = " zed " }],
        }.Normalize();

        Assert.Equal(["rider"], settings.DisabledPredefinedTools);
        Assert.Equal([new ToolDefinition { Executable = "zed" }], settings.CustomTools);
    }
}
