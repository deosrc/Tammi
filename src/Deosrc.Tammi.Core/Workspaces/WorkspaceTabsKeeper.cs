namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Opens the workspace tabs and the repository git graph tabs (with the submodule and worktree graphs open in them) that were open when the app last closed, then keeps
/// them (and the shown workspace) saved as they change.
/// </summary>
public sealed class WorkspaceTabsKeeper(
    WorkspaceTabs workspaceTabs,
    IWorkspaceStore workspaces,
    OpenWorkspaceTabsStore store,
    WorkspaceGitGraphs? gitGraphs = null,
    MainTabOrder? tabOrder = null) : IDisposable
{
    private readonly MainTabOrder _order = tabOrder ?? new MainTabOrder();
    private readonly SemaphoreSlim _saving = new(1, 1);
    private string? _active;
    private string? _activeRepository;
    private bool _started;
    private readonly Dictionary<string, string> _shownGraphs = [];
    private readonly Dictionary<string, string> _restoredShownGraphs = [];
    private readonly Lock _shownLock = new();

    /// <summary>
    /// The repository whose git graph tab was shown last time and was opened again, or <c>null</c>. Set by
    /// <see cref="RestoreAsync"/>.
    /// </summary>
    public string? RestoredRepositoryPath { get; private set; }

    /// <summary>
    /// Opens the saved tabs, skipping workspaces that are no longer listed or can't be opened, and starts keeping the
    /// tabs saved. Only the first call does anything. Nothing is saved until the tabs have been opened, so a failure
    /// here can't lose them.
    /// </summary>
    /// <returns>The workspace whose tab was shown last time and was opened again, or <c>null</c>.</returns>
    public async Task<string?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return null;
        }

        _started = true;
        var saved = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var restored = new List<string>();
        var restoredRepositories = new List<string>();
        if (gitGraphs is not null)
        {
            foreach (var tab in saved.Tabs.Where(tab => tab.Kind == SavedTab.RepositoryKind))
            {
                // A repository that's gone is skipped. Its graph couldn't be read.
                if (tab.Path is not { } path || !Directory.Exists(path))
                {
                    continue;
                }

                gitGraphs.OpenRepository(path);
                foreach (var graph in tab.Tabs ?? [])
                {
                    // A submodule or worktree that's gone is skipped too.
                    if (graph.Path is { } graphPath && Directory.Exists(graphPath))
                    {
                        gitGraphs.OpenRepository(path, graphPath);
                    }
                }

                if (tab.Active?.Path is { } shownGraph)
                {
                    lock (_shownLock)
                    {
                        _restoredShownGraphs[path] = shownGraph;
                        _shownGraphs[path] = shownGraph;
                    }
                }

                restoredRepositories.Add(path);
            }

            gitGraphs.Changed += OnGraphsChanged;
        }

        var listed = saved.Tabs.Any(tab => tab.Kind == SavedTab.WorkspaceKind)
            ? (await workspaces.ListAsync(cancellationToken).ConfigureAwait(false))
                .Where(workspace => workspace.Availability == WorkspaceAvailability.Available)
                .ToDictionary(workspace => workspace.Id)
            : [];
        // The tabs are listed in the order they were open, whatever they were.
        foreach (var tab in saved.Tabs)
        {
            if (tab.Kind == SavedTab.WorkspaceKind && tab.Id is { } id && listed.TryGetValue(id, out var workspace))
            {
                workspaceTabs.Open(workspace.Id, workspace.Name);
                restored.Add(MainTabOrder.WorkspaceKey(workspace.Id));
            }
            else if (tab.Kind == SavedTab.RepositoryKind && tab.Path is { } path && restoredRepositories.Contains(path))
            {
                restored.Add(MainTabOrder.RepositoryKey(path));
            }
        }

        _order.Set(restored);
        string? active = null;
        if (saved.Active is { Kind: SavedTab.WorkspaceKind, Id: { } shown } && workspaceTabs.Find(shown) is not null)
        {
            active = shown;
        }
        else if (saved.Active is { Kind: SavedTab.RepositoryKind, Path: { } shownRepository }
            && gitGraphs?.ForRepository(shownRepository) is not null)
        {
            RestoredRepositoryPath = shownRepository;
            _activeRepository = shownRepository;
        }

        _active = active;
        workspaceTabs.Changed += OnChanged;
        _order.Changed += OnOrderChanged;
        await SaveAsync().ConfigureAwait(false);
        return active;
    }

    /// <summary>
    /// Takes the working directory of the secondary git graph that a repository's tab was showing when the app last
    /// closed, if it has been opened again. It can be taken once, so the tab shows it only the first time.
    /// </summary>
    public string? TakeRestoredShownGraph(string repositoryPath)
    {
        lock (_shownLock)
        {
            if (!_restoredShownGraphs.TryGetValue(repositoryPath, out var graphPath)
                || gitGraphs?.ForRepository(repositoryPath) is null
                || gitGraphs.For(StandaloneRepository.IdFor(repositoryPath)).All(graph => graph.Path != graphPath))
            {
                return null;
            }

            _restoredShownGraphs.Remove(repositoryPath);
            return graphPath;
        }
    }

    /// <summary>
    /// Records which secondary git graph a repository's tab is showing, or <c>null</c> if it shows its own graph, a
    /// terminal or an agent.
    /// </summary>
    public void SetShownGraph(string repositoryPath, string? graphPath)
    {
        lock (_shownLock)
        {
            if (graphPath is null ? !_shownGraphs.Remove(repositoryPath) : _shownGraphs.TryGetValue(repositoryPath, out var current) && current == graphPath)
            {
                return;
            }

            if (graphPath is not null)
            {
                _shownGraphs[repositoryPath] = graphPath;
            }

            // What the user chose wins over what was shown before.
            _restoredShownGraphs.Remove(repositoryPath);
        }

        if (_started)
        {
            _ = SaveAsync();
        }
    }

    /// <summary>
    /// Records which workspace's tab, or which repository's git graph tab, is shown. Both are <c>null</c> when
    /// another page is.
    /// </summary>
    public void SetActive(string? workspaceId, string? repositoryPath = null)
    {
        if (_active == workspaceId && _activeRepository == repositoryPath)
        {
            return;
        }

        _active = workspaceId;
        _activeRepository = repositoryPath;
        if (_started)
        {
            _ = SaveAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        workspaceTabs.Changed -= OnChanged;
        if (gitGraphs is not null)
        {
            gitGraphs.Changed -= OnGraphsChanged;
        }

        _order.Changed -= OnOrderChanged;
        _saving.Dispose();
    }

    private void OnChanged() => _ = SaveAsync();

    private void OnOrderChanged() => _ = SaveAsync();

    private void OnGraphsChanged(string workspaceId) => _ = SaveAsync();

    private async Task SaveAsync()
    {
        try
        {
            await _saving.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            // Read after waiting, so the last of several quick changes is what's saved.
            var open = new Dictionary<string, SavedTab>();
            foreach (var tab in workspaceTabs.Tabs)
            {
                open[MainTabOrder.WorkspaceKey(tab.WorkspaceId)] = new SavedTab(SavedTab.WorkspaceKind, Id: tab.WorkspaceId);
            }

            foreach (var graph in gitGraphs?.StandaloneGraphs() ?? [])
            {
                var others = (gitGraphs?.For(graph.WorkspaceId) ?? [])
                    .Where(other => !other.IsPrimary)
                    .OrderBy(other => other.StartOrder)
                    .Select(other => new SavedTab(SavedTab.GitGraphKind, Path: other.Path))
                    .ToList();
                SavedTab? shownGraph = null;
                lock (_shownLock)
                {
                    if (_shownGraphs.TryGetValue(graph.Path, out var shownPath))
                    {
                        shownGraph = others.Find(other => other.Path == shownPath);
                    }
                }

                open[MainTabOrder.RepositoryKey(graph.Path)] = new SavedTab(
                    SavedTab.RepositoryKind,
                    Path: graph.Path,
                    Tabs: others.Count > 0 ? others : null,
                    Active: shownGraph);
            }

            // In the order of the tab strip. A tab it hasn't listed yet comes last.
            var tabs = _order.Keys.Where(open.ContainsKey).Concat(open.Keys).Distinct().Select(key => open[key]).ToList();
            var active = _active is { } shown && open.ContainsKey(MainTabOrder.WorkspaceKey(shown))
                ? open[MainTabOrder.WorkspaceKey(shown)]
                : _activeRepository is { } shownRepository && open.TryGetValue(MainTabOrder.RepositoryKey(shownRepository), out var repository)
                    ? repository
                    : null;
            await store.SaveAsync(new OpenWorkspaceTabs(tabs, active)).ConfigureAwait(false);
        }
        finally
        {
            _saving.Release();
        }
    }
}
