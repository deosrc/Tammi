using TAMMi.Core.Integrations;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

public sealed class WorkspaceNamesTests
{
    [Fact]
    public void Suggest_WithOneTask_UsesItsTitle()
    {
        Assert.Equal("Fix login", WorkspaceNames.Suggest([Task("acme/api#1", "Fix login")]));
    }

    [Fact]
    public void Suggest_WithNoTasks_SuggestsNothing()
    {
        Assert.Null(WorkspaceNames.Suggest([]));
    }

    [Fact]
    public void Suggest_WithSeveralTasks_SuggestsNothing()
    {
        Assert.Null(WorkspaceNames.Suggest([Task("acme/api#1", "Fix login"), Task("ABC-2", "Add logout")]));
    }

    [Theory]
    [InlineData("Fix: login / logout", "Fix login logout")]
    [InlineData("Support a|b and <c>", "Support a b and c")]
    [InlineData("  Tidy   the\tspaces  ", "Tidy the spaces")]
    [InlineData("Ends with dots...", "Ends with dots")]
    [InlineData("Why does it fail?", "Why does it fail")]
    [InlineData("Line\nbreak", "Line break")]
    [InlineData("Ünïcödé títle", "Ünïcödé títle")]
    public void FromTitle_MakesValidFolderName(string title, string expected)
    {
        string? name = WorkspaceNames.FromTitle(title);

        Assert.Equal(expected, name);
        Assert.Null(WorkspaceNames.Validate(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("...")]
    public void FromTitle_WithNothingUsable_ReturnsNull(string? title)
    {
        Assert.Null(WorkspaceNames.FromTitle(title));
    }

    [Fact]
    public void FromTitle_WithLongTitle_CutsAtWord()
    {
        string title = string.Join(' ', Enumerable.Repeat("word", 30));

        string? name = WorkspaceNames.FromTitle(title);

        Assert.NotNull(name);
        Assert.True(name.Length <= WorkspaceNames.MaxSuggestedLength);
        Assert.EndsWith("word", name);
        Assert.StartsWith(name, title);
    }

    [Fact]
    public void FromTitle_WithLongWord_CutsWithinIt()
    {
        string name = WorkspaceNames.FromTitle("short " + new string('x', 200))!;

        Assert.Equal(WorkspaceNames.MaxSuggestedLength, name.Length);
    }

    [Theory]
    [InlineData("Fix login")]
    [InlineData("  Fix login  ")]
    [InlineData(".hidden")]
    [InlineData("v1.2")]
    [InlineData("CONSOLE")]
    public void Validate_AcceptsValidNames(string name)
    {
        Assert.Null(WorkspaceNames.Validate(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("../escape")]
    [InlineData("C:")]
    [InlineData("what?")]
    [InlineData("star*")]
    [InlineData("quote\"")]
    [InlineData("pipe|")]
    [InlineData("tab\there")]
    [InlineData("ends with dot.")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("com1.txt")]
    [InlineData("LPT9")]
    public void Validate_RejectsInvalidNames(string? name)
    {
        Assert.NotNull(WorkspaceNames.Validate(name));
    }

    [Fact]
    public void Validate_RejectsNameTooLongForFolder()
    {
        Assert.Null(WorkspaceNames.Validate(new string('a', 255)));
        Assert.NotNull(WorkspaceNames.Validate(new string('a', 256)));

        // Linux limits names in bytes: 128 two-byte characters are 256 bytes.
        Assert.NotNull(WorkspaceNames.Validate(new string('é', 128)));
    }

    private static TaskInfo Task(string key, string title) =>
        new("github", key, title, new Uri("https://example.com/" + Uri.EscapeDataString(key)));
}
