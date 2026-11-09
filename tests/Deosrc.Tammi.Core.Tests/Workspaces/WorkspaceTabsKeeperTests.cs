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
        for (var i = 0; i < 100 && saved.WorkspaceIds.Count != 1; i++)
        {
            await Task.Delay(20, CancellationToken);
            saved = await store.LoadAsync(CancellationToken);
        }

        Assert.Equal(["a"], saved.WorkspaceIds);
        Assert.Equal("a", saved.ActiveWorkspaceId);
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
