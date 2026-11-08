using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

public class AgentDefinitionTests
{
    public static TheoryData<string> SpecialPrompts =>
    [
        "plain",
        "with spaces",
        "",
        " leading and trailing ",
        "quotes \" and ' and `",
        "shell $HOME $(whoami) `id` ; | & && || < > * ? ~ # !",
        "cmd %PATH% %% ^ & | < > ( ) !var!",
        @"backslashes \ \\ trailing\",
        "C:\\path with spaces\\\"quoted\"\\",
        "line one\nline two\r\nline three",
        "tabs\tand unicode: café ✓ 日本語",
        "-starts-like-an-option",
        "{prompt} inside the prompt",
    ];

    [Theory]
    [MemberData(nameof(SpecialPrompts))]
    public void BuildArguments_PassesPromptAsOneArgumentUnchanged(string prompt)
    {
        var definition = new AgentDefinition("Test", "agent", "--flag") { PromptArguments = ["{prompt}"] };

        var arguments = definition.BuildArguments(prompt);

        if (prompt.Length == 0)
        {
            Assert.Equal(["--flag"], arguments);
        }
        else
        {
            Assert.Equal(["--flag", prompt], arguments);
        }
    }

    [Fact]
    public void BuildArguments_FillsPlaceholderInEveryPromptArgument()
    {
        var definition = new AgentDefinition("Test", "agent", "-a")
        {
            PromptArguments = ["-i", "{prompt}", "--prompt={prompt}", "--literal"],
        };

        Assert.Equal(
            ["-a", "-i", "do it", "--prompt=do it", "--literal"],
            definition.BuildArguments("do it"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildArguments_WithoutPrompt_LeavesPromptArgumentsOut(string? prompt)
    {
        var definition = new AgentDefinition("Test", "agent", "-a") { PromptArguments = ["-i", "{prompt}"] };

        Assert.Equal(["-a"], definition.BuildArguments(prompt));
    }

    [Fact]
    public void BuildArguments_WhenAgentTakesNoPrompt_LeavesPromptOut()
    {
        var definition = new AgentDefinition("Test", "agent", "-a");

        Assert.False(definition.AcceptsInitialPrompt);
        Assert.Equal(["-a"], definition.BuildArguments("ignored"));
    }

    [Fact]
    public void OpenCode_TakesPromptAsOption_AndHasNoHooksOrResume()
    {
        var definition = AgentDefinition.OpenCode;

        Assert.Equal("opencode", definition.Command);
        Assert.Equal(["--prompt", "Read tasks.md"], definition.BuildArguments("Read tasks.md", "ignored-id"));
        Assert.Empty(definition.BuildArguments(null));
        Assert.Null(definition.Hooks);
        Assert.False(definition.CanResume);
    }

    [Fact]
    public void ClaudeCode_TakesPromptAsPositionalArgument()
    {
        Assert.True(AgentDefinition.ClaudeCode.AcceptsInitialPrompt);
        Assert.Equal(["Read tasks.md"], AgentDefinition.ClaudeCode.BuildArguments("Read tasks.md"));
    }

    [Fact]
    public void Equals_ComparesHooks()
    {
        var definition = new AgentDefinition("Agent", "agent");

        Assert.NotEqual(definition, definition with { Hooks = ClaudeCodeHooks.Instance });
        Assert.Equal(definition with { Hooks = ClaudeCodeHooks.Instance }, definition with { Hooks = ClaudeCodeHooks.Instance });
    }

    [Fact]
    public void Equals_ComparesArgumentsByValue()
    {
        var definition = new AgentDefinition("Test", "agent", "-a") { PromptArguments = ["{prompt}"] };

        Assert.Equal(definition, new AgentDefinition("Test", "agent", ["-a"]) { PromptArguments = ["{prompt}"] });
        Assert.Equal(definition.GetHashCode(), new AgentDefinition("Test", "agent", "-a") { PromptArguments = ["{prompt}"] }.GetHashCode());
        Assert.NotEqual(definition, definition with { Arguments = ["-b"] });
        Assert.NotEqual(definition, definition with { PromptArguments = [] });
        Assert.NotEqual(definition, definition with { Command = "other" });
        Assert.NotEqual(definition, definition with { SessionIdArguments = ["--id", "{sessionId}"] });
        Assert.NotEqual(definition, definition with { ResumeArguments = ["--resume", "{sessionId}"] });
    }

    [Fact]
    public void BuildArguments_WithSessionId_AddsSessionIdArgumentsBeforePrompt()
    {
        var definition = Resumable with { PromptArguments = ["{prompt}"] };

        Assert.Equal(["-a", "--id", "abc", "--tag=abc", "do it"], definition.BuildArguments("do it", "abc"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildArguments_WithoutSessionId_LeavesSessionIdArgumentsOut(string? sessionId)
    {
        Assert.Equal(["-a"], Resumable.BuildArguments(null, sessionId));
    }

    [Fact]
    public void BuildArguments_WhenAgentCantResume_LeavesSessionIdOut()
    {
        // Setting the ID is no use without a way to resume, so neither list on its own is used.
        var definition = Resumable with { ResumeArguments = [] };

        Assert.False(definition.CanResume);
        Assert.Equal(["-a"], definition.BuildArguments(null, "abc"));
    }

    [Fact]
    public void BuildResumeArguments_ReplacesSessionIdArgumentsAndPrompt()
    {
        var definition = Resumable with { PromptArguments = ["{prompt}"] };

        Assert.Equal(["-a", "--resume", "abc"], definition.BuildResumeArguments("abc"));
    }

    [Fact]
    public void BuildResumeArguments_WhenAgentCantResume_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            (Resumable with { SessionIdArguments = [] }).BuildResumeArguments("abc"));
    }

    [Fact]
    public void ClaudeCode_SetsSessionIdAtLaunchAndResumesIt()
    {
        var claude = AgentDefinition.ClaudeCode;

        Assert.True(claude.CanResume);
        Assert.Equal(["--session-id", "abc", "Read tasks.md"], claude.BuildArguments("Read tasks.md", "abc"));
        Assert.Equal(["--resume", "abc"], claude.BuildResumeArguments("abc"));
    }

    [Fact]
    public void CanResume_WithHooksThatReportSessionId_NeedsOnlyResumeArguments()
    {
        var definition = AgentDefinition.ClaudeCode with { SessionIdArguments = [] };

        Assert.True(definition.CanResume);
        Assert.Equal(["do it"], definition.BuildArguments("do it", "abc"));
        Assert.False((definition with { ResumeArguments = [] }).CanResume);
    }

    [Fact]
    public void Normalize_FillsInMissingSessionArguments()
    {
        var definition = new AgentDefinition("Test", "agent") { SessionIdArguments = null!, ResumeArguments = ["-r", null!] };

        var normalized = definition.Normalize();

        Assert.Empty(normalized.SessionIdArguments);
        Assert.Equal(["-r"], normalized.ResumeArguments);
    }

    private static AgentDefinition Resumable { get; } = new("Test", "agent", "-a")
    {
        SessionIdArguments = ["--id", "{sessionId}", "--tag={sessionId}"],
        ResumeArguments = ["--resume", "{sessionId}"],
    };
}
