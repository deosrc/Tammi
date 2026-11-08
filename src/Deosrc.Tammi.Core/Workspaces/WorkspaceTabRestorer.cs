using Deosrc.Tammi.Core.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Remembers which agents and git graphs are open in each workspace, and opens them again the first time the workspace
/// is opened after Tammi restarts. Terminals aren't remembered.
/// </summary>
/// <remarks>
/// The tabs are kept in the workspace's <c>.tammi/open-tabs.json</c> (see <see cref="SavedTabsFileStore"/>), and
/// updated as soon as an agent or graph opens or closes. They aren't updated as Tammi closes and ends every agent, so
/// the tabs open then are the ones restored. An agent is restored by resuming its Agent Session, so one whose session
/// can't be resumed isn't kept.
/// </remarks>
public sealed class WorkspaceTabRestorer : IDisposable
{
    private readonly WorkspaceAgents _agents;
    private readonly WorkspaceGitGraphs _graphs;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Tracked> _tracked = [];
    private readonly SemaphoreSlim _saving = new(1, 1);

    /// <summary>
    /// Creates a restorer that follows the agents and graphs.
    /// </summary>
    public WorkspaceTabRestorer(
        WorkspaceAgents agents,
        WorkspaceGitGraphs graphs,
        ILogger<WorkspaceTabRestorer>? logger = null)
    {
        _agents = agents;
        _graphs = graphs;
        _logger = logger ?? NullLogger<WorkspaceTabRestorer>.Instance;
        _agents.Changed += OnChanged;
        _graphs.Changed += OnChanged;
    }

    /// <summary>
    /// Opens the agents and git graphs a workspace had open when Tammi last closed, then starts keeping track of its
    /// tabs. It does this only the first time it's called for a workspace, so a workspace whose tab is closed and
    /// reopened keeps the tabs it has.
    /// </summary>
    /// <returns>Why tabs couldn't be opened again, one message for each.</returns>
    public async Task<IReadOnlyList<string>> RestoreAsync(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var tracked = new Tracked(workspace, new SavedTabsFileStore(
            Path.Combine(workspace.MetadataFolder, SavedTabsFileStore.DefaultFileName),
            _logger));
        lock (_lock)
        {
            if (!_tracked.TryAdd(workspace.Id, tracked))
            {
                return [];
            }
        }

        var problems = new List<string>();
        try
        {
            foreach (var saved in await tracked.Store.ListAsync().ConfigureAwait(false))
            {
                try
                {
                    if (await RestoreTabAsync(workspace, saved).ConfigureAwait(false) is { } problem)
                    {
                        problems.Add(problem);
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    _logger.LogWarning(ex, "Couldn't open a saved {Kind} tab of the workspace {Workspace}.", saved.Kind, workspace.Id);
                    problems.Add($"Couldn't open a {saved.Kind} tab again. {ex.Message}");
                }
            }
        }
        finally
        {
            tracked.Restoring = false;
        }

        await SaveAsync(workspace.Id).ConfigureAwait(false);
        return problems;
    }

    /// <summary>
    /// Stops keeping track of a workspace's tabs and leaves what's saved as it is, e.g. as the workspace is deleted.
    /// </summary>
    public void Forget(string workspaceId)
    {
        lock (_lock)
        {
            _tracked.Remove(workspaceId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _agents.Changed -= OnChanged;
        _graphs.Changed -= OnChanged;
    }

    private async Task<string?> RestoreTabAsync(Workspace workspace, SavedTab saved)
    {
        switch (saved.Kind)
        {
            case SavedTab.AgentKind when AgentSession.IsValidId(saved.SessionId):
                if (_agents.IsRunning(workspace.Id, saved.SessionId!))
                {
                    return null;
                }

                var sessions = await _agents.SessionsOf(workspace).ListAsync().ConfigureAwait(false);
                if (sessions.FirstOrDefault(session => string.Equals(
                        session.Id, saved.SessionId, StringComparison.OrdinalIgnoreCase)) is not { } found)
                {
                    return "An agent that was open couldn't be opened again, as its session isn't recorded any more.";
                }

                await _agents.ResumeAsync(workspace, found).ConfigureAwait(false);
                return null;

            case SavedTab.GitGraphKind when saved.Repository is not null:
                if (workspace.Repositories.FirstOrDefault(repository => repository.FolderName == saved.Repository)
                    is not { } repository)
                {
                    return $"The git graph of {saved.Repository} couldn't be opened again, as it's no longer in the workspace.";
                }

                _graphs.Open(workspace, repository, saved.SubmodulePath);
                return null;

            default:
                return null;
        }
    }

    private void OnChanged(string workspaceId)
    {
        lock (_lock)
        {
            if (!_tracked.TryGetValue(workspaceId, out var tracked) || tracked.Restoring)
            {
                return;
            }
        }

        _ = SaveAsync(workspaceId);
    }

    private async Task SaveAsync(string workspaceId)
    {
        await _saving.WaitAsync().ConfigureAwait(false);
        try
        {
            Tracked? tracked;
            lock (_lock)
            {
                if (!_tracked.TryGetValue(workspaceId, out tracked) || tracked.Restoring)
                {
                    return;
                }
            }

            // Tammi ending every agent as it closes isn't the user closing their tabs.
            if (_agents.IsDisposed || !Directory.Exists(tracked.Workspace.MetadataFolder))
            {
                return;
            }

            var tabs = _agents.For(workspaceId)
                .Where(agent => agent.Agent.SessionId is not null)
                .Select(agent => (agent.StartOrder, Tab: new SavedTab(SavedTab.AgentKind, SessionId: agent.Agent.SessionId)))
                .Concat(_graphs.For(workspaceId).Select(graph => (graph.StartOrder, Tab: new SavedTab(
                    SavedTab.GitGraphKind,
                    Repository: graph.Repository.FolderName,
                    SubmodulePath: graph.SubmodulePath))))
                .OrderBy(entry => entry.StartOrder)
                .Select(entry => entry.Tab)
                .ToArray();
            if (tracked.Saved is { } saved && saved.SequenceEqual(tabs))
            {
                return;
            }

            await tracked.Store.SetAsync(tabs).ConfigureAwait(false);
            tracked.Saved = tabs;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't save the open tabs of the workspace {Workspace}.", workspaceId);
        }
        finally
        {
            _saving.Release();
        }
    }

    private sealed class Tracked(Workspace workspace, SavedTabsFileStore store)
    {
        public Workspace Workspace { get; } = workspace;

        public SavedTabsFileStore Store { get; } = store;

        public bool Restoring { get; set; } = true;

        public IReadOnlyList<SavedTab>? Saved { get; set; }
    }
}
