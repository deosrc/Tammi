using System.Text.Json;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// What the visualiser draws: the workspaces, each with its status, task source and agents, as JSON for the page.
/// </summary>
/// <remarks>
/// A workspace's status is worked out as Home does, from the git status that's been read so far. This doesn't read any
/// itself, so a workspace shows as it would with no changes until Home or a tab has read its status.
/// </remarks>
public sealed class VisualiserFeed
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceStore _workspaceStore;
    private readonly WorkspaceAgents _agents;
    private readonly HomeGitStatus _gitStatus;

    public VisualiserFeed(IWorkspaceStore workspaceStore, WorkspaceAgents agents, HomeGitStatus gitStatus)
    {
        _workspaceStore = workspaceStore;
        _agents = agents;
        _gitStatus = gitStatus;
        _workspaceStore.Changed += OnChanged;
        _agents.Changed += _ => OnChanged();
    }

    /// <summary>
    /// Raised when a workspace or one of its agents changes. It can be raised on a background thread.
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

    private void OnChanged() => Changed?.Invoke();
}
