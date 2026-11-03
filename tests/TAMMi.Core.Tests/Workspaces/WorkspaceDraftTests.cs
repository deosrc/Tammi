using TAMMi.Core.Integrations;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

public sealed class WorkspaceDraftTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    public WorkspaceDraftTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_StartsWithParentFolderAndNoTasksOrName()
    {
        var draft = new WorkspaceDraft(_root);

        Assert.Equal(_root, draft.ParentFolder);
        Assert.Empty(draft.Tasks);
        Assert.Equal("", draft.Name);
        Assert.False(draft.IsNameEdited);
    }

    [Fact]
    public void AddTask_WithOneTask_DefaultsNameToItsTitle()
    {
        var draft = new WorkspaceDraft(_root);

        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        Assert.Equal("Fix login", draft.Name);
        Assert.False(draft.IsNameEdited);
    }

    [Fact]
    public void AddTask_WithSecondTask_ClearsDefaultName()
    {
        var draft = new WorkspaceDraft(_root);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        draft.AddTask(Task("jira", "ABC-2", "Add logout"));

        Assert.Equal("", draft.Name);
    }

    [Fact]
    public void RemoveTask_BackToOneTask_DefaultsNameToItsTitle()
    {
        var draft = new WorkspaceDraft(_root);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));
        draft.AddTask(Task("jira", "ABC-2", "Add logout"));

        Assert.True(draft.RemoveTask(new TaskReference("github", "acme/api#1")));

        Assert.Equal("Add logout", draft.Name);
    }

    [Fact]
    public void AddTask_AfterNameEdited_KeepsName()
    {
        var draft = new WorkspaceDraft(_root);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));
        draft.Name = "Login work";

        draft.RemoveTask(new TaskReference("github", "acme/api#1"));
        draft.AddTask(Task("jira", "ABC-2", "Add logout"));

        Assert.Equal("Login work", draft.Name);
        Assert.True(draft.IsNameEdited);
    }

    [Fact]
    public void Name_Cleared_FollowsTasksAgainWhenTheyChange()
    {
        var draft = new WorkspaceDraft(_root);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));
        draft.Name = "Login work";

        draft.Name = "";

        // The user may be about to type a new name, so it isn't filled in straight away.
        Assert.Equal("", draft.Name);
        Assert.False(draft.IsNameEdited);

        draft.AddTask(Task("jira", "ABC-2", "Add logout"));
        draft.RemoveTask(new TaskReference("github", "acme/api#1"));

        Assert.Equal("Add logout", draft.Name);
    }

    [Fact]
    public void AddTask_AlreadyChosen_IsIgnored()
    {
        var draft = new WorkspaceDraft(_root);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        Assert.False(draft.AddTask(Task("github", "acme/api#1", "Fix login (renamed)")));

        Assert.Single(draft.Tasks);
        Assert.Equal("Fix login", draft.Name);
    }

    [Fact]
    public void AddTask_SameKeyFromAnotherSource_IsAdded()
    {
        var draft = new WorkspaceDraft(_root);
        draft.AddTask(Task("jira-a.example", "ABC-1", "One"));

        Assert.True(draft.AddTask(Task("jira-b.example", "ABC-1", "Other")));

        Assert.Equal(2, draft.Tasks.Count);
        Assert.True(draft.ContainsTask(new TaskReference("jira-b.example", "ABC-1")));
    }

    [Fact]
    public void RemoveTask_NotChosen_ReturnsFalse()
    {
        Assert.False(new WorkspaceDraft(_root).RemoveTask(new TaskReference("github", "acme/api#1")));
    }

    [Fact]
    public void Folder_IsNameInParentFolder()
    {
        var draft = new WorkspaceDraft(_root + Path.DirectorySeparatorChar) { Name = "  Fix login  " };

        Assert.Equal(Path.Combine(_root, "Fix login"), draft.Folder);
        Assert.True(draft.IsLocationValid);
        Assert.Null(draft.NameProblem);
        Assert.Null(draft.ParentFolderProblem);
        Assert.Null(draft.FolderProblem);
    }

    [Fact]
    public void Folder_WithInvalidName_IsNullAndReportsName()
    {
        var draft = new WorkspaceDraft(_root) { Name = "a/b" };

        Assert.Null(draft.Folder);
        Assert.NotNull(draft.NameProblem);
        Assert.Null(draft.FolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void NameProblem_WithoutName_AsksForOne()
    {
        var draft = new WorkspaceDraft(_root);

        Assert.NotNull(draft.NameProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/folder")]
    public void ParentFolderProblem_WithBlankOrRelativeFolder_IsReported(string parent)
    {
        var draft = new WorkspaceDraft(parent) { Name = "Fix login" };

        Assert.NotNull(draft.ParentFolderProblem);
        Assert.Null(draft.Folder);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void ParentFolderProblem_WithFile_IsReported()
    {
        string file = Path.Combine(_root, "file");
        File.WriteAllText(file, "");

        var draft = new WorkspaceDraft(file) { Name = "Fix login" };

        Assert.NotNull(draft.ParentFolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void ParentFolder_Missing_IsValidAndNew()
    {
        var draft = new WorkspaceDraft(Path.Combine(_root, "not", "yet")) { Name = "Fix login" };

        Assert.Null(draft.ParentFolderProblem);
        Assert.True(draft.ParentFolderIsNew);
        Assert.True(draft.IsLocationValid);
    }

    [Fact]
    public void ParentFolderIsNew_WithExistingFolder_IsFalse()
    {
        Assert.False(new WorkspaceDraft(_root).ParentFolderIsNew);
    }

    [Fact]
    public void FolderProblem_WhenFolderExists_IsReported()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Fix login"));

        var draft = new WorkspaceDraft(_root) { Name = "Fix login" };

        Assert.NotNull(draft.FolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void FolderProblem_WhenFileHasTheName_IsReported()
    {
        File.WriteAllText(Path.Combine(_root, "Fix login"), "");

        var draft = new WorkspaceDraft(_root) { Name = "Fix login" };

        Assert.NotNull(draft.FolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void FolderProblem_SeesFolderCreatedAfterCheck()
    {
        var draft = new WorkspaceDraft(_root) { Name = "Fix login" };
        Assert.True(draft.IsLocationValid);

        Directory.CreateDirectory(Path.Combine(_root, "Fix login"));

        Assert.False(draft.IsLocationValid);
    }

    private static TaskInfo Task(string source, string key, string title) =>
        new(source, key, title, new Uri("https://example.com/" + Uri.EscapeDataString(key)));
}
