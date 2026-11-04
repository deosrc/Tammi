using Tammi.Core.Integrations;
using Tammi.Core.Settings;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceNamesTests
{
    private static readonly WorkspaceNameFormat Default = new();

    private static readonly WorkspaceNameFormat AsWrittenWithSpaces = new()
    {
        Separator = WorkspaceNameSeparator.Spaces,
        Case = WorkspaceNameCase.AsWritten,
        IncludeTaskReference = false,
    };

    [Fact]
    public void Suggest_ByDefault_IsLowercaseHyphenatedWithTheReference()
    {
        Assert.Equal("jira-123-add-some-feature",
            WorkspaceNames.Suggest([Task("JIRA-123", "Add some feature")], Default));
    }

    [Fact]
    public void Suggest_ForAGitHubIssue_UsesTheRepositoryAndNumber()
    {
        Assert.Equal("api-42-fix-login", WorkspaceNames.Suggest([Task("acme/api#42", "Fix login")], Default));
    }

    [Theory]
    [InlineData(WorkspaceNameSeparator.Hyphens, WorkspaceNameCase.Lowercase, true, "jira-123-add-some-feature")]
    [InlineData(WorkspaceNameSeparator.Underscores, WorkspaceNameCase.Lowercase, true, "jira_123_add_some_feature")]
    [InlineData(WorkspaceNameSeparator.Spaces, WorkspaceNameCase.Lowercase, true, "jira-123 add some feature")]
    [InlineData(WorkspaceNameSeparator.Hyphens, WorkspaceNameCase.Uppercase, true, "JIRA-123-ADD-SOME-FEATURE")]
    [InlineData(WorkspaceNameSeparator.Hyphens, WorkspaceNameCase.AsWritten, true, "JIRA-123-Add-some-Feature")]
    [InlineData(WorkspaceNameSeparator.Spaces, WorkspaceNameCase.AsWritten, true, "JIRA-123 Add some Feature")]
    [InlineData(WorkspaceNameSeparator.Hyphens, WorkspaceNameCase.Lowercase, false, "add-some-feature")]
    [InlineData(WorkspaceNameSeparator.Spaces, WorkspaceNameCase.AsWritten, false, "Add some Feature")]
    public void Suggest_FollowsTheFormat(
        WorkspaceNameSeparator separator,
        WorkspaceNameCase letterCase,
        bool includeReference,
        string expected)
    {
        var format = new WorkspaceNameFormat
        {
            Separator = separator,
            Case = letterCase,
            IncludeTaskReference = includeReference,
        };

        Assert.Equal(expected, WorkspaceNames.Suggest([Task("JIRA-123", "Add some Feature")], format));
    }

    [Fact]
    public void Suggest_WithNoTasks_SuggestsNothing()
    {
        Assert.Null(WorkspaceNames.Suggest([], Default));
    }

    [Fact]
    public void Suggest_WithSeveralTasks_SuggestsNothing()
    {
        Assert.Null(WorkspaceNames.Suggest([Task("acme/api#1", "Fix login"), Task("ABC-2", "Add logout")], Default));
    }

    [Fact]
    public void Suggest_WithATitleOfOnlyPunctuation_UsesTheReference()
    {
        Assert.Equal("abc-2", WorkspaceNames.Suggest([Task("ABC-2", "???")], Default));
    }

    [Theory]
    [InlineData("acme/api#42", "api#42")]
    [InlineData("ABC-123", "ABC-123")]
    [InlineData("a/b/c#1", "c#1")]
    public void ShortReference_DropsTheOwner(string key, string expected)
    {
        Assert.Equal(expected, WorkspaceNames.ShortReference(key));
    }

    [Theory]
    [InlineData("Fix: login / logout", "fix-login-logout")]
    [InlineData("Don't break, please!", "dont-break-please")]
    [InlineData("Support a|b and <c>", "support-a-b-and-c")]
    [InlineData("  Tidy   the\tspaces  ", "tidy-the-spaces")]
    [InlineData("Use v1.2 (beta)", "use-v1-2-beta")]
    [InlineData("Ünïcödé títle", "ünïcödé-títle")]
    public void FromTitle_Hyphenated_LeavesOutPunctuation(string title, string expected)
    {
        string? name = WorkspaceNames.FromTitle(title, Default);

        Assert.Equal(expected, name);
        Assert.Null(WorkspaceNames.Validate(name));
    }

    [Theory]
    [InlineData("Fix: login / logout", "Fix login logout")]
    [InlineData("Support a|b and <c>", "Support a b and c")]
    [InlineData("  Tidy   the\tspaces  ", "Tidy the spaces")]
    [InlineData("Ends with dots...", "Ends with dots")]
    [InlineData("Why does it fail?", "Why does it fail")]
    [InlineData("Don't break, please!", "Don't break, please!")]
    [InlineData("Line\nbreak", "Line break")]
    public void FromTitle_WithSpaces_KeepsPunctuationThatFoldersAllow(string title, string expected)
    {
        string? name = WorkspaceNames.FromTitle(title, AsWrittenWithSpaces);

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
        Assert.Null(WorkspaceNames.FromTitle(title, Default));
        Assert.Null(WorkspaceNames.FromTitle(title, AsWrittenWithSpaces));
    }

    [Fact]
    public void FromTitle_WithLongTitle_CutsAtWord_KeepingTheReference()
    {
        string title = string.Join(' ', Enumerable.Repeat("word", 30));

        string? name = WorkspaceNames.FromTitle(title, Default, "ABC-1");

        Assert.NotNull(name);
        Assert.True(name.Length <= WorkspaceNames.MaxSuggestedLength);
        Assert.StartsWith("abc-1-word-", name);
        Assert.EndsWith("-word", name);
    }

    [Fact]
    public void FromTitle_WithLongWord_CutsWithinIt()
    {
        string name = WorkspaceNames.FromTitle("short " + new string('x', 200), Default)!;

        Assert.Equal(WorkspaceNames.MaxSuggestedLength, name.Length);
        Assert.StartsWith("short-xxx", name);
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
