using System.Collections.Concurrent;
using System.Text.Json;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// What the visualiser draws: the workspaces, each with its status, task source and agents, as JSON for the page.
/// </summary>
/// <remarks>
/// A workspace's status is worked out as Home does, from the git status that's been read so far. This doesn't read any
/// itself, so a workspace shows as it would with no changes until Home or a tab has read its status. It follows
/// reads as they happen, including those of workspaces that are open in a tab. While a workspace's status is being
/// read again, e.g. just after its tab opens, it keeps the status it last had.
/// </remarks>
public sealed class VisualiserFeed
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceStore _workspaceStore;
    private readonly WorkspaceAgents _agents;
    private readonly HomeGitStatus _gitStatus;
    private readonly WorkspaceTabs _tabs;
    private readonly IntegrationRegistry _registry;
    private readonly ISettingsStore _settings;
    private readonly ConcurrentDictionary<string, WorkspaceStatus> _statuses = new(StringComparer.Ordinal);
    private int _scheduled;
    private string? _last;
    private IDisposable? _observing;

    public VisualiserFeed(
        IWorkspaceStore workspaceStore,
        WorkspaceAgents agents,
        HomeGitStatus gitStatus,
        WorkspaceTabs tabs,
        IntegrationRegistry registry,
        ISettingsStore settings)
    {
        _workspaceStore = workspaceStore;
        _agents = agents;
        _gitStatus = gitStatus;
        _tabs = tabs;
        _registry = registry;
        _settings = settings;
        _settings.Changed += OnSourceChanged;
        _workspaceStore.Changed += OnWorkspacesChanged;
        _tabs.Changed += OnWorkspacesChanged;
        _agents.Changed += _ => OnSourceChanged();
        _gitStatus.Changed += OnSourceChanged;
        _gitStatus.LiveStarted += OnWorkspacesChanged;
    }

    /// <summary>
    /// Raised when the snapshot has changed: a workspace, its status or one of its agents. It can be raised on a
    /// background thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Reads the git status and Pull Requests of the workspaces again, as Home does when it's opened, so the
    /// statuses are up to date. The visualiser then follows them as they're read. Workspaces that are open in a tab are
    /// already live, so they aren't read.
    /// </summary>
    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var workspaces = await ObserveAsync(cancellationToken);
            if (workspaces.Length > 0)
            {
                await _gitStatus.RefreshAsync(workspaces, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
        }
    }

    // Workspaces that are open in a tab are live, and their changes only reach HomeGitStatus's Changed event for
    // workspaces that are observed. Which are live changes as tabs open and close, so who's observed is redone then.
    private async Task<Workspace[]> ObserveAsync(CancellationToken cancellationToken = default)
    {
        var listings = await _workspaceStore.ListAsync(cancellationToken);
        Workspace[] workspaces = [.. listings.Select(listing => listing.Workspace).OfType<Workspace>()];
        Interlocked.Exchange(ref _observing, _gitStatus.Observe(workspaces, OnSourceChanged))?.Dispose();
        return workspaces;
    }

    // The workspaces' monitors start after their tabs open, so this is also called as they do. It's followed before
    // the snapshot is looked at again, so a read that finishes in between isn't missed.
    private void OnWorkspacesChanged()
    {
        if (Changed is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ObserveAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
            }

            OnSourceChanged();
        });
    }

    /// <summary>
    /// The current snapshot, as JSON.
    /// </summary>
    public async Task<string> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        var listings = await _workspaceStore.ListAsync(cancellationToken);
        var nodes = listings.Select(listing =>
        {
            var agents = _agents.For(listing.Id);
            var status = StatusOf(listing, agents);
            return new
            {
                id = listing.Id,
                name = listing.Name,
                status = status.ToString(),
                statusLabel = WorkspaceStatuses.Label(status),
                // Grouped as Home groups its workspaces: by repository, JIRA project, or "No task".
                source = listing.Workspace is { } details
                    ? WorkspaceSources.Of(details.Tasks, id => _registry.All.FirstOrDefault(i => i.Id == id)?.Name)
                    : WorkspaceSources.NoTasks,
                agents = agents
                    .Select(agent => new { id = agent.Name, state = agent.State.ToString() })
                    .ToArray(),
            };
        });
        return JsonSerializer.Serialize(
            new
            {
                workspaceVisualisation = _settings.Current.Visualiser.WorkspaceVisualisation.ToString(),
                burstWhenWorkingFinishes = _settings.Current.Visualiser.BurstWhenWorkingFinishes,
                workspaces = nodes,
            },
            JsonOptions);
    }

    // Git status can change many times in a moment, so changes that come close together are looked at once, and
    // Changed is raised only if what the page would draw is different.
    private WorkspaceStatus StatusOf(WorkspaceListing listing, IReadOnlyList<WorkspaceAgent> agents)
    {
        if (listing.Workspace is not { } workspace || listing.Availability != WorkspaceAvailability.Available)
        {
            return WorkspaceStatus.RequiresAttention;
        }

        // Git status and Pull Requests are read separately, so a workspace that has just become live has one before
        // the other. Until both are, its status would be wrong, so it keeps the one it last had.
        var summary = _gitStatus.Get(workspace);
        if (summary.IsLoaded && summary.Repositories.All(repository => repository.PullRequests.IsLoaded))
        {
            return _statuses[listing.Id] = WorkspaceStatuses.Of(summary, agents.Select(agent => agent.State));
        }

        return _statuses.TryGetValue(listing.Id, out var last)
            ? last
            : WorkspaceStatuses.Of(
                summary.IsLoaded ? summary : new WorkspaceGitSummary([]),
                agents.Select(agent => agent.State));
    }

    private void OnSourceChanged()
    {
        if (Changed is not null && Interlocked.Exchange(ref _scheduled, 1) == 0)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(Quiet).ConfigureAwait(false);
                Volatile.Write(ref _scheduled, 0);
                try
                {
                    var snapshot = await SnapshotAsync().ConfigureAwait(false);
                    if (Interlocked.Exchange(ref _last, snapshot) != snapshot)
                    {
                        Changed?.Invoke();
                    }
                }
                catch (IOException)
                {
                }
            });
        }
    }
}
