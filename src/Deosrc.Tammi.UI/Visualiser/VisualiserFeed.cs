using System.Text.Json;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Visualiser;

/// <summary>
/// What the visualiser draws: the workspaces, each with its task source and agents, as JSON for the page.
/// </summary>
public sealed class VisualiserFeed
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceStore _workspaceStore;
    private readonly WorkspaceAgents _agents;

    public VisualiserFeed(IWorkspaceStore workspaceStore, WorkspaceAgents agents)
    {
        _workspaceStore = workspaceStore;
        _agents = agents;
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
        var nodes = listings.Select(listing => new
        {
            id = listing.Id,
            name = listing.Name,
            source = listing.Workspace?.Tasks.FirstOrDefault()?.Source ?? "local",
            agents = _agents.For(listing.Id)
                .Select(agent => new { id = agent.Name, state = agent.State.ToString() })
                .ToArray(),
        });
        return JsonSerializer.Serialize(nodes, JsonOptions);
    }

    private void OnChanged() => Changed?.Invoke();
}
