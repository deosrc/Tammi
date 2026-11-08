using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Settings;

public class ToolSettingsTests
{
    [Fact]
    public void GetBuiltIn_WithoutSettings_IsEnabledWithDefaultScope()
    {
        var settings = new ToolSettings();

        Assert.True(settings.GetBuiltIn(BuiltInTool.VsCode).Enabled);
        Assert.Equal(BuiltInTool.VsCode.DefaultScope, settings.GetScope(BuiltInTool.VsCode));
    }

    [Fact]
    public void WithBuiltIn_ChangesOnlyThatTool()
    {
        var settings = new ToolSettings()
            .WithBuiltIn(BuiltInTool.Rider, s => s with { Enabled = false })
            .WithBuiltIn(BuiltInTool.Rider, s => s with { Scope = ToolScope.Repository });

        Assert.False(settings.GetBuiltIn(BuiltInTool.Rider).Enabled);
        Assert.Equal(ToolScope.Repository, settings.GetScope(BuiltInTool.Rider));
        Assert.True(settings.GetBuiltIn(BuiltInTool.VsCode).Enabled);
    }

    [Fact]
    public void Equals_ComparesToolsByValue()
    {
        ToolSettings Create() => new ToolSettings
        {
            CustomTools = [new ToolDefinition { Name = "Zed", Executable = "zed" }],
        }.WithBuiltIn(BuiltInTool.Rider, s => s with { Enabled = false });

        Assert.Equal(Create(), Create());
        Assert.NotEqual(Create(), Create().WithBuiltIn(BuiltInTool.Rider, s => s with { Enabled = true }));
        Assert.NotEqual(Create(), Create() with { CustomTools = [] });
    }

    [Fact]
    public void Normalize_DropsMissingToolsAndUnknownScopes()
    {
        var settings = new ToolSettings
        {
            BuiltInTools = new Dictionary<string, BuiltInToolSettings>
            {
                ["rider"] = new() { Scope = (ToolScope)42 },
                ["vscode"] = null!,
            },
            CustomTools = [null!, new ToolDefinition { Executable = " zed " }],
        }.Normalize();

        Assert.Null(settings.GetBuiltIn(BuiltInTool.Rider).Scope);
        Assert.False(settings.BuiltInTools.ContainsKey("vscode"));
        Assert.Equal([new ToolDefinition { Executable = "zed" }], settings.CustomTools);
    }
}
