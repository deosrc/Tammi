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

    private static SavedTab Workspace(string id) => new(SavedTab.WorkspaceKind, Id: id);

    private static SavedTab Repository(string path, IReadOnlyList<string>? graphs = null, string? shown = null) => new(
        SavedTab.RepositoryKind,
        Path: path,
        Tabs: graphs?.Select(graph => new SavedTab(SavedTab.GitGraphKind, Path: graph)).ToList(),
        Active: shown is null ? null : new SavedTab(SavedTab.GitGraphKind, Path: shown));

    [Fact]
    public async Task RestoreAsync_OpensSavedWorkspacesInOrder_SkippingUnavailableOnes()
    {
        var store = new OpenWorkspaceTabsStore(_directory);
        await store.SaveAsync(
            new OpenWorkspaceTabs([Workspace("a"), Workspace("gone"), Workspace("broken"), Workspace("b")], Workspace("b")),
            CancellationToken);
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

        var saved = await WaitForSavedAsync(store, saved => saved.Tabs.Count == 1 && saved.Active?.Id == "a");

        Assert.Equal([Workspace("a")], saved.Tabs);
        Assert.Equal(Workspace("a"), saved.Active);
    }

    private static WorkspaceGitGraphs NewGraphs() => new(
        new GitRepositoryReader(), new GitWorkingTree(new GitCli()), new GitSync(new GitCli()), new GitTags(new GitCli()),
        new GitBranches(new GitCli()), new GitStash(new GitCli()), new GitDiffs(new GitRepositoryReader(), new GitCli()),
        new InMemorySettingsStore());

    private async Task<OpenWorkspaceTabs> WaitForSavedAsync(OpenWorkspaceTabsStore store, Func<OpenWorkspaceTabs, bool> done)
    {
        var saved = OpenWorkspaceTabs.None;
        for (var i = 0; i < 100 && !done(saved); i++)
        {
            await Task.Delay(20, CancellationToken);
            saved = await store.LoadAsync(CancellationToken);
        }

        return saved;
    }

    [Fact]
    public async Task RestoreAsync_OpensSavedRepositoryGraphs_SkippingMissingFolders()
    {
        var existing = Path.Combine(_directory, "repo");
        Directory.CreateDirectory(existing);
        var store = new OpenWorkspaceTabsStore(_directory);
        await store.SaveAsync(
            new OpenWorkspaceTabs([Repository(existing), Repository(Path.Combine(_directory, "gone"))]),
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

            var saved = await WaitForSavedAsync(store, saved => saved.Active is not null);

            Assert.Equal(Repository(repo), saved.Active);
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

        var saved = await WaitForSavedAsync(store, saved => saved.Tabs.SequenceEqual([Repository("/repos/b")]));

        Assert.Equal([Repository("/repos/b")], saved.Tabs);
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

            var saved = await WaitForSavedAsync(store, saved => saved.Tabs.FirstOrDefault()?.Active is not null);

            Assert.Equal([Repository(repo, [sub, other], sub)], saved.Tabs);
        }

        using var restoredGraphs = NewGraphs();
        using var restoredKeeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), new FakeStore(), store, restoredGraphs);

        await restoredKeeper.RestoreAsync(CancellationToken);

        Assert.Equal([repo, sub, other], restoredGraphs.For(StandaloneRepository.IdFor(repo)).Select(graph => graph.Path));
        Assert.Equal(sub, restoredKeeper.TakeRestoredShownGraph(repo));
        Assert.Null(restoredKeeper.TakeRestoredShownGraph(repo));
    }

    [Fact]
    public async Task WorkspaceAndRepositoryTabs_KeepTheirOrderTogether()
    {
        var repo = Path.Combine(_directory, "repo");
        Directory.CreateDirectory(repo);
        var store = new OpenWorkspaceTabsStore(_directory);
        var workspaces = new FakeStore(
            new WorkspaceListing("a", "Ay", "/a", WorkspaceAvailability.Available),
            new WorkspaceListing("b", "Bee", "/b", WorkspaceAvailability.Available));
        await store.SaveAsync(
            new OpenWorkspaceTabs([Workspace("a"), Repository(repo), Workspace("b")], Repository(repo)),
            CancellationToken);
        using var graphs = NewGraphs();
        var order = new MainTabOrder();
        using var keeper = new WorkspaceTabsKeeper(new WorkspaceTabs(), workspaces, store, graphs, order);

        await keeper.RestoreAsync(CancellationToken);

        Assert.Equal(
            [MainTabOrder.WorkspaceKey("a"), MainTabOrder.RepositoryKey(repo), MainTabOrder.WorkspaceKey("b")],
            order.Keys);
        var saved = await WaitForSavedAsync(store, saved => saved.Tabs.Count == 3);
        Assert.Equal([Workspace("a"), Repository(repo), Workspace("b")], saved.Tabs);
    }

    [Fact]
    public async Task LoadAsync_FromVersion1File_KeepsTabsAndTheirGraphs()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, OpenWorkspaceTabsStore.FileName), """
            { "version": 1, "workspaceIds": ["a", "b"], "activeWorkspaceId": "b",
              "repositoryPaths": ["/r"], "activeRepositoryPath": "/r",
              "secondaryGraphPaths": { "/r": ["/r/sub"] }, "shownGraphPaths": { "/r": "/r/sub" } }
            """, CancellationToken);

        var saved = await new OpenWorkspaceTabsStore(_directory).LoadAsync(CancellationToken);

        Assert.Equal([Workspace("a"), Workspace("b"), Repository("/r", ["/r/sub"], "/r/sub")], saved.Tabs);
        Assert.Equal(Workspace("b"), saved.Active);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptFile_ReturnsNone()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, OpenWorkspaceTabsStore.FileName), "{ nope", CancellationToken);

        Assert.Empty((await new OpenWorkspaceTabsStore(_directory).LoadAsync(CancellationToken)).Tabs);
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
