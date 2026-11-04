using TAMMi.Core.Catalogue;
using TAMMi.Core.Git;
using TAMMi.Core.Integrations;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

/// <summary>
/// The repository step of <see cref="WorkspaceDraft"/>: the chosen repositories' folder names and the branch name.
/// </summary>
public sealed class WorkspaceDraftRepositoriesTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "tammi-draft-tests");

    [Fact]
    public void AddRepository_SuggestsRepositoryName()
    {
        var draft = NewDraft();

        var api = draft.AddRepository(Repository("acme", "api"));

        Assert.NotNull(api);
        Assert.Equal("api", api.FolderName);
        Assert.False(api.IsFolderNameEdited);
        Assert.Null(api.FolderNameProblem);
        Assert.Equal("origin", api.Remote?.Name);
        Assert.Equal("acme", api.Owner);
    }

    [Fact]
    public void AddRepository_Twice_IsIgnored()
    {
        var draft = NewDraft();
        var repository = Repository("acme", "api");

        draft.AddRepository(repository);

        Assert.Null(draft.AddRepository(repository));
        Assert.Single(draft.Repositories);
    }

    [Fact]
    public void AddRepository_SameName_PrefixesBothOwners()
    {
        var draft = NewDraft();
        var acme = draft.AddRepository(Repository("acme", "api"))!;

        var other = draft.AddRepository(Repository("other", "api"))!;

        Assert.Equal("acme-api", acme.FolderName);
        Assert.Equal("other-api", other.FolderName);
    }

    [Fact]
    public void RemoveRepository_ClashGone_ReturnsToRepositoryName()
    {
        var draft = NewDraft();
        var acme = draft.AddRepository(Repository("acme", "api"))!;
        var other = Repository("other", "api");
        draft.AddRepository(other);

        Assert.True(draft.RemoveRepository(other));

        Assert.Equal("api", acme.FolderName);
    }

    [Fact]
    public void EditedFolderName_IsKept_AndOthersAvoidIt()
    {
        var draft = NewDraft();
        var web = draft.AddRepository(Repository("acme", "web"))!;
        var api = draft.AddRepository(Repository("acme", "api"))!;

        web.FolderName = "api";

        Assert.True(web.IsFolderNameEdited);
        Assert.Equal("api", web.FolderName);
        Assert.Equal("acme-api", api.FolderName);
        Assert.Null(web.FolderNameProblem);
        Assert.Null(api.FolderNameProblem);
    }

    [Fact]
    public void ClearedFolderName_FollowsSuggestionAtNextChange()
    {
        var draft = NewDraft();
        var api = draft.AddRepository(Repository("acme", "api"))!;
        api.FolderName = "server";

        api.FolderName = "";

        // Not filled in straight away, so the user can type over it.
        Assert.False(api.IsFolderNameEdited);
        Assert.Equal("", api.FolderName);
        Assert.NotNull(api.FolderNameProblem);

        draft.AddRepository(Repository("acme", "web"));
        Assert.Equal("api", api.FolderName);
    }

    [Fact]
    public void EditedFolderNamesThatClash_HaveProblems()
    {
        var draft = NewDraft();
        var api = draft.AddRepository(Repository("acme", "api"))!;
        var web = draft.AddRepository(Repository("acme", "web"))!;

        api.FolderName = "server";
        web.FolderName = "SERVER";

        Assert.NotNull(api.FolderNameProblem);
        Assert.NotNull(web.FolderNameProblem);
        Assert.False(draft.IsRepositoriesValid);
    }

    [Fact]
    public void Owner_WithoutRemotes_IsParentFolderName()
    {
        var draft = NewDraft();

        var api = draft.AddRepository(new CatalogueRepository { Path = Path.Combine(Root, "clones", "api") })!;

        Assert.Null(api.Remote);
        Assert.Equal("clones", api.Owner);
    }

    [Fact]
    public void Remote_PrefersOrigin_ThenFirst()
    {
        var draft = NewDraft();
        var withOrigin = draft.AddRepository(new CatalogueRepository
        {
            Path = Path.Combine(Root, "a", "api"),
            Remotes = [new GitRemote("fork", "git@github.com:me/api.git"), new GitRemote("origin", "git@github.com:acme/api.git")],
        })!;
        var withoutOrigin = draft.AddRepository(new CatalogueRepository
        {
            Path = Path.Combine(Root, "b", "web"),
            Remotes = [new GitRemote("fork", "git@github.com:me/web.git"), new GitRemote("upstream", "git@github.com:acme/web.git")],
        })!;

        Assert.Equal("origin", withOrigin.Remote?.Name);
        Assert.Equal("acme", withOrigin.Owner);
        Assert.Equal("fork", withoutOrigin.Remote?.Name);
        Assert.Equal("me", withoutOrigin.Owner);
    }

    [Fact]
    public void BranchName_OneTask_FollowsTask()
    {
        var draft = NewDraft();

        draft.AddTask(Task("ABC-123", "Add some feature"));

        Assert.Equal("ABC-123-add-some-feature", draft.BranchName);
        Assert.False(draft.IsBranchNameEdited);
    }

    [Fact]
    public void BranchName_NoTasks_FollowsName()
    {
        var draft = NewDraft();

        draft.Name = "Spring Clean";

        Assert.Equal("spring-clean", draft.BranchName);
    }

    [Fact]
    public void BranchName_SeveralTasks_FollowsName()
    {
        var draft = NewDraft();
        draft.AddTask(Task("ABC-1", "One"));
        draft.AddTask(Task("ABC-2", "Two"));

        draft.Name = "Both Things";

        Assert.Equal("both-things", draft.BranchName);
    }

    [Fact]
    public void BranchName_Edited_StopsFollowing()
    {
        var draft = NewDraft();
        draft.BranchName = "my-branch";

        draft.AddTask(Task("ABC-1", "One"));
        draft.Name = "Other";

        Assert.True(draft.IsBranchNameEdited);
        Assert.Equal("my-branch", draft.BranchName);
    }

    [Fact]
    public void BranchName_Cleared_FollowsAgainAtNextChange()
    {
        var draft = NewDraft();
        draft.BranchName = "my-branch";

        draft.BranchName = "";
        Assert.Equal("", draft.BranchName);
        Assert.NotNull(draft.BranchNameProblem);

        draft.AddTask(Task("ABC-1", "One"));
        Assert.Equal("ABC-1-one", draft.BranchName);
    }

    [Fact]
    public void IsRepositoriesValid_NeedsBaseBranch()
    {
        var draft = NewDraft();
        draft.Name = "Work";
        var api = draft.AddRepository(Repository("acme", "api"))!;

        Assert.False(draft.IsRepositoriesValid);

        api.BaseBranch = "origin/main";
        Assert.True(draft.IsRepositoriesValid);
    }

    [Fact]
    public void ToCreationRequest_HoldsTheChoices()
    {
        var draft = NewDraft();
        draft.AddTask(Task("ABC-1", "One"));
        draft.Name = " Work ";
        var api = draft.AddRepository(Repository("acme", "api"))!;
        api.FolderName = " server ";
        api.BaseBranch = " origin/develop ";

        var request = draft.ToCreationRequest();

        Assert.Equal("Work", request.Name);
        Assert.Equal(Path.Combine(Root, "Work"), request.Folder);
        Assert.Equal([new TaskReference("source", "ABC-1")], request.Tasks);
        Assert.Equal("ABC-1-one", request.BranchName);
        var repository = Assert.Single(request.Repositories);
        Assert.Equal(new WorkspaceCreationRepository("server", api.Repository.Path, "origin/develop", "origin"), repository);
    }

    [Fact]
    public void ChooseTaskRepositories_ChoosesCloneWithMatchingRemote()
    {
        var draft = NewDraft();
        draft.AddTask(GitHubTask("acme", "api", 42));
        var catalogue = new[] { Repository("acme", "web"), Repository("acme", "api"), Repository("other", "api") };

        var added = draft.ChooseTaskRepositories(catalogue);

        var api = Assert.Single(added);
        Assert.Same(catalogue[1], api.Repository);
        Assert.Equal([new TaskReference("github", "acme/api#42")], api.ChosenForTasks);
        Assert.Equal([api], draft.Repositories);
        Assert.Equal("api", api.FolderName);
    }

    [Fact]
    public void ChooseTaskRepositories_NoMatchOrNoRepository_ChoosesNothing()
    {
        var draft = NewDraft();
        draft.AddTask(GitHubTask("acme", "missing", 1));
        draft.AddTask(Task("ABC-1", "A JIRA issue"));

        Assert.Empty(draft.ChooseTaskRepositories([Repository("acme", "api")]));
        Assert.Empty(draft.Repositories);
    }

    [Fact]
    public void ChooseTaskRepositories_TwoTasksInOneRepository_ChoosesItOnce()
    {
        var draft = NewDraft();
        draft.AddTask(GitHubTask("acme", "api", 1));
        draft.AddTask(GitHubTask("acme", "api", 2));

        var api = Assert.Single(draft.ChooseTaskRepositories([Repository("acme", "api")]));

        Assert.Equal(2, api.ChosenForTasks.Count);
    }

    [Fact]
    public void ChooseTaskRepositories_TasksInSeveralRepositories_ChoosesEach()
    {
        var draft = NewDraft();
        draft.AddTask(GitHubTask("acme", "api", 1));
        draft.AddTask(GitHubTask("other", "api", 2));

        draft.ChooseTaskRepositories([Repository("acme", "api"), Repository("other", "api")]);

        Assert.Equal(["acme-api", "other-api"], draft.Repositories.Select(repository => repository.FolderName));
    }

    [Fact]
    public void ChooseTaskRepositories_PrefersCloneWhoseFetchedRemoteMatches()
    {
        var draft = NewDraft();
        draft.AddTask(GitHubTask("acme", "api", 1));
        // A clone of a fork, with the repository as its upstream, and a clone of the repository itself.
        var fork = new CatalogueRepository
        {
            Path = Path.Combine(Root, "forks", "api"),
            Remotes =
            [
                new GitRemote("origin", "git@github.com:me/api.git"),
                new GitRemote("upstream", "https://github.com/acme/api.git"),
            ],
        };
        var clone = Repository("acme", "api");

        var chosen = Assert.Single(draft.ChooseTaskRepositories([fork, clone]));

        Assert.Same(clone, chosen.Repository);
    }

    [Fact]
    public void ChooseTaskRepositories_AlreadyChosen_AddsNothing()
    {
        var draft = NewDraft();
        var api = Repository("acme", "api");
        var chosen = draft.AddRepository(api)!;
        draft.AddTask(GitHubTask("acme", "api", 1));

        Assert.Empty(draft.ChooseTaskRepositories([api]));
        Assert.Equal([chosen], draft.Repositories);
        Assert.Empty(chosen.ChosenForTasks);
    }

    [Fact]
    public void ChooseTaskRepositories_RemovedByUser_IsNotChosenAgain()
    {
        var draft = NewDraft();
        var api = Repository("acme", "api");
        draft.AddTask(GitHubTask("acme", "api", 1));
        draft.ChooseTaskRepositories([api]);

        draft.RemoveRepository(api);

        Assert.Empty(draft.ChooseTaskRepositories([api]));
        Assert.Empty(draft.Repositories);
    }

    [Fact]
    public void ChooseTaskRepositories_TaskRemoved_RemovesRepositoryChosenForIt()
    {
        var draft = NewDraft();
        var api = Repository("acme", "api");
        var web = Repository("acme", "web");
        var apiTask = GitHubTask("acme", "api", 1);
        draft.AddTask(apiTask);
        draft.AddTask(GitHubTask("acme", "web", 2));
        draft.ChooseTaskRepositories([api, web]);
        var other = draft.AddRepository(Repository("other", "lib"))!;

        draft.RemoveTask(apiTask.Reference);
        draft.ChooseTaskRepositories([api, web]);

        Assert.Equal([web, other.Repository], draft.Repositories.Select(repository => repository.Repository));
    }

    [Fact]
    public void ChooseTaskRepositories_TaskRemovedAndAddedAgain_ChoosesAgain()
    {
        var draft = NewDraft();
        var api = Repository("acme", "api");
        var task = GitHubTask("acme", "api", 1);
        draft.AddTask(task);
        draft.ChooseTaskRepositories([api]);
        draft.RemoveTask(task.Reference);
        draft.ChooseTaskRepositories([api]);

        draft.AddTask(task);

        Assert.Single(draft.ChooseTaskRepositories([api]));
    }

    private static TaskInfo GitHubTask(string owner, string repository, int number) =>
        new("github", $"{owner}/{repository}#{number}", "Issue", new Uri($"https://github.com/{owner}/{repository}/issues/{number}"))
        {
            RepositoryUrl = new Uri($"https://github.com/{owner}/{repository}"),
        };

    private static WorkspaceDraft NewDraft() => new(Root);

    private static CatalogueRepository Repository(string owner, string name) => new()
    {
        Path = Path.Combine(Root, "clones", owner, name),
        Remotes = [new GitRemote("origin", $"git@github.com:{owner}/{name}.git")],
    };

    private static TaskInfo Task(string key, string title) =>
        new("source", key, title, new Uri("https://example.com/task"));
}
