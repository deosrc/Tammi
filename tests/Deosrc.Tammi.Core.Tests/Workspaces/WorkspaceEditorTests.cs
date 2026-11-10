using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Tests.Git;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceEditorTests : IDisposable
{
    private const string Branch = "ABC-1-fix-login";

    private readonly TempRepositories _repos = new();
    private readonly GitWorktreeService _worktrees;
    private readonly WorkspaceStore _store;
    private readonly WorkspaceCreator _creator;
    private readonly WorkspaceEditor _editor;

    public WorkspaceEditorTests()
    {
        _worktrees = new GitWorktreeService(_repos.Git, new GitRepositoryReader());
        _store = new WorkspaceStore(_repos.PathTo("data"), [], []);
        _creator = new WorkspaceCreator(_store, _worktrees);
        _editor = new WorkspaceEditor(_store, _worktrees);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Edit_ChangesTasks_KeepsRepositories()
    {
        string api = await CloneAsync("api");
        var workspace = await CreateAsync(api);

        var result = await _editor.EditAsync(
            workspace.Id,
            new WorkspaceEditRequest { Tasks = [new TaskReference("jira", "ABC-2")] },
            cancellationToken: Token);

        Assert.Equal([new TaskReference("jira", "ABC-2")], result.Workspace.Tasks);
        Assert.Equal(workspace.Repositories, result.Workspace.Repositories);
        var reloaded = await _store.LoadAsync(workspace.Folder, Token);
        AssertSame(result.Workspace, reloaded);
    }

    [Fact]
    public async Task Edit_AddsRepository_CreatesWorktreeOnTheWorkspaceBranch()
    {
        string api = await CloneAsync("api");
        string web = await CloneAsync("web");
        var workspace = await CreateAsync(api);

        var result = await _editor.EditAsync(
            workspace.Id,
            new WorkspaceEditRequest
            {
                Tasks = workspace.Tasks,
                BranchName = Branch,
                AddedRepositories = [new WorkspaceCreationRepository("web", web, "origin/main", "origin")],
            },
            cancellationToken: Token);

        Assert.Empty(result.Warnings);
        Assert.Equal(
            [
                new WorkspaceRepository("api", api, Branch, "origin/main"),
                new WorkspaceRepository("web", web, Branch, "origin/main"),
            ],
            result.Workspace.Repositories);
        Assert.True(Directory.Exists(Path.Combine(workspace.Folder, "web")));
        Assert.True((await _worktrees.GetBranchStateAsync(web, Branch, cancellationToken: Token)).Exists);
        AssertSame(result.Workspace, (await _store.FindAsync(workspace.Id, Token))!.Workspace!);
    }

    [Fact]
    public async Task Edit_FirstRepositoryOfWorkspaceWithoutAny_UsesRequestedBranch()
    {
        string api = await CloneAsync("api");
        var workspace = await CreateAsync();

        var result = await _editor.EditAsync(
            workspace.Id,
            new WorkspaceEditRequest
            {
                Tasks = workspace.Tasks,
                BranchName = "other-branch",
                AddedRepositories = [new WorkspaceCreationRepository("api", api, "origin/main", "origin")],
            },
            cancellationToken: Token);

        Assert.Equal("other-branch", Assert.Single(result.Workspace.Repositories).Branch);
    }

    [Fact]
    public async Task Edit_RepositoryAlreadyInWorkspace_FailsCheckAndChangesNothing()
    {
        string api = await CloneAsync("api");
        var workspace = await CreateAsync(api);

        var ex = await Assert.ThrowsAsync<WorkspaceEditException>(() => _editor.EditAsync(
            workspace.Id,
            new WorkspaceEditRequest
            {
                Tasks = [],
                BranchName = Branch,
                AddedRepositories = [new WorkspaceCreationRepository("api-2", api, "origin/main", "origin")],
            },
            cancellationToken: Token));

        Assert.NotEmpty(ex.Problems);
        AssertSame(workspace, (await _store.FindAsync(workspace.Id, Token))!.Workspace!);
    }

    [Fact]
    public async Task Edit_FolderNameClash_FailsCheck()
    {
        string api = await CloneAsync("api");
        string web = await CloneAsync("web");
        var workspace = await CreateAsync(api);

        var problems = await _editor.CheckAsync(
            workspace.Id,
            new WorkspaceEditRequest
            {
                BranchName = Branch,
                AddedRepositories = [new WorkspaceCreationRepository("API", web, "origin/main", "origin")],
            },
            Token);

        Assert.NotEmpty(problems);
    }

    [Fact]
    public async Task Edit_DifferentBranchFromWorkspaces_FailsCheck()
    {
        string api = await CloneAsync("api");
        string web = await CloneAsync("web");
        var workspace = await CreateAsync(api);

        var problems = await _editor.CheckAsync(
            workspace.Id,
            new WorkspaceEditRequest
            {
                BranchName = "another",
                AddedRepositories = [new WorkspaceCreationRepository("web", web, "origin/main", "origin")],
            },
            Token);

        Assert.Contains(problems, problem => problem.Contains(Branch));
    }

    [Fact]
    public async Task Edit_Cancelled_RemovesAddedWorktreeAndBranch_KeepsMetadata()
    {
        string api = await CloneAsync("api");
        string web = await CloneAsync("web");
        var workspace = await CreateAsync(api);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var progress = new CancellingProgress(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _editor.EditAsync(
            workspace.Id,
            new WorkspaceEditRequest
            {
                Tasks = [new TaskReference("jira", "ABC-9")],
                BranchName = Branch,
                AddedRepositories = [new WorkspaceCreationRepository("web", web, "origin/main", "origin")],
            },
            progress,
            cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(workspace.Folder, "web")));
        Assert.False((await _worktrees.GetBranchStateAsync(web, Branch, cancellationToken: Token)).Exists);
        Assert.True(Directory.Exists(Path.Combine(workspace.Folder, "api")));
        AssertSame(workspace, (await _store.FindAsync(workspace.Id, Token))!.Workspace!);
    }

    [Fact]
    public async Task Edit_UnknownWorkspace_Throws() =>
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() =>
            _editor.EditAsync("missing", new WorkspaceEditRequest(), cancellationToken: Token));

    // Records compare their lists by reference, so compare what they hold.
    private static void AssertSame(Workspace expected, Workspace actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Folder, actual.Folder);
        Assert.Equal(expected.Tasks, actual.Tasks);
        Assert.Equal(expected.Repositories, actual.Repositories);
    }

    private async Task<string> CloneAsync(string name) =>
        await _repos.CloneAsync(await _repos.CreateRemoteAsync(name), $"clones/{name}");

    private async Task<Workspace> CreateAsync(params string[] repositories)
    {
        var result = await _creator.CreateAsync(
            new WorkspaceCreationRequest
            {
                Name = "Fix login",
                Folder = _repos.PathTo("workspaces/fix-login"),
                Tasks = [new TaskReference("jira", "ABC-1")],
                BranchName = Branch,
                Repositories =
                [
                    .. repositories.Select(path => new WorkspaceCreationRepository(
                        Path.GetFileName(path), path, "origin/main", "origin")),
                ],
            },
            cancellationToken: Token);
        return result.Workspace;
    }

    /// <summary>
    /// Cancels as soon as the worktree is being created, so the edit is rolled back part way.
    /// </summary>
    private sealed class CancellingProgress(CancellationTokenSource cancellation) : IProgress<WorkspaceCreationProgress>
    {
        public void Report(WorkspaceCreationProgress value)
        {
            if (value.Message.StartsWith("Checking out", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
        }
    }
}
