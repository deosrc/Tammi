using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The git status and Pull Requests that Home shows for each workspace.
/// </summary>
/// <remarks>
/// A workspace open in a tab, whose monitor and Pull Request list are running, is read from them, so it's live.
/// Any other workspace is read once each time <see cref="RefreshAsync"/> is called, and the result is kept until the
/// next one.
/// </remarks>
public sealed class HomeGitStatus
{
    private readonly WorkspaceGitStatusMonitors _monitors;
    private readonly WorkspacePullRequestLists _pullRequestLists;
    private readonly IGitRepositoryReader _reader;
    private readonly IGitWorktreeService _worktrees;
    private readonly IntegrationRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, WorkspaceGitSummary> _read = [];

    public HomeGitStatus(
        WorkspaceGitStatusMonitors monitors,
        WorkspacePullRequestLists pullRequestLists,
        IGitRepositoryReader reader,
        IGitWorktreeService worktrees,
        IntegrationRegistry registry,
        TimeProvider timeProvider)
    {
        _monitors = monitors;
        _pullRequestLists = pullRequestLists;
        _reader = reader;
        _worktrees = worktrees;
        _registry = registry;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Raised when a workspace's summary may have changed. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Returns a workspace's summary as it stands now. Repositories that haven't been read yet aren't loaded.
    /// </summary>
    public WorkspaceGitSummary Get(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        WorkspaceGitStatusMonitor? monitor = _monitors.Find(workspace.Id);
        if (monitor is not null && SameRepositories(monitor.Workspace, workspace))
        {
            var pullRequests = _pullRequestLists.Find(workspace.Id)?.Repositories;
            return Combine(workspace, monitor.Repositories, pullRequests);
        }

        lock (_lock)
        {
            if (_read.TryGetValue(workspace.Id, out var kept) && SameRepositories(kept, workspace))
            {
                return kept;
            }
        }

        return Combine(workspace, [], null);
    }

    /// <summary>
    /// Reads the given workspaces again, except those that are live: git status and Pull Requests. Workspaces are read
    /// one at a time, so a long list doesn't start many git processes at once.
    /// </summary>
    public async Task RefreshAsync(IEnumerable<Workspace> workspaces, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        Workspace[] toRead = [.. workspaces];

        lock (_lock)
        {
            // Forget workspaces that are no longer listed.
            var ids = toRead.Select(workspace => workspace.Id).ToHashSet(StringComparer.Ordinal);
            foreach (string id in _read.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                _read.Remove(id);
            }
        }

        foreach (Workspace workspace in toRead)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_monitors.Find(workspace.Id) is { } monitor && SameRepositories(monitor.Workspace, workspace))
            {
                // Live: its own monitor keeps it current, and its Pull Request list reads on its timer.
                continue;
            }

            WorkspaceGitSummary summary = await ReadAsync(workspace, cancellationToken).ConfigureAwait(false);
            lock (_lock)
            {
                _read[workspace.Id] = summary;
            }

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Calls <paramref name="onChanged"/> whenever a live workspace among <paramref name="workspaces"/> reads a
    /// repository's git status or Pull Requests. Only workspaces that are live now are followed, so call it again when
    /// workspaces open or close.
    /// </summary>
    /// <returns>A subscription that stops the calls when disposed.</returns>
    public IDisposable Observe(IEnumerable<Workspace> workspaces, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(onChanged);

        var subscription = new Subscription();
        foreach (Workspace workspace in workspaces)
        {
            if (_monitors.Find(workspace.Id) is { } monitor)
            {
                monitor.Changed += onChanged;
                subscription.Add(() => monitor.Changed -= onChanged);
            }

            if (_pullRequestLists.Find(workspace.Id) is { } list)
            {
                list.Changed += onChanged;
                subscription.Add(() => list.Changed -= onChanged);
            }
        }

        return subscription;
    }

    private async Task<WorkspaceGitSummary> ReadAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        using var monitor = new WorkspaceGitStatusMonitor(workspace, _reader, _worktrees, NoFileSystemWatches.Instance);
        using var list = new WorkspacePullRequestList(workspace, _registry, _reader, _timeProvider);
        await Task.WhenAll(monitor.RefreshAsync(cancellationToken), list.RefreshAsync()).ConfigureAwait(false);
        return Combine(workspace, monitor.Repositories, list.Repositories);
    }

    private static WorkspaceGitSummary Combine(
        Workspace workspace,
        IReadOnlyList<WorkspaceRepositoryStatus> statuses,
        IReadOnlyList<WorkspaceRepositoryPullRequests>? pullRequests) =>
        new([.. workspace.Repositories.Select(repository => new HomeRepositoryStatus(
            statuses.FirstOrDefault(status => status.Repository == repository)
                ?? new WorkspaceRepositoryStatus(repository, workspace.GetWorktreePath(repository)),
            pullRequests?.FirstOrDefault(list => list.Repository == repository)
                ?? new WorkspaceRepositoryPullRequests(repository)))]);

    private static bool SameRepositories(Workspace a, Workspace b) =>
        a.Folder == b.Folder && a.Repositories.SequenceEqual(b.Repositories);

    private static bool SameRepositories(WorkspaceGitSummary summary, Workspace workspace) =>
        summary.Repositories.Select(repository => repository.Git.Repository).SequenceEqual(workspace.Repositories)
            && summary.Repositories.All(repository =>
                repository.Git.WorktreePath == workspace.GetWorktreePath(repository.Git.Repository));

    private sealed class Subscription : IDisposable
    {
        private readonly List<Action> _undo = [];

        public void Add(Action undo) => _undo.Add(undo);

        public void Dispose()
        {
            foreach (Action undo in _undo)
            {
                undo();
            }

            _undo.Clear();
        }
    }
}
