using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceDraftTests : IDisposable
{
    // Folder names suggested from the title alone, as written, so the tests can check which task they follow.
    private static readonly WorkspaceFolderNameFormat TitleOnly = new()
    {
        Separator = WorkspaceFolderNameSeparator.Spaces,
        Case = WorkspaceFolderNameCase.AsWritten,
        IncludeTaskReference = false,
    };

    // Names suggested from the title alone, likewise.
    private const string TitleTemplate = "{TaskTitle}";

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
    public void Constructor_StartsWithParentFolderAndNoTasksOrNames()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);

        Assert.Equal(_root, draft.ParentFolder);
        Assert.Empty(draft.Tasks);
        Assert.Equal("", draft.Name);
        Assert.False(draft.IsNameEdited);
        Assert.Equal("", draft.FolderName);
        Assert.False(draft.IsFolderNameEdited);
    }

    [Fact]
    public void AddTask_WithOneTask_DefaultsNameToItsTitle()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);

        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        Assert.Equal("Fix login", draft.Name);
        Assert.False(draft.IsNameEdited);
    }

    [Fact]
    public void AddTask_WithoutAFormatOrTemplate_SuggestsTheDefaults()
    {
        var draft = new WorkspaceDraft(_root);

        draft.AddTask(Task("jira", "JIRA-123", "Add some feature"));

        Assert.Equal("JIRA-123 Add some feature", draft.Name);
        Assert.Equal("jira-123-add-some-feature", draft.FolderName);
        Assert.Equal(Path.Combine(_root, "jira-123-add-some-feature"), draft.Folder);
    }

    [Fact]
    public void AddTask_SuggestsTheNameAndFolderNameSeparately()
    {
        var draft = new WorkspaceDraft(_root, new WorkspaceFolderNameFormat(), "{RepositoryName}: {TaskTitle}");

        draft.AddTask(Task("github", "acme/api#1", "Fix login") with { RepositoryUrl = new Uri("https://github.com/acme/api") });

        Assert.Equal("api: Fix login", draft.Name);
        Assert.Equal("api-1-fix-login", draft.FolderName);
        Assert.False(draft.IsNameEdited);
        Assert.False(draft.IsFolderNameEdited);
    }

    [Fact]
    public void AddTask_TemplateCantBeFilledIn_NameFollowsTheFolderName()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, "{RepositoryName} {TaskTitle}");

        draft.AddTask(Task("jira", "ABC-1", "Add logout"));

        Assert.Equal("Add logout", draft.FolderName);
        Assert.Equal("Add logout", draft.Name);
    }

    [Fact]
    public void FolderName_EnteredWithSeveralTasks_IsFollowedByTheName()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));
        draft.AddTask(Task("jira", "ABC-2", "Add logout"));

        draft.FolderName = "login-and-logout";

        Assert.Equal("login-and-logout", draft.Name);
        Assert.False(draft.IsNameEdited);
        Assert.True(draft.IsFolderNameEdited);
    }

    [Fact]
    public void Name_EnteredWithNoTasks_IsFollowedByTheFolderName_InTheFolderNameFormat()
    {
        var draft = new WorkspaceDraft(_root, new WorkspaceFolderNameFormat(), TitleTemplate);

        draft.Name = "Try out: the new API";

        Assert.Equal("try-out-the-new-api", draft.FolderName);
        Assert.False(draft.IsFolderNameEdited);
        Assert.Equal("try-out-the-new-api", draft.BranchName);
    }

    [Fact]
    public void Name_Entered_DoesntChangeAFolderNameSuggestedFromTheTask()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        draft.Name = "Login work";

        Assert.Equal("Fix login", draft.FolderName);
    }

    [Fact]
    public void FolderName_Entered_DoesntChangeAnEnteredName()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "Login work" };

        draft.FolderName = "login";

        Assert.Equal("Login work", draft.Name);
        Assert.Equal(Path.Combine(_root, "login"), draft.Folder);
    }

    [Fact]
    public void FolderName_Cleared_FollowsTheTasksAgainWhenTheyChange()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));
        draft.FolderName = "login";

        draft.FolderName = "";

        Assert.Equal("", draft.FolderName);
        Assert.False(draft.IsFolderNameEdited);

        draft.AddTask(Task("jira", "ABC-2", "Add logout"));
        draft.RemoveTask(new TaskReference("github", "acme/api#1"));

        Assert.Equal("Add logout", draft.FolderName);
    }

    [Fact]
    public void AddTask_WithSecondTask_ClearsDefaultName()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        draft.AddTask(Task("jira", "ABC-2", "Add logout"));

        Assert.Equal("", draft.Name);
    }

    [Fact]
    public void RemoveTask_BackToOneTask_DefaultsNameToItsTitle()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));
        draft.AddTask(Task("jira", "ABC-2", "Add logout"));

        Assert.True(draft.RemoveTask(new TaskReference("github", "acme/api#1")));

        Assert.Equal("Add logout", draft.Name);
    }

    [Fact]
    public void AddTask_AfterNameEdited_KeepsName()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
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
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
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
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("github", "acme/api#1", "Fix login"));

        Assert.False(draft.AddTask(Task("github", "acme/api#1", "Fix login (renamed)")));

        Assert.Single(draft.Tasks);
        Assert.Equal("Fix login", draft.Name);
    }

    [Fact]
    public void AddTask_SameKeyFromAnotherSource_IsAdded()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);
        draft.AddTask(Task("jira-a.example", "ABC-1", "One"));

        Assert.True(draft.AddTask(Task("jira-b.example", "ABC-1", "Other")));

        Assert.Equal(2, draft.Tasks.Count);
        Assert.True(draft.ContainsTask(new TaskReference("jira-b.example", "ABC-1")));
    }

    [Fact]
    public void RemoveTask_NotChosen_ReturnsFalse()
    {
        Assert.False(new WorkspaceDraft(_root, TitleOnly, TitleTemplate).RemoveTask(new TaskReference("github", "acme/api#1")));
    }

    [Fact]
    public void Folder_IsFolderNameInParentFolder()
    {
        var draft = new WorkspaceDraft(_root + Path.DirectorySeparatorChar) { FolderName = "  Fix login  " };

        Assert.Equal(Path.Combine(_root, "Fix login"), draft.Folder);
        Assert.True(draft.IsLocationValid);
        Assert.Null(draft.NameProblem);
        Assert.Null(draft.FolderNameProblem);
        Assert.Null(draft.ParentFolderProblem);
        Assert.Null(draft.FolderProblem);
    }

    [Fact]
    public void Folder_WithInvalidFolderName_IsNullAndReportsIt()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "Work", FolderName = "a/b" };

        Assert.Null(draft.Folder);
        Assert.NotNull(draft.FolderNameProblem);
        Assert.Null(draft.NameProblem);
        Assert.Null(draft.FolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void Name_CanHaveCharactersAFolderNameCant()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "api/web: fix?", FolderName = "fix" };

        Assert.Null(draft.NameProblem);
        Assert.True(draft.IsLocationValid);
        Assert.Equal("api/web: fix?", draft.ToCreationRequest().Name);
    }

    [Fact]
    public void NameProblem_WithOnlyAFolderNameEnteredAndThenTheNameCleared_AsksForOne()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "Work", FolderName = "work" };

        draft.Name = "";

        Assert.NotNull(draft.NameProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void NameProblem_WithoutName_AsksForOne()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate);

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
        Assert.False(new WorkspaceDraft(_root, TitleOnly, TitleTemplate).ParentFolderIsNew);
    }

    [Fact]
    public void FolderProblem_WhenFolderExists_IsReported()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Fix login"));

        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "Fix login" };

        Assert.NotNull(draft.FolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void FolderProblem_WhenFileHasTheName_IsReported()
    {
        File.WriteAllText(Path.Combine(_root, "Fix login"), "");

        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "Fix login" };

        Assert.NotNull(draft.FolderProblem);
        Assert.False(draft.IsLocationValid);
    }

    [Fact]
    public void FolderProblem_SeesFolderCreatedAfterCheck()
    {
        var draft = new WorkspaceDraft(_root, TitleOnly, TitleTemplate) { Name = "Fix login" };
        Assert.True(draft.IsLocationValid);

        Directory.CreateDirectory(Path.Combine(_root, "Fix login"));

        Assert.False(draft.IsLocationValid);
    }

    private static TaskInfo Task(string source, string key, string title) =>
        new(source, key, title, new Uri("https://example.com/" + Uri.EscapeDataString(key)));
}
