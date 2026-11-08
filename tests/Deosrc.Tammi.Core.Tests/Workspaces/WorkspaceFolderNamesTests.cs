using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceFolderNamesTests
{
    private static readonly WorkspaceFolderNameFormat Default = new();

    private static readonly WorkspaceFolderNameFormat AsWrittenWithSpaces = new()
    {
        Separator = WorkspaceFolderNameSeparator.Spaces,
        Case = WorkspaceFolderNameCase.AsWritten,
        IncludeTaskReference = false,
    };

    [Fact]
    public void Suggest_ByDefault_IsLowercaseHyphenatedWithTheReference()
    {
        Assert.Equal("jira-123-add-some-feature",
            WorkspaceFolderNames.Suggest([Task("JIRA-123", "Add some feature")], Default));
    }

    [Fact]
    public void Suggest_ForAGitHubIssue_UsesTheRepositoryAndNumber()
    {
        Assert.Equal("api-42-fix-login", WorkspaceFolderNames.Suggest([Task("acme/api#42", "Fix login")], Default));
    }

    [Theory]
    [InlineData(WorkspaceFolderNameSeparator.Hyphens, WorkspaceFolderNameCase.Lowercase, true, "jira-123-add-some-feature")]
    [InlineData(WorkspaceFolderNameSeparator.Underscores, WorkspaceFolderNameCase.Lowercase, true, "jira_123_add_some_feature")]
    [InlineData(WorkspaceFolderNameSeparator.Spaces, WorkspaceFolderNameCase.Lowercase, true, "jira-123 add some feature")]
    [InlineData(WorkspaceFolderNameSeparator.Hyphens, WorkspaceFolderNameCase.Uppercase, true, "JIRA-123-ADD-SOME-FEATURE")]
    [InlineData(WorkspaceFolderNameSeparator.Hyphens, WorkspaceFolderNameCase.AsWritten, true, "JIRA-123-Add-some-Feature")]
    [InlineData(WorkspaceFolderNameSeparator.Spaces, WorkspaceFolderNameCase.AsWritten, true, "JIRA-123 Add some Feature")]
    [InlineData(WorkspaceFolderNameSeparator.Hyphens, WorkspaceFolderNameCase.Lowercase, false, "add-some-feature")]
    [InlineData(WorkspaceFolderNameSeparator.Spaces, WorkspaceFolderNameCase.AsWritten, false, "Add some Feature")]
    public void Suggest_FollowsTheFormat(
        WorkspaceFolderNameSeparator separator,
        WorkspaceFolderNameCase letterCase,
        bool includeReference,
        string expected)
    {
        var format = new WorkspaceFolderNameFormat
        {
            Separator = separator,
            Case = letterCase,
            IncludeTaskReference = includeReference,
        };

        Assert.Equal(expected, WorkspaceFolderNames.Suggest([Task("JIRA-123", "Add some Feature")], format));
    }

    [Fact]
    public void Suggest_WithNoTasks_SuggestsNothing()
    {
        Assert.Null(WorkspaceFolderNames.Suggest([], Default));
    }

    [Fact]
    public void Suggest_WithSeveralTasks_SuggestsNothing()
    {
        Assert.Null(WorkspaceFolderNames.Suggest([Task("acme/api#1", "Fix login"), Task("ABC-2", "Add logout")], Default));
    }

    [Fact]
    public void Suggest_WithATitleOfOnlyPunctuation_UsesTheReference()
    {
        Assert.Equal("abc-2", WorkspaceFolderNames.Suggest([Task("ABC-2", "???")], Default));
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
        string? name = WorkspaceFolderNames.FromTitle(title, Default);

        Assert.Equal(expected, name);
        Assert.Null(WorkspaceFolderNames.Validate(name));
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
        string? name = WorkspaceFolderNames.FromTitle(title, AsWrittenWithSpaces);

        Assert.Equal(expected, name);
        Assert.Null(WorkspaceFolderNames.Validate(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("...")]
    public void FromTitle_WithNothingUsable_ReturnsNull(string? title)
    {
        Assert.Null(WorkspaceFolderNames.FromTitle(title, Default));
        Assert.Null(WorkspaceFolderNames.FromTitle(title, AsWrittenWithSpaces));
    }

    [Fact]
    public void FromTitle_WithLongTitle_CutsAtWord_KeepingTheReference()
    {
        string title = string.Join(' ', Enumerable.Repeat("word", 30));

        string? name = WorkspaceFolderNames.FromTitle(title, Default, "ABC-1");

        Assert.NotNull(name);
        Assert.True(name.Length <= WorkspaceFolderNames.MaxSuggestedLength);
        Assert.StartsWith("abc-1-word-", name);
        Assert.EndsWith("-word", name);
    }

    [Fact]
    public void FromTitle_WithLongWord_CutsWithinIt()
    {
        string name = WorkspaceFolderNames.FromTitle("short " + new string('x', 200), Default)!;

        Assert.Equal(WorkspaceFolderNames.MaxSuggestedLength, name.Length);
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
        Assert.Null(WorkspaceFolderNames.Validate(name));
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
        Assert.NotNull(WorkspaceFolderNames.Validate(name));
    }

    [Fact]
    public void Validate_RejectsNameTooLongForFolder()
    {
        Assert.Null(WorkspaceFolderNames.Validate(new string('a', 255)));
        Assert.NotNull(WorkspaceFolderNames.Validate(new string('a', 256)));

        // Linux limits names in bytes: 128 two-byte characters are 256 bytes.
        Assert.NotNull(WorkspaceFolderNames.Validate(new string('é', 128)));
    }

    private static TaskInfo Task(string key, string title) =>
        new("github", key, title, new Uri("https://example.com/" + Uri.EscapeDataString(key)));
}
