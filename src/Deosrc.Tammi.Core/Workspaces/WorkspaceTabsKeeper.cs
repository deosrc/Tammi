namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Opens the workspace tabs that were open when the app last closed, then keeps them (and the shown one) saved
/// as they change.
/// </summary>
public sealed class WorkspaceTabsKeeper(
    WorkspaceTabs tabs,
    IWorkspaceStore workspaces,
    OpenWorkspaceTabsStore store) : IDisposable
{
    private readonly SemaphoreSlim _saving = new(1, 1);
    private string? _active;
    private bool _started;

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
    /// Records which workspace's tab is shown, or <c>null</c> when another page is.
    /// </summary>
    public void SetActive(string? workspaceId)
    {
        if (_active == workspaceId)
        {
            return;
        }

        _active = workspaceId;
        if (_started)
        {
            _ = SaveAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        tabs.Changed -= OnChanged;
        _saving.Dispose();
    }

    private void OnChanged() => _ = SaveAsync();

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
            await store.SaveAsync(new OpenWorkspaceTabs(ids, active)).ConfigureAwait(false);
        }
        finally
        {
            _saving.Release();
        }
    }
}
