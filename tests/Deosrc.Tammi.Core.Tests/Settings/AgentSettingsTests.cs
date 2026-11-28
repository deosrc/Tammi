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
            [AgentDefinition.ClaudeCode, custom],
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

    [Fact]
    public void StartOnWorkspaceCreation_ByDefault_StartsNoAgent()
    {
        var settings = new AgentSettings();

        Assert.Null(settings.StartOnWorkspaceCreation);
        Assert.Null(settings.DefinitionToStartOnWorkspaceCreation);
    }

    [Theory]
    [InlineData("Gemini")]
    [InlineData(" gemini ")]
    public void DefinitionToStartOnWorkspaceCreation_IsTheAgentWithTheName_IgnoringCaseAndSpaces(string name)
    {
        var gemini = new AgentDefinition("Gemini", "gemini");
        var settings = new AgentSettings
        {
            CustomDefinitions = [new AgentDefinition("Codex", "codex"), gemini],
            StartOnWorkspaceCreation = name,
        };

        Assert.Same(gemini, settings.DefinitionToStartOnWorkspaceCreation);
    }

    [Fact]
    public void DefinitionToStartOnWorkspaceCreation_WithAMissingAgent_IsNull_UntilAnAgentHasTheNameAgain()
    {
        var settings = new AgentSettings { StartOnWorkspaceCreation = "Codex" };

        Assert.Null(settings.DefinitionToStartOnWorkspaceCreation);

        var codex = new AgentDefinition("Codex", "codex");
        Assert.Same(codex, (settings with { CustomDefinitions = [codex] }).DefinitionToStartOnWorkspaceCreation);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData(" Removed agent ", "Removed agent")]
    public void Normalize_TrimsTheAgentToStart_AndKeepsAMissingOne(string? name, string? expected)
    {
        Assert.Equal(expected, new AgentSettings { StartOnWorkspaceCreation = name }.Normalize().StartOnWorkspaceCreation);
    }

    [Fact]
    public void Equals_ComparesTheAgentToStart()
    {
        Assert.Equal(
            new AgentSettings { StartOnWorkspaceCreation = "Codex" },
            new AgentSettings { StartOnWorkspaceCreation = "Codex" });
        Assert.NotEqual(new AgentSettings { StartOnWorkspaceCreation = "Codex" }, new AgentSettings());
    }

    private static AgentSettings WithCustomNames(params string[] names) =>
        new() { CustomDefinitions = names.Select(name => new AgentDefinition(name, "agent")).ToArray() };
}
