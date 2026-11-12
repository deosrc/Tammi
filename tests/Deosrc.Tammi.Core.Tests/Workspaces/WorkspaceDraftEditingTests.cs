using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

/// <summary>
/// <see cref="WorkspaceDraft.ForEditing"/>: changing the tasks and adding repositories to an existing workspace.
/// </summary>
public sealed class WorkspaceDraftEditingTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "tammi-draft-edit-tests");

    [Fact]
    public void ForEditing_KeepsNameFolderTasksAndBranch()
    {
        var draft = WorkspaceDraft.ForEditing(Workspace(), [Task("ABC-1")]);

        Assert.True(draft.IsEditing);
        Assert.Equal("Fix login", draft.Name);
        Assert.Equal("fix-login", draft.FolderName);
        Assert.Equal([new TaskReference("source", "ABC-1")], draft.Tasks.Select(task => task.Reference));
        Assert.Equal("my-branch", draft.BranchName);
        Assert.True(draft.IsBranchFixed);
    }

    [Fact]
    public void ExistingRepository_CountsAsChosen_ButCannotBeAddedAgain()
    {
        var draft = WorkspaceDraft.ForEditing(Workspace(), []);
        var api = Repository("acme", "api");

        Assert.True(draft.ContainsRepository(api));
        Assert.True(draft.IsExistingRepository(api));
        Assert.Null(draft.AddRepository(api));
        Assert.Empty(draft.Repositories);
    }

    [Fact]
    public void AddRepository_FolderNameAvoidsExistingFolders()
    {
        var draft = WorkspaceDraft.ForEditing(Workspace(), []);

        var added = draft.AddRepository(Repository("other", "api2"));
        Assert.NotNull(added);
        added.FolderName = "API";

        Assert.NotNull(added.FolderNameProblem);
    }

    [Fact]
    public void AddRepository_SameNameAsExisting_GetsOwnerPrefix()
    {
        var draft = WorkspaceDraft.ForEditing(Workspace(), []);

        var added = draft.AddRepository(Repository("other", "api") with
        {
            Path = Path.Combine(Root, "clones", "other", "api"),
        });

        Assert.Equal("other-api", added!.FolderName);
    }

    [Fact]
    public void ChooseTaskRepositories_DoesNotChooseForExistingTasks()
    {
        var task = Task("acme/web#1") with { RepositoryUrl = new Uri("https://github.com/acme/web") };
        var draft = WorkspaceDraft.ForEditing(Workspace(), [task]);
        var web = Repository("acme", "web");

        var added = draft.ChooseTaskRepositories([web]);

        Assert.Empty(added);
        Assert.Empty(draft.Repositories);
    }

    [Fact]
    public void ChooseTaskRepositories_ChoosesForTaskAddedWhileEditing()
    {
        var draft = WorkspaceDraft.ForEditing(Workspace(), []);
        draft.AddTask(Task("acme/web#1") with { RepositoryUrl = new Uri("https://github.com/acme/web") });

        var added = draft.ChooseTaskRepositories([Repository("acme", "web")]);

        Assert.Single(added);
    }

    [Fact]
    public void ToEditRequest_HasTasksBranchAndAddedRepositories()
    {
        var draft = WorkspaceDraft.ForEditing(Workspace(), [Task("ABC-1")]);
        draft.AddTask(Task("ABC-2"));
        var web = draft.AddRepository(Repository("acme", "web"))!;
        web.MergeTarget = "origin/main";

        var request = draft.ToEditRequest();

        Assert.Equal(
            [new TaskReference("source", "ABC-1"), new TaskReference("source", "ABC-2")], request.Tasks);
        Assert.Equal("my-branch", request.BranchName);
        var added = Assert.Single(request.AddedRepositories);
        Assert.Equal("web", added.FolderName);
        Assert.Equal("origin/main", added.MergeTarget);
    }

    [Fact]
    public void IsRepositoriesValid_NoBranchAndNothingAdded_IsValid()
    {
        var draft = WorkspaceDraft.ForEditing(
            new Workspace { Id = "id", Name = "Fix login", Folder = Path.Combine(Root, "workspaces", "fix-login") }, []);
        draft.BranchName = "";

        Assert.True(draft.IsRepositoriesValid);
    }

    [Fact]
    public void ToEditRequest_NewDraft_Throws() =>
        Assert.Throws<InvalidOperationException>(() => new WorkspaceDraft(Root).ToEditRequest());

    private static Workspace Workspace() => new()
    {
        Id = "id",
        Name = "Fix login",
        Folder = Path.Combine(Root, "workspaces", "fix-login"),
        Repositories = [new WorkspaceRepository("api", Path.Combine(Root, "clones", "acme", "api"), "my-branch", "origin/main")],
    };

    private static CatalogueRepository Repository(string owner, string name) => new()
    {
        Path = Path.Combine(Root, "clones", owner, name),
        Remotes = [new GitRemote("origin", $"git@github.com:{owner}/{name}.git")],
    };

    private static TaskInfo Task(string key) => new("source", key, key, new Uri("https://example.com/task"));
}
