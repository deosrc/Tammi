using System.Text.Json;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// What the visualiser draws: the workspaces, each with its status, task source and agents, as JSON for the page.
/// </summary>
/// <remarks>
/// A workspace's status is worked out as Home does, from the git status that's been read so far. This doesn't read any
/// itself, so a workspace shows as it would with no changes until Home or a tab has read its status. It follows
/// Home's reads as they happen.
/// </remarks>
public sealed class VisualiserFeed
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceStore _workspaceStore;
    private readonly WorkspaceAgents _agents;
    private readonly HomeGitStatus _gitStatus;
    private int _scheduled;
    private string? _last;

    public VisualiserFeed(IWorkspaceStore workspaceStore, WorkspaceAgents agents, HomeGitStatus gitStatus)
    {
        _workspaceStore = workspaceStore;
        _agents = agents;
        _gitStatus = gitStatus;
        _workspaceStore.Changed += OnSourceChanged;
        _agents.Changed += _ => OnSourceChanged();
        _gitStatus.Changed += OnSourceChanged;
    }

    /// <summary>
    /// Raised when the snapshot has changed: a workspace, its status or one of its agents. It can be raised on a
    /// background thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The current snapshot, as JSON.
    /// </summary>
    public async Task<string> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        var listings = await _workspaceStore.ListAsync(cancellationToken);
        var nodes = listings.Select(listing =>
        {
            var agents = _agents.For(listing.Id);
            var status = listing.Workspace is { } workspace && listing.Availability == WorkspaceAvailability.Available
                ? WorkspaceStatuses.Of(
                    _gitStatus.Get(workspace) is { IsLoaded: true } summary ? summary : new WorkspaceGitSummary([]),
                    agents.Select(agent => agent.State))
                : WorkspaceStatus.RequiresAttention;
            return new
            {
                id = listing.Id,
                name = listing.Name,
                status = status.ToString(),
                statusLabel = WorkspaceStatuses.Label(status),
                source = listing.Workspace?.Tasks.FirstOrDefault()?.Source ?? "local",
                agents = agents
                    .Select(agent => new { id = agent.Name, state = agent.State.ToString() })
                    .ToArray(),
            };
        });
        return JsonSerializer.Serialize(nodes, JsonOptions);
    }

    // Git status can change many times in a moment, so changes that come close together are looked at once, and
    // Changed is raised only if what the page would draw is different.
    private void OnSourceChanged()
    {
        if (Interlocked.Exchange(ref _scheduled, 1) == 0)
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
