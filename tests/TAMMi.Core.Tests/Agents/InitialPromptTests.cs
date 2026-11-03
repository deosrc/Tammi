using TAMMi.Core.Agents;

namespace TAMMi.Core.Tests.Agents;

public class InitialPromptTests
{
    [Fact]
    public void Expand_ReplacesPlaceholderWithPath()
    {
        Assert.Equal(
            "Read /work/ws/.tammi/tasks.md now.",
            InitialPrompt.Expand("Read {contextFile} now.", "/work/ws/.tammi/tasks.md"));
    }

    [Fact]
    public void Expand_ReplacesEveryPlaceholder()
    {
        Assert.Equal("a.md, then a.md", InitialPrompt.Expand("{contextFile}, then {contextFile}", "a.md"));
    }

    [Theory]
    [InlineData("No placeholder here.")]
    [InlineData("Other braces stay: {path} {contextfile} {{contextFile}")]
    [InlineData("Quotes \" ' ` and symbols & | < > ^ % ! $ ; ( ) \\ stay\nacross lines")]
    [InlineData("")]
    public void Expand_KeepsOtherTextAsItIs(string template)
    {
        var expected = template.Replace("{contextFile}", "x.md", StringComparison.Ordinal);

        Assert.Equal(expected, InitialPrompt.Expand(template, "x.md"));
    }

    [Theory]
    [InlineData(@"C:\Users\Jo Smith\Workspaces\100% done & dusted\.tammi\tasks.md")]
    [InlineData("/home/jo/it's \"quoted\" $HOME `cmd`/.tammi/tasks.md")]
    [InlineData("/home/jo/{contextFile}/.tammi/tasks.md")]
    public void Expand_InsertsPathWithSpecialCharactersAsItIs(string path)
    {
        Assert.Equal($"Read {path}.", InitialPrompt.Expand("Read {contextFile}.", path));
    }

    [Fact]
    public void DefaultTemplate_IsOneLineWithPlaceholder()
    {
        Assert.Contains(InitialPrompt.ContextFilePlaceholder, InitialPrompt.DefaultTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', InitialPrompt.DefaultTemplate);
        Assert.DoesNotContain('\r', InitialPrompt.DefaultTemplate);
    }
}
