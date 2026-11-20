using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Tests.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceTabsKeeperTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreAsync_WithoutFile_OpensNothing()
    {
        var tabs = new WorkspaceTabs();
        using var keeper = new WorkspaceTabsKeeper(tabs, new FakeStore(), new OpenWorkspaceTabsStore(_directory));

        Assert.Null(await keeper.RestoreAsync(CancellationToken));

        Assert.Empty(tabs.Tabs);
    }

    [Fact]
    public async Task RestoreAsync_OpensSavedWorkspacesInOrder_SkippingUnavailableOnes()
    {
        var store = new OpenWorkspaceTabsStore(_directory);
        await store.SaveAsync(new OpenWorkspaceTabs(["a", "gone", "broken", "b"], "b"), CancellationToken);
        var workspaces = new FakeStore(
            new WorkspaceListing("b", "Bee", "/b", WorkspaceAvailability.Available),
            new WorkspaceListing("a", "Ay", "/a", WorkspaceAvailability.Available),
            new WorkspaceListing("broken", "Broken", "/x", WorkspaceAvailability.Unreadable));
        var tabs = new WorkspaceTabs();
        using var keeper = new WorkspaceTabsKeeper(tabs, workspaces, store);

        var active = await keeper.RestoreAsync(CancellationToken);

        Assert.Equal("b", active);
        Assert.Equal([new WorkspaceTab("a", "Ay"), new WorkspaceTab("b", "Bee")], tabs.Tabs);
    }

    [Fact]
    public async Task Changes_AfterRestore_AreSaved()
    {
        var store = new OpenWorkspaceTabsStore(_directory);
        var tabs = new WorkspaceTabs();
        using var keeper = new WorkspaceTabsKeeper(tabs, new FakeStore(), store);
        await keeper.RestoreAsync(CancellationToken);

        tabs.Open("a", "Ay");
        tabs.Open("b", "Bee");
        keeper.SetActive("a");
        tabs.Close("b");

        OpenWorkspaceTabs saved = OpenWorkspaceTabs.None;
        for (var i = 0; i < 100 && !(saved.WorkspaceIds.Count == 1 && saved.ActiveWorkspaceId == "a"); i++)
        {
            await Task.Delay(20, CancellationToken);
            saved = await store.LoadAsync(CancellationToken);
        }

        Assert.Equal(["a"], saved.WorkspaceIds);
        Assert.Equal("a", saved.ActiveWorkspaceId);
    }

    private static WorkspaceGitGraphs NewGraphs() => new(
        new GitRepositoryReader(), new GitWorkingTree(new GitCli()), new GitSync(new GitCli()), new GitTags(new GitCli()),
        new GitBranches(new GitCli()), new GitStash(new GitCli()), new GitDiffs(new GitRepositoryReader(), new GitCli()),
        new InMemorySettingsStore());

    [Fact]
    public async Task RestoreAsync_OpensSavedRepositoryGraphs_SkippingMissingFolders()
    {
        var existing = Path.Combine(_directory, "repo");
        Directory.CreateDirectory(existing);
        var store = new OpenWorkspaceTabsStore(_directory);
        await store.SaveAsync(
            new OpenWorkspaceTabs([]) { RepositoryPaths = [existing, Path.Combine(_directory, "gone")] },
            CancellationToken);
        using var graphs = NewGraphs();
        using var keeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, graphs);

        await keeper.RestoreAsync(CancellationToken);

        Assert.Equal([existing], graphs.StandaloneGraphs().Select(graph => graph.Path));
    }

    [Fact]
    public async Task ShownRepositoryGraph_IsSavedAndRestored()
    {
        var repo = Path.Combine(_directory, "repo");
        Directory.CreateDirectory(repo);
        var store = new OpenWorkspaceTabsStore(_directory);
        using (var graphs = NewGraphs())
        using (var keeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, graphs))
        {
            await keeper.RestoreAsync(CancellationToken);
            graphs.OpenRepository(repo);
            keeper.SetActive(null, repo);

            OpenWorkspaceTabs saved = OpenWorkspaceTabs.None;
            for (var i = 0; i < 100 && saved.ActiveRepositoryPath != repo; i++)
            {
                await Task.Delay(20, CancellationToken);
                saved = await store.LoadAsync(CancellationToken);
            }

            Assert.Equal(repo, saved.ActiveRepositoryPath);
        }

        using var restoredGraphs = NewGraphs();
        using var restoredKeeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, restoredGraphs);

        Assert.Null(await restoredKeeper.RestoreAsync(CancellationToken));
        Assert.Equal(repo, restoredKeeper.RestoredRepositoryPath);
    }

    [Fact]
    public async Task RepositoryGraphs_AfterRestore_AreSaved()
    {
        var store = new OpenWorkspaceTabsStore(_directory);
        using var graphs = NewGraphs();
        using var keeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, graphs);
        await keeper.RestoreAsync(CancellationToken);

        graphs.OpenRepository("/repos/a");
        graphs.OpenRepository("/repos/b");
        graphs.Close(graphs.ForRepository("/repos/a")!);

        OpenWorkspaceTabs saved = OpenWorkspaceTabs.None;
        for (var i = 0; i < 100 && !saved.RepositoryPaths.SequenceEqual(["/repos/b"]); i++)
        {
            await Task.Delay(20, CancellationToken);
            saved = await store.LoadAsync(CancellationToken);
        }

        Assert.Equal(["/repos/b"], saved.RepositoryPaths);
    }

    [Fact]
    public async Task SecondaryRepositoryGraphs_AreSavedAndRestoredWithTheShownOne()
    {
        var repo = Path.Combine(_directory, "repo");
        var sub = Path.Combine(repo, "sub");
        var other = Path.Combine(_directory, "worktree");
        Directory.CreateDirectory(sub);
        Directory.CreateDirectory(other);
        var store = new OpenWorkspaceTabsStore(_directory);
        using (var graphs = NewGraphs())
        using (var keeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, graphs))
        {
            await keeper.RestoreAsync(CancellationToken);
            graphs.OpenRepository(repo);
            graphs.OpenRepository(repo, sub);
            graphs.OpenRepository(repo, other);
            keeper.SetShownGraph(repo, sub);

            OpenWorkspaceTabs saved = OpenWorkspaceTabs.None;
            for (var i = 0; i < 100 && saved.ShownGraphPaths.GetValueOrDefault(repo) != sub; i++)
            {
                await Task.Delay(20, CancellationToken);
                saved = await store.LoadAsync(CancellationToken);
            }

            Assert.Equal([sub, other], saved.SecondaryGraphPaths[repo]);
        }

        using var restoredGraphs = NewGraphs();
        using var restoredKeeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, restoredGraphs);

        await restoredKeeper.RestoreAsync(CancellationToken);

        Assert.Equal([repo, sub, other], restoredGraphs.For(StandaloneRepository.IdFor(repo)).Select(graph => graph.Path));
        Assert.Equal(sub, restoredKeeper.TakeRestoredShownGraph(repo));
        Assert.Null(restoredKeeper.TakeRestoredShownGraph(repo));
    }

    [Fact]
    public async Task LoadAsync_WithCorruptFile_ReturnsNone()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, OpenWorkspaceTabsStore.FileName), "{ nope", CancellationToken);

        Assert.Empty((await new OpenWorkspaceTabsStore(_directory).LoadAsync(CancellationToken)).WorkspaceIds);
    }

    private sealed class FakeStore(params WorkspaceListing[] listings) : IWorkspaceStore
    {
        public string? IndexProblem => null;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<WorkspaceListing>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkspaceListing>>(listings);

        public Task<WorkspaceListing?> FindAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Workspace> LoadAsync(string folder, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Workspace> SaveAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Workspace> RenameAsync(string id, string name, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Workspace> AddExistingAsync(string folder, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveFromIndexAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
