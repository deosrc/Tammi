using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceNamesTests
{
    private static readonly TaskInfo GitHubTask =
        new("github", "acme/api#42", "Fix the login page", new Uri("https://github.com/acme/api/issues/42"))
        {
            RepositoryUrl = new Uri("https://github.com/acme/api"),
        };

    private static readonly TaskInfo JiraTask =
        new("jira", "ABC-123", "Add some feature", new Uri("https://example.atlassian.net/browse/ABC-123"));

    [Fact]
    public void Suggest_WithTheDefaultTemplate_IsTheReferenceAndTitle()
    {
        Assert.Equal("api#42 Fix the login page", WorkspaceNames.Suggest([GitHubTask], GeneralSettings.DefaultWorkspaceNameTemplate));
        Assert.Equal("ABC-123 Add some feature", WorkspaceNames.Suggest([JiraTask], GeneralSettings.DefaultWorkspaceNameTemplate));
    }

    [Theory]
    [InlineData("{TaskKey}", "acme/api#42")]
    [InlineData("{TaskReference}", "api#42")]
    [InlineData("{TaskTitle}", "Fix the login page")]
    [InlineData("{RepositoryOwner}/{RepositoryName}: {TaskTitle}", "acme/api: Fix the login page")]
    [InlineData("{taskreference} - {TASKTITLE}", "api#42 - Fix the login page")]
    [InlineData("Login", "Login")]
    public void Suggest_FillsInEachPlaceholder_IgnoringCase(string template, string expected)
    {
        Assert.Equal(expected, WorkspaceNames.Suggest([GitHubTask], template));
    }

    [Fact]
    public void Suggest_LeavesUnknownTextInBracesAsItIs()
    {
        Assert.Equal("{Team} api#42", WorkspaceNames.Suggest([GitHubTask], "{Team} {TaskReference}"));
    }

    [Theory]
    [InlineData("{RepositoryName} {TaskTitle}")]
    [InlineData("{RepositoryOwner}")]
    public void Suggest_WithAPlaceholderTheTaskHasNoValueFor_SuggestsNothing(string template)
    {
        Assert.Null(WorkspaceNames.Suggest([JiraTask], template));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Suggest_WithABlankTemplate_SuggestsNothing(string? template)
    {
        Assert.Null(WorkspaceNames.Suggest([GitHubTask], template));
    }

    [Fact]
    public void Suggest_WithNoTasksOrSeveral_SuggestsNothing()
    {
        Assert.Null(WorkspaceNames.Suggest([], "{TaskTitle}"));
        Assert.Null(WorkspaceNames.Suggest([GitHubTask, JiraTask], "{TaskTitle}"));
        Assert.Null(WorkspaceNames.Suggest([GitHubTask, JiraTask], "Fixed text"));
    }

    [Fact]
    public void Suggest_CollapsesLineBreaksAndRunsOfSpaces()
    {
        var task = JiraTask with { Title = "  Add\r\nsome \t feature  " };

        Assert.Equal("ABC-123 Add some feature", WorkspaceNames.Suggest([task], " {TaskReference}   {TaskTitle} "));
    }

    [Fact]
    public void FindUnknown_ListsTextInBracesThatIsntAPlaceholder()
    {
        Assert.Equal(["TaskSummary", "x"], WorkspaceNames.FindUnknown("{TaskSummary} {TaskTitle} {x} {x}"));
        Assert.Empty(WorkspaceNames.FindUnknown(null));
    }

    [Theory]
    [InlineData("api#42: fix / login?")]
    [InlineData("  CON  ")]
    [InlineData("ends with a full stop.")]
    public void Validate_AllowsWhatAFolderNameCant(string name)
    {
        Assert.Null(WorkspaceNames.Validate(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    [InlineData("a\ttab")]
    public void Validate_RejectsBlankNamesAndControlCharacters(string? name)
    {
        Assert.NotNull(WorkspaceNames.Validate(name));
    }
}
