using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The Pull Requests from the workspace branch of each of a workspace's repositories. It's used while the workspace is
/// open in a tab, and reads them again every <see cref="RefreshInterval"/>, since they change on the service (e.g. a
/// check finishing) without anything changing on disk.
/// </summary>
/// <remarks>
/// A repository's remotes say which integrations to ask: each <see cref="IPullRequestIntegration"/> looks for the
/// branch in the repositories on its own service. Each repository is read on its own, so a slow or failing service
/// doesn't hold up the others. While they're read again, the previous Pull Requests are kept until the new ones arrive.
/// </remarks>
public sealed class WorkspacePullRequestList : IDisposable
{
    /// <summary>
    /// How often the Pull Requests are read again, by default.
    /// </summary>
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(2);

    private readonly IntegrationRegistry _registry;
    private readonly IGitRepositoryReader _reader;
    private readonly Lock _lock = new();
    private readonly WorkspaceRepositoryPullRequests[] _repositories;
    private readonly ITimer _timer;
    private CancellationTokenSource? _refresh;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Creates the list. Nothing is read until <see cref="Start"/> or <see cref="Refresh"/> is called.
    /// </summary>
    /// <param name="workspace">The workspace whose repositories to read.</param>
    /// <param name="registry">The integrations to ask.</param>
    /// <param name="reader">Reads the repositories' remotes.</param>
    /// <param name="timeProvider">Times the reads every <paramref name="refreshInterval"/>.</param>
    /// <param name="refreshInterval">How often to read again, or <see cref="DefaultRefreshInterval"/>.</param>
    public WorkspacePullRequestList(
        Workspace workspace,
        IntegrationRegistry registry,
        IGitRepositoryReader reader,
        TimeProvider timeProvider,
        TimeSpan? refreshInterval = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Workspace = workspace;
        RefreshInterval = refreshInterval ?? DefaultRefreshInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RefreshInterval, TimeSpan.Zero, nameof(refreshInterval));
        _registry = registry;
        _reader = reader;
        _repositories = [.. workspace.Repositories.Select(repository => new WorkspaceRepositoryPullRequests(repository))];
        _timer = timeProvider.CreateTimer(_ => OnTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// The workspace whose Pull Requests these are.
    /// </summary>
    public Workspace Workspace { get; }

    /// <summary>
    /// How often the Pull Requests are read again once started.
    /// </summary>
    public TimeSpan RefreshInterval { get; }

    /// <summary>
    /// The Pull Requests of each of the workspace's repositories, in the workspace's order.
    /// </summary>
    public IReadOnlyList<WorkspaceRepositoryPullRequests> Repositories
    {
        get
        {
            lock (_lock)
            {
                return [.. _repositories];
            }
        }
    }

    /// <summary>
    /// Raised when a repository's Pull Requests have been read. It's raised on a background thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Finds the Pull Requests of one of the workspace's repositories.
    /// </summary>
    /// <returns>Them, or <see langword="null"/> if the workspace has no repository in that folder.</returns>
    public WorkspaceRepositoryPullRequests? Find(string folderName) =>
        Repositories.FirstOrDefault(repository => repository.Repository.FolderName == folderName);

    /// <summary>
    /// Reads the Pull Requests in the background, and then every <see cref="RefreshInterval"/>. Calling it again does
    /// nothing.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
        }

        Refresh();
    }

    /// <summary>
    /// Reads every repository's Pull Requests again in the background. A read that's under way is cancelled.
    /// </summary>
    public void Refresh() => _ = RefreshAsync();

    /// <summary>
    /// Reads every repository's Pull Requests again. A read that's under way is cancelled. Once started, the next
    /// timed read is a full <see cref="RefreshInterval"/> later.
    /// </summary>
    /// <returns>A task that completes when every repository has been read, or a newer read or disposing cancels it.</returns>
    public Task RefreshAsync()
    {
        CancellationToken token;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _refresh?.Cancel();
            _refresh?.Dispose();
            _refresh = new CancellationTokenSource();
            token = _refresh.Token;
            if (_started)
            {
                _timer.Change(RefreshInterval, RefreshInterval);
            }
        }

        return Task.WhenAll(_repositories
            .Select((repository, slot) => (repository.Repository, slot))
            .Select(read => Task.Run(() => ReadAsync(read.slot, read.Repository, token), CancellationToken.None)));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            _refresh?.Cancel();
            _refresh?.Dispose();
            _refresh = null;
        }
    }

    private void OnTimer()
    {
        try
        {
            Refresh();
        }
        catch (ObjectDisposedException)
        {
            // It was disposed as the timer fired.
        }
    }

    private async Task ReadAsync(int slot, WorkspaceRepository repository, CancellationToken token)
    {
        (IReadOnlyList<PullRequestInfo>? pullRequests, string? problem) =
            await ReadPullRequestsAsync(repository, token).ConfigureAwait(false);
        lock (_lock)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var previous = _repositories[slot];
            _repositories[slot] = previous with
            {
                IsLoaded = true,
                PullRequests = pullRequests ?? previous.PullRequests,
                Problem = problem,
            };
        }

        Changed?.Invoke();
    }

    /// <returns>
    /// The Pull Requests, or <see langword="null"/> if any integration failed (with why), or if reading was cancelled.
    /// </returns>
    private async Task<(IReadOnlyList<PullRequestInfo>? PullRequests, string? Problem)> ReadPullRequestsAsync(
        WorkspaceRepository repository,
        CancellationToken token)
    {
        IReadOnlyList<IPullRequestIntegration> integrations = _registry.PullRequestIntegrations;
        if (integrations.Count == 0)
        {
            return ([], null);
        }

        string[] remoteUrls;
        try
        {
            // A worktree shares its Local Repository's remotes, which can be read even when the worktree is broken.
            remoteUrls =
            [
                .. _reader.ReadRemotes(repository.LocalRepositoryPath)
                    .SelectMany(remote => remote.PushUrl is { } pushUrl ? [remote.Url, pushUrl] : new[] { remote.Url })
                    .Distinct(StringComparer.Ordinal),
            ];
        }
        catch (Exception exception) when (exception is GitException or LibGit2Sharp.LibGit2SharpException
            or IOException or UnauthorizedAccessException)
        {
            // A missing or unreadable Local Repository has no Pull Requests to show. Its card says what's wrong.
            return ([], null);
        }

        if (remoteUrls.Length == 0)
        {
            return ([], null);
        }

        var pullRequests = new List<PullRequestInfo>();
        foreach (IPullRequestIntegration integration in integrations)
        {
            try
            {
                pullRequests.AddRange(await integration
                    .GetPullRequestsAsync(remoteUrls, repository.Branch, token)
                    .ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return (null, null);
            }
            catch (Exception exception)
            {
                // Any failure is shown on the repository rather than lost on a background thread.
                return (null, $"Couldn't read the pull requests from {integration.Name}. {exception.Message}");
            }
        }

        return (pullRequests, null);
    }
}
