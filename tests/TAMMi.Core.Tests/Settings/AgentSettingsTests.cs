using TAMMi.Core.Agents;
using TAMMi.Core.Settings;

namespace TAMMi.Core.Tests.Settings;

public class AgentSettingsTests
{
    [Fact]
    public void HasDuplicateName_WithDifferentNames_IsFalse()
    {
        var settings = WithNames("Claude Code", "Codex");

        Assert.False(settings.HasDuplicateName(0));
        Assert.False(settings.HasDuplicateName(1));
    }

    [Theory]
    [InlineData("Claude Code", "Claude Code")]
    [InlineData("Claude Code", "claude code")]
    [InlineData("Claude Code", "  Claude Code ")]
    public void HasDuplicateName_WithMatchingNames_IsTrueForBoth(string first, string second)
    {
        var settings = WithNames(first, "Codex", second);

        Assert.True(settings.HasDuplicateName(0));
        Assert.False(settings.HasDuplicateName(1));
        Assert.True(settings.HasDuplicateName(2));
    }

    [Fact]
    public void HasDuplicateName_WithBlankNames_IsFalse()
    {
        var settings = WithNames("", " ", "");

        Assert.False(settings.HasDuplicateName(0));
        Assert.False(settings.HasDuplicateName(1));
    }

    [Fact]
    public void HasDuplicateName_WithBuiltInDefinitions_IsFalse()
    {
        var settings = new AgentSettings();

        Assert.All(Enumerable.Range(0, settings.Definitions.Count), i => Assert.False(settings.HasDuplicateName(i)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void HasDuplicateName_OutsideTheList_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WithNames("a", "b").HasDuplicateName(index));
    }

    private static AgentSettings WithNames(params string[] names) =>
        new() { Definitions = names.Select(name => new AgentDefinition(name, "agent")).ToArray() };
}
