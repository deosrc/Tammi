using TAMMi.Core.Agents;

namespace TAMMi.Core.Tests.Agents;

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
    public void ClaudeCode_TakesPromptAsPositionalArgument()
    {
        Assert.True(AgentDefinition.ClaudeCode.AcceptsInitialPrompt);
        Assert.Equal(["Read tasks.md"], AgentDefinition.ClaudeCode.BuildArguments("Read tasks.md"));
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
    }
}
