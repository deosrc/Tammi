using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public class AgentSettingsTests
{
    [Fact]
    public void Definitions_AreSupportedAgentsThenCustomAgents()
    {
        var custom = new AgentDefinition("Codex", "codex");

        Assert.Equal(
            [AgentDefinition.ClaudeCode, AgentDefinition.OpenCode, custom],
            new AgentSettings { CustomDefinitions = [custom] }.Definitions);
    }

    [Fact]
    public void HasDuplicateName_WithDifferentNames_IsFalse()
    {
        var settings = WithCustomNames("Codex", "Gemini");

        Assert.False(settings.HasDuplicateName(0));
        Assert.False(settings.HasDuplicateName(1));
    }

    [Theory]
    [InlineData("Codex", "Codex")]
    [InlineData("Codex", "codex")]
    [InlineData("Codex", "  Codex ")]
    public void HasDuplicateName_WithMatchingCustomNames_IsTrueForBoth(string first, string second)
    {
        var settings = WithCustomNames(first, "Gemini", second);

        Assert.True(settings.HasDuplicateName(0));
        Assert.False(settings.HasDuplicateName(1));
        Assert.True(settings.HasDuplicateName(2));
    }

    [Theory]
    [InlineData("Claude Code")]
    [InlineData(" claude code ")]
    public void HasDuplicateName_WithSupportedAgentsName_IsTrue(string name)
    {
        Assert.True(WithCustomNames(name).HasDuplicateName(0));
    }

    [Fact]
    public void HasDuplicateName_WithBlankNames_IsFalse()
    {
        var settings = WithCustomNames("", " ", "");

        Assert.False(settings.HasDuplicateName(0));
        Assert.False(settings.HasDuplicateName(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void HasDuplicateName_OutsideTheCustomAgents_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WithCustomNames("a", "b").HasDuplicateName(index));
    }

    private static AgentSettings WithCustomNames(params string[] names) =>
        new() { CustomDefinitions = names.Select(name => new AgentDefinition(name, "agent")).ToArray() };
}
