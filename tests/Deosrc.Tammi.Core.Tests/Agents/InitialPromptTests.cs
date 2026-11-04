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
    public void Predefined_AreThreeDistinctOneLineTemplates_WithThePlaceholder()
    {
        Assert.Equal(
            [InitialPrompt.ImplementTemplate, InitialPrompt.PlanTemplate, InitialPrompt.ReadOnlyTemplate],
            InitialPrompt.Predefined.Select(prompt => prompt.Template));
        Assert.Equal(3, InitialPrompt.Predefined.Select(prompt => prompt.Name).Distinct().Count());
        Assert.All(InitialPrompt.Predefined, prompt =>
        {
            Assert.Contains(InitialPrompt.ContextFilePlaceholder, prompt.Template, StringComparison.Ordinal);
            Assert.Contains("read it again", prompt.Template, StringComparison.Ordinal);
            Assert.DoesNotContain('\n', prompt.Template);
            Assert.DoesNotContain('\r', prompt.Template);
        });
    }

    [Fact]
    public void DefaultTemplate_IsAPredefinedTemplate()
    {
        Assert.Contains(InitialPrompt.DefaultTemplate, InitialPrompt.Predefined.Select(prompt => prompt.Template));
    }
}
