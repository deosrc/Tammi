using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Tests.Git;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceCreatorTests : IDisposable
{
    private const string Branch = "ABC-1-fix-login";

    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();
    private readonly GitWorktreeService _worktrees;
    private readonly WorkspaceStore _store;
    private readonly WorkspaceCreator _creator;

    public WorkspaceCreatorTests()
    {
        _worktrees = new GitWorktreeService(_repos.Git, _reader);
        _store = new WorkspaceStore(_repos.PathTo("data"), [], []);
        _creator = new WorkspaceCreator(_store, _worktrees);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Its parent folder doesn't exist yet, so creating the workspace creates it too.
    private string Folder => _repos.PathTo("workspaces/Fix login");

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Create_SeveralRepositories_CreatesWorktreesMetadataAndIndexEntry()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string app = await _repos.CloneAsync(await _repos.CreateRemoteWithSubmoduleAsync(), "clones/app");
        var progress = new RecordingProgress();

        var result = await _creator.CreateAsync(
            Request(
                new WorkspaceCreationRepository("api", api, "origin/main", "origin"),
                new WorkspaceCreationRepository("app", app, "origin/main", "origin")),
            progress,
            Token);

        Assert.Empty(result.Warnings);
        var workspace = result.Workspace;
        Assert.Equal("Fix login", workspace.Name);
        Assert.Equal(Folder, workspace.Folder);
        Assert.Equal([new TaskReference("jira", "ABC-1")], workspace.Tasks);
        Assert.Equal(
            [
                new WorkspaceRepository("api", api, Branch, "origin/main"),
                new WorkspaceRepository("app", app, Branch, "origin/main"),
            ],
            workspace.Repositories);

        foreach (var repository in workspace.Repositories)
        {
            string worktree = workspace.GetWorktreePath(repository);
            Assert.Equal(WorktreeHealth.Healthy, _worktrees.CheckWorktree(worktree).Health);
            var status = _reader.ReadStatus(worktree);
            Assert.Equal(Branch, status.Branch);
            Assert.Null(status.Upstream);
        }

        // Submodules are checked out.
        Assert.True(File.Exists(Path.Combine(Folder, "app", "lib", "README.md")));

        // Listed on Home, and readable from the folder.
        var listing = Assert.Single(await _store.ListAsync(Token));
        Assert.Equal(WorkspaceAvailability.Available, listing.Availability);
        Assert.Equal(workspace.Id, listing.Id);
        var loaded = await _store.LoadAsync(Folder, Token);
        Assert.Equal(workspace.Id, loaded.Id);
        Assert.Equal(workspace.Tasks, loaded.Tasks);
        Assert.Equal(workspace.Repositories, loaded.Repositories);

        var last = progress.Updates[^1];
        Assert.Equal(last.TotalSteps, last.CompletedSteps);
        Assert.Contains(progress.Updates, update => update.Message.Contains("Fetching app", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_NoRepositories_CreatesFolderAndMetadata()
    {
        var result = await _creator.CreateAsync(Request(), cancellationToken: Token);

        Assert.Empty(result.Workspace.Repositories);
        Assert.True(File.Exists(WorkspaceStore.GetMetadataFilePath(Folder)));
    }

    [Fact]
    public async Task Create_ChosenBase_BranchesFromIt()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string seed = _repos.PathTo("api-seed");
        await _repos.RunAsync(seed, "switch", "-c", "develop");
        await _repos.CommitFileAsync(seed, "develop.txt", "develop", "Develop commit");
        await _repos.RunAsync(seed, "push", "origin", "develop");
        string api = await _repos.CloneAsync(remote, "clones/api");

        await _creator.CreateAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/develop", "origin")),
            cancellationToken: Token);

        Assert.True(File.Exists(Path.Combine(Folder, "api", "develop.txt")));
    }

    [Fact]
    public async Task Create_FetchesBeforeBranching()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string seed = _repos.PathTo("api-seed");
        await _repos.CommitFileAsync(seed, "later.txt", "later", "Later commit");
        await _repos.RunAsync(seed, "push", "origin", "main");

        await _creator.CreateAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin")),
            cancellationToken: Token);

        Assert.True(File.Exists(Path.Combine(Folder, "api", "later.txt")));
    }

    [Fact]
    public async Task Create_FetchFails_CreatesFromLastFetchedBaseWithWarning()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        string api = await _repos.CloneAsync(remote, "clones/api");
        Directory.Move(remote, remote + ".gone");

        var result = await _creator.CreateAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin")),
            cancellationToken: Token);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("couldn't fetch", warning, StringComparison.Ordinal);
        Assert.Equal(Branch, _reader.ReadStatus(Path.Combine(Folder, "api")).Branch);
    }

    [Fact]
    public async Task Create_WorkspaceBranchExistsInARepository_ReusesIt()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string web = await _repos.CloneAsync(await _repos.CreateRemoteAsync("web"), "clones/web");
        await _repos.RunAsync(web, "branch", Branch);

        var result = await _creator.CreateAsync(
            Request(
                new WorkspaceCreationRepository("api", api, "origin/main", "origin"),
                new WorkspaceCreationRepository("web", web, "origin/main", "origin")),
            cancellationToken: Token);

        Assert.All(result.Workspace.Repositories, repository => Assert.Equal(Branch, repository.Branch));
        Assert.Equal(Branch, _reader.ReadStatus(Path.Combine(Folder, "web")).Branch);
    }

    [Theory]
    [InlineData("api", "API", "origin/main", "Each needs its own folder")]
    [InlineData("../api", "web", "origin/main", "folder name")]
    [InlineData("api", ".tammi", "origin/main", "Tammi keeps")]
    [InlineData("api", "web", "origin/missing", "doesn't exist")]
    [InlineData("api", "web", "", "choose a merge target")]
    public async Task Check_FindsProblems(string apiFolder, string webFolder, string webBase, string expected)
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string web = await _repos.CloneAsync(await _repos.CreateRemoteAsync("web"), "clones/web");

        var problems = await _creator.CheckAsync(
            Request(
                new WorkspaceCreationRepository(apiFolder, api, "origin/main", "origin"),
                new WorkspaceCreationRepository(webFolder, web, webBase, "origin")),
            Token);

        Assert.Contains(problems, problem => problem.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Check_SameLocalRepositoryTwice_IsAProblem()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");

        var problems = await _creator.CheckAsync(
            Request(
                new WorkspaceCreationRepository("api", api, "origin/main", "origin"),
                new WorkspaceCreationRepository("api-2", api, "origin/main", "origin")),
            Token);

        Assert.Contains(problems, problem => problem.Contains("twice", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad..branch")]
    [InlineData("-option")]
    public async Task Check_InvalidBranch_IsAProblem(string branch)
    {
        var problems = await _creator.CheckAsync(Request() with { BranchName = branch }, Token);

        Assert.Contains(problems, problem => problem.Contains("valid branch name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_NameAFolderCantHave_IsntAProblem()
    {
        var problems = await _creator.CheckAsync(Request() with { Name = "api#1: fix / login?" }, Token);

        Assert.Empty(problems);
    }

    [Fact]
    public async Task Check_BlankName_IsAProblem()
    {
        var problems = await _creator.CheckAsync(Request() with { Name = " " }, Token);

        Assert.Contains(problems, problem => problem.Contains("name for the workspace", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_InvalidFolderName_IsAProblem()
    {
        var problems = await _creator.CheckAsync(
            Request() with { Folder = Path.Combine(Path.GetDirectoryName(Folder)!, "ends.") },
            Token);

        Assert.Contains(problems, problem => problem.Contains("full stop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_FolderExists_IsAProblem()
    {
        Directory.CreateDirectory(Folder);

        var problems = await _creator.CheckAsync(Request(), Token);

        Assert.Contains(problems, problem => problem.Contains("already exists", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_StepFails_RemovesEverythingCreated()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string app = await _repos.CloneAsync(await _repos.CreateRemoteWithSubmoduleAsync(), "clones/app");

        // The submodule can't be checked out, so the second repository fails after its worktree is added.
        Directory.Move(_repos.PathTo("lib.git"), _repos.PathTo("lib.gone"));

        var ex = await Assert.ThrowsAsync<WorkspaceCreationException>(() => _creator.CreateAsync(
            Request(
                new WorkspaceCreationRepository("api", api, "origin/main", "origin"),
                new WorkspaceCreationRepository("app", app, "origin/main", "origin")),
            cancellationToken: Token));

        Assert.IsType<GitCommandException>(ex.InnerException);
        Assert.Empty(ex.CleanupProblems);
        await AssertNothingLeftAsync(api, app);
    }

    [Fact]
    public async Task Create_Cancelled_RemovesEverythingCreated()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string web = await _repos.CloneAsync(await _repos.CreateRemoteAsync("web"), "clones/web");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var progress = new RecordingProgress(update =>
        {
            // Once the first worktree is done.
            if (update.Message.StartsWith("Fetching web", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _creator.CreateAsync(
            Request(
                new WorkspaceCreationRepository("api", api, "origin/main", "origin"),
                new WorkspaceCreationRepository("web", web, "origin/main", "origin")),
            progress,
            cancellation.Token));

        await AssertNothingLeftAsync(api, web);
    }

    [Fact]
    public async Task Create_RepositoryWithOwnBranch_UsesItInsteadOfTheWorkspaces()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string web = await _repos.CloneAsync(await _repos.CreateRemoteAsync("web"), "clones/web");

        var result = await _creator.CreateAsync(
            Request(
                new WorkspaceCreationRepository("api", api, "origin/main", "origin"),
                new WorkspaceCreationRepository("web", web, "origin/main", "origin", "web-only")),
            cancellationToken: Token);

        Assert.Equal(
            [Branch, "web-only"],
            result.Workspace.Repositories.Select(repository => repository.Branch));
        Assert.True((await _worktrees.GetBranchStateAsync(web, "web-only", cancellationToken: Token)).Exists);
        Assert.False((await _worktrees.GetBranchStateAsync(web, Branch, cancellationToken: Token)).Exists);
    }

    [Fact]
    public async Task Create_ExistingLocalBranch_ChecksItOutAndKeepsItWhenRolledBack()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");
        string web = await _repos.CloneAsync(await _repos.CreateRemoteAsync("web"), "clones/web");
        await _repos.RunAsync(api, "branch", "existing", "origin/main");

        var result = await _creator.CreateAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin", "existing")),
            cancellationToken: Token);

        Assert.Equal("existing", Assert.Single(result.Workspace.Repositories).Branch);
        var head = await _repos.RunAsync(Path.Combine(Folder, "api"), "rev-parse", "--abbrev-ref", "HEAD");
        Assert.Equal("existing", head.StandardOutput.Trim());

        // A failure later in the same request removes the worktree, but not a branch that was already there.
        string app = await _repos.CloneAsync(await _repos.CreateRemoteWithSubmoduleAsync(), "clones/app");
        await _repos.RunAsync(web, "branch", "taken", "origin/main");
        Directory.Move(_repos.PathTo("lib.git"), _repos.PathTo("lib.gone"));
        await Assert.ThrowsAsync<WorkspaceCreationException>(() => _creator.CreateAsync(
            Request(
                new WorkspaceCreationRepository("web", web, "origin/main", "origin", "taken"),
                new WorkspaceCreationRepository("app", app, "origin/main", "origin")) with
            {
                Folder = _repos.PathTo("workspaces/Another"),
            },
            cancellationToken: Token));
        Assert.True((await _worktrees.GetBranchStateAsync(web, "taken", cancellationToken: Token)).Exists);
        Assert.False(Directory.Exists(_repos.PathTo("workspaces/Another/web")));
    }

    [Fact]
    public async Task Create_ExistingRemoteBranch_CreatesLocalBranchTrackingIt()
    {
        string remote = await _repos.CreateRemoteAsync("api");
        await _repos.RunAsync(remote, "branch", "feature", "main");
        string api = await _repos.CloneAsync(remote, "clones/api");

        var request = Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin", "feature"));
        var result = await _creator.CreateAsync(request, cancellationToken: Token);

        Assert.Equal("feature", Assert.Single(result.Workspace.Repositories).Branch);
        var upstream = await _repos.RunAsync(api, "rev-parse", "--abbrev-ref", "feature@{upstream}");
        Assert.Equal("origin/feature", upstream.StandardOutput.Trim());
    }

    [Fact]
    public async Task Check_BlankRepositoryBranch_IsAProblem()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");

        var problems = await _creator.CheckAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin", " ")),
            Token);

        Assert.Contains(problems, problem => problem.Contains("enter a branch name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_RepositoryBranchThatExists_IsntAProblem()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");

        var problems = await _creator.CheckAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin", "main")),
            Token);

        Assert.Empty(problems);
    }

    [Fact]
    public async Task Check_AllRepositoriesHaveOwnBranches_DefaultBranchNeedntBeValid()
    {
        string api = await _repos.CloneAsync(await _repos.CreateRemoteAsync("api"), "clones/api");

        var problems = await _creator.CheckAsync(
            Request(new WorkspaceCreationRepository("api", api, "origin/main", "origin", "own")) with { BranchName = "" },
            Token);

        Assert.Empty(problems);
    }

    private async Task AssertNothingLeftAsync(params string[] repositories)
    {
        Assert.False(Directory.Exists(Folder));
        Assert.False(Directory.Exists(_repos.PathTo("workspaces")));
        Assert.Empty(await _store.ListAsync(Token));
        foreach (string repository in repositories)
        {
            Assert.Equal(BranchState.Missing, await _worktrees.GetBranchStateAsync(repository, Branch, cancellationToken: Token));
            var worktreeList = await _repos.RunAsync(repository, "worktree", "list", "--porcelain");
            Assert.DoesNotContain(Folder, worktreeList.StandardOutput, StringComparison.Ordinal);
        }
    }

    private WorkspaceCreationRequest Request(params WorkspaceCreationRepository[] repositories) => new()
    {
        Name = "Fix login",
        Folder = Folder,
        Tasks = [new TaskReference("jira", "ABC-1")],
        BranchName = Branch,
        Repositories = repositories,
    };

    /// <summary>
    /// Records progress as it's reported, on the reporting thread (unlike <see cref="Progress{T}"/>).
    /// </summary>
    private sealed class RecordingProgress(Action<WorkspaceCreationProgress>? onReport = null)
        : IProgress<WorkspaceCreationProgress>
    {
        public List<WorkspaceCreationProgress> Updates { get; } = [];

        public void Report(WorkspaceCreationProgress value)
        {
            Updates.Add(value);
            onReport?.Invoke(value);
        }
    }
}
