namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Opens the workspace tabs and the repository git graph tabs that were open when the app last closed, then keeps
/// them (and the shown workspace) saved as they change.
/// </summary>
public sealed class WorkspaceTabsKeeper(
    WorkspaceTabs tabs,
    IWorkspaceStore workspaces,
    OpenWorkspaceTabsStore store,
    WorkspaceGitGraphs? gitGraphs = null) : IDisposable
{
    private readonly SemaphoreSlim _saving = new(1, 1);
    private string? _active;
    private string? _activeRepository;
    private bool _started;

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
        if (gitGraphs is not null)
        {
            // A repository that's gone is skipped. Its graph couldn't be read.
            foreach (var path in saved.RepositoryPaths.Where(Directory.Exists))
            {
                gitGraphs.OpenRepository(path);
            }

            if (saved.ActiveRepositoryPath is { } shownRepository && gitGraphs.ForRepository(shownRepository) is not null)
            {
                RestoredRepositoryPath = shownRepository;
                _activeRepository = shownRepository;
            }

            gitGraphs.Changed += OnGraphsChanged;
        }

        if (saved.WorkspaceIds.Count > 0)
        {
            var listed = (await workspaces.ListAsync(cancellationToken).ConfigureAwait(false))
                .Where(workspace => workspace.Availability == WorkspaceAvailability.Available)
                .ToDictionary(workspace => workspace.Id);
            foreach (var id in saved.WorkspaceIds)
            {
                if (listed.TryGetValue(id, out var workspace))
                {
                    tabs.Open(workspace.Id, workspace.Name);
                }
            }
        }

        var active = saved.ActiveWorkspaceId is { } shown && tabs.Find(shown) is not null ? shown : null;
        _active = active;
        tabs.Changed += OnChanged;
        await SaveAsync().ConfigureAwait(false);
        return active;
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
        tabs.Changed -= OnChanged;
        if (gitGraphs is not null)
        {
            gitGraphs.Changed -= OnGraphsChanged;
        }

        _saving.Dispose();
    }

    private void OnChanged() => _ = SaveAsync();

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
            var ids = tabs.Tabs.Select(tab => tab.WorkspaceId).ToList();
            var active = _active is { } shown && ids.Contains(shown) ? shown : null;
            List<string> repositoryPaths = gitGraphs?.StandaloneGraphs().Select(graph => graph.Path).ToList() ?? [];
            await store.SaveAsync(new OpenWorkspaceTabs(ids, active)
            {
                RepositoryPaths = repositoryPaths,
                ActiveRepositoryPath = _activeRepository is { } shownRepository && repositoryPaths.Contains(shownRepository)
                    ? shownRepository
                    : null,
            }).ConfigureAwait(false);
        }
        finally
        {
            _saving.Release();
        }
    }
}
