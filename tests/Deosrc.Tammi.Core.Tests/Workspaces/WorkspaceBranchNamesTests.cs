using Tammi.Core.Integrations;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceBranchNamesTests
{
    [Fact]
    public void Suggest_JiraTask_KeepsKeyCaseAndLowercasesTitle()
    {
        Assert.Equal("ABC-123-add-some-feature", WorkspaceBranchNames.Suggest([Task("ABC-123", "Add some feature")], null));
    }

    [Fact]
    public void Suggest_GitHubTask_UsesShortReference()
    {
        Assert.Equal("api-42-fix-login", WorkspaceBranchNames.Suggest([Task("acme/api#42", "Fix login")], "Ignored"));
    }

    [Fact]
    public void Suggest_LeavesOutPunctuation()
    {
        Assert.Equal(
            "ABC-1-dont-crash-on-empty-lists",
            WorkspaceBranchNames.Suggest([Task("ABC-1", "Don't crash: on [empty] lists!")], null));
    }

    [Fact]
    public void Suggest_LongTitle_IsCutAtAWord()
    {
        string title = string.Join(' ', Enumerable.Repeat("word", 40));

        string? name = WorkspaceBranchNames.Suggest([Task("ABC-1", title)], null);

        Assert.NotNull(name);
        Assert.True(name.Length <= WorkspaceNames.MaxSuggestedLength);
        Assert.StartsWith("ABC-1-word-", name);
        Assert.EndsWith("-word", name);
    }

    [Fact]
    public void Suggest_NoTasks_UsesWorkspaceName()
    {
        Assert.Equal("my-cool-workspace", WorkspaceBranchNames.Suggest([], " My Cool  Workspace "));
    }

    [Fact]
    public void Suggest_SeveralTasks_UsesWorkspaceName()
    {
        Assert.Equal(
            "release-2-0",
            WorkspaceBranchNames.Suggest([Task("ABC-1", "One"), Task("ABC-2", "Two")], "Release 2.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!!")]
    public void Suggest_NoTasksAndNoUsableName_IsNull(string? name)
    {
        Assert.Null(WorkspaceBranchNames.Suggest([], name));
    }

    private static TaskInfo Task(string key, string title) =>
        new("source", key, title, new Uri("https://example.com/task"));
}
