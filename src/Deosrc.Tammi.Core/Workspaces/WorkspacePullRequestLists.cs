using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The Pull Request lists of the workspaces open in tabs. A workspace's list is read when it's first started, kept while
/// its tab is open (reading again on its timer), and dropped when its tab closes. Every list is read again when
/// integrations are added or removed, since Pull Requests from them may now be readable, or no longer be.
/// </summary>
public sealed class WorkspacePullRequestLists : IDisposable
{
    private readonly WorkspaceTabs _tabs;
    private readonly IntegrationRegistry _registry;
    private readonly IGitRepositoryReader _reader;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan? _refreshInterval;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, WorkspacePullRequestList> _lists = [];
    private bool _disposed;

    /// <param name="refreshInterval">
    /// How often each list reads again, or <see cref="WorkspacePullRequestList.DefaultRefreshInterval"/>.
    /// </param>
    public WorkspacePullRequestLists(
        WorkspaceTabs tabs,
        IntegrationRegistry registry,
        IGitRepositoryReader reader,
        TimeProvider timeProvider,
        TimeSpan? refreshInterval = null)
    {
        _tabs = tabs;
        _registry = registry;
        _reader = reader;
        _timeProvider = timeProvider;
        _refreshInterval = refreshInterval;
        _tabs.Changed += OnTabsChanged;
        _registry.Changed += OnRegistryChanged;
    }

    /// <summary>
    /// Returns the list of an open workspace, or <see langword="null"/> if it hasn't been started.
    /// </summary>
    /// <summary>
    /// Raised when a workspace's list has been started: it's now live. It can be raised on any thread.
    /// </summary>
    public event Action? Started;

    public WorkspacePullRequestList? Find(string workspaceId)
    {
        lock (_lock)
        {
            return _lists.GetValueOrDefault(workspaceId);
        }
    }

    /// <summary>
    /// Starts reading the Pull Requests of a workspace that's open in a tab. If it's already started, returns its list,
    /// unless its repositories have changed: then its list is replaced.
    /// </summary>
    /// <exception cref="InvalidOperationException">The workspace isn't open in a tab.</exception>
    public WorkspacePullRequestList Start(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        WorkspacePullRequestList list;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_tabs.Find(workspace.Id) is null)
            {
                throw new InvalidOperationException($"The workspace '{workspace.Name}' isn't open.");
            }

            if (_lists.TryGetValue(workspace.Id, out var existing))
            {
                if (existing.Workspace.Repositories.SequenceEqual(workspace.Repositories))
                {
                    return existing;
                }

                existing.Dispose();
            }

            list = new WorkspacePullRequestList(workspace, _registry, _reader, _timeProvider, _refreshInterval);
            _lists[workspace.Id] = list;
        }

        list.Start();
        Started?.Invoke();
        return list;
    }

    /// <summary>
    /// Drops every list.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _tabs.Changed -= OnTabsChanged;
            _registry.Changed -= OnRegistryChanged;
            foreach (var list in _lists.Values)
            {
                list.Dispose();
            }

            _lists.Clear();
        }
    }

    private void OnTabsChanged()
    {
        lock (_lock)
        {
            foreach (string id in _lists.Keys.Where(id => _tabs.Find(id) is null).ToArray())
            {
                _lists.Remove(id, out var list);
                list!.Dispose();
            }
        }
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        WorkspacePullRequestList[] lists;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            lists = [.. _lists.Values];
        }

        foreach (var list in lists)
        {
            try
            {
                list.Refresh();
            }
            catch (ObjectDisposedException)
            {
                // Its tab closed meanwhile.
            }
        }
    }
}
