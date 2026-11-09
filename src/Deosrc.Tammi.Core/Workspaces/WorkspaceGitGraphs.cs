using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The git graphs open in every workspace. Like terminals, they stay open when a workspace's tab closes, so it shows
/// them again when it reopens.
/// </summary>
public sealed class WorkspaceGitGraphs(IGitRepositoryReader reader, IGitWorkingTree workingTree, ISettingsStore settings) : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<WorkspaceGitGraph> _graphs = [];

    /// <summary>
    /// Raised with a workspace's ID when one of its graphs opens or closes. It can be raised on a background thread.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// A workspace's open graphs, in the order they were opened.
    /// </summary>
    public IReadOnlyList<WorkspaceGitGraph> For(string workspaceId)
    {
        lock (_lock)
        {
            return _graphs.Where(graph => graph.WorkspaceId == workspaceId).ToArray();
        }
    }

    /// <summary>
    /// Opens the graph of a workspace's repository, or of one of its submodules, showing the commits the settings
    /// choose. If it's already open, returns that graph instead. Nothing is read until the graph is loaded.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="submodulePath">
    /// The submodule's path in the repository, with <c>/</c> separators, or <see langword="null"/> for the repository.
    /// </param>
    public WorkspaceGitGraph Open(Workspace workspace, WorkspaceRepository repository, string? submodulePath = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(repository);

        WorkspaceGitGraph graph;
        lock (_lock)
        {
            if (_graphs.Find(open => open.WorkspaceId == workspace.Id
                    && open.Repository.FolderName == repository.FolderName
                    && open.SubmodulePath == submodulePath) is { } existing)
            {
                return existing;
            }

            graph = new WorkspaceGitGraph(workspace, repository, submodulePath, settings.Current.Git.GraphScope, reader, workingTree);
            _graphs.Add(graph);
        }

        Changed?.Invoke(workspace.Id);
        return graph;
    }

    /// <summary>
    /// Closes a graph. Closing one that's already closed does nothing.
    /// </summary>
    public void Close(WorkspaceGitGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        lock (_lock)
        {
            if (!_graphs.Remove(graph))
            {
                return;
            }
        }

        graph.Dispose();
        Changed?.Invoke(graph.WorkspaceId);
    }

    /// <summary>
    /// Closes all of a workspace's graphs, e.g. before it's deleted.
    /// </summary>
    public void CloseAll(string workspaceId)
    {
        foreach (WorkspaceGitGraph graph in For(workspaceId))
        {
            Close(graph);
        }
    }

    /// <summary>
    /// Closes every graph, as the app closes.
    /// </summary>
    public void Dispose()
    {
        WorkspaceGitGraph[] graphs;
        lock (_lock)
        {
            graphs = [.. _graphs];
            _graphs.Clear();
        }

        foreach (WorkspaceGitGraph graph in graphs)
        {
            graph.Dispose();
        }
    }
}
