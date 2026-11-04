using LibGit2Sharp;
using TAMMi.Core.Git;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// Keeps the git status of a workspace's repositories up to date, by watching their Workspace Worktrees for file
/// changes. It's used while the workspace is open in a tab.
/// </summary>
/// <remarks>
/// <para>
/// A worktree's own folder only holds its files. Its index and HEAD are in its git directory in the Local Repository
/// (<c>&lt;repo&gt;/.git/worktrees/&lt;name&gt;</c>, which also holds its submodules' git directories), and its
/// branches are in the Local Repository's refs. All of these are watched, so staging, committing and fetching update
/// the status too, whichever tool does them.
/// </para>
/// <para>
/// Each repository is read on its own, when something in it changes. Changes that arrive while it's being read are
/// collected into one more read.
/// </para>
/// </remarks>
public sealed class WorkspaceGitStatusMonitor : IDisposable
{
    private readonly IGitRepositoryReader _reader;
    private readonly IGitWorktreeService _worktrees;
    private readonly IFileSystemWatchService _watchService;
    private readonly RepositorySlot[] _slots;
    private volatile bool _started;
    private volatile bool _disposed;

    /// <summary>
    /// Creates the monitor. Nothing is read or watched until <see cref="Start"/> is called.
    /// </summary>
    public WorkspaceGitStatusMonitor(
        Workspace workspace,
        IGitRepositoryReader reader,
        IGitWorktreeService worktrees,
        IFileSystemWatchService watchService)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        Workspace = workspace;
        _reader = reader;
        _worktrees = worktrees;
        _watchService = watchService;
        _slots = workspace.Repositories
            .Select(repository => new RepositorySlot(
                new WorkspaceRepositoryStatus(repository, workspace.GetWorktreePath(repository))))
            .ToArray();
    }

    /// <summary>
    /// The workspace being monitored.
    /// </summary>
    public Workspace Workspace { get; }

    /// <summary>
    /// The status of each of the workspace's repositories, in the workspace's order.
    /// </summary>
    public IReadOnlyList<WorkspaceRepositoryStatus> Repositories => [.. _slots.Select(slot => slot.Current)];

    /// <summary>
    /// Raised when a repository's status has been read. It's raised on a background thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Reads every repository's status in the background, and starts watching them. Calling it again does nothing.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        foreach (var slot in _slots)
        {
            QueueRead(slot);
        }
    }

    /// <summary>
    /// Reads every repository's status now, and checks that the right folders are watched.
    /// </summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.WhenAll(_slots.Select(slot => Task.Run(() => ReadAsync(slot, cancellationToken), cancellationToken)));
    }

    /// <summary>
    /// Stops watching the repositories.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var slot in _slots)
        {
            slot.StopWatching();
        }
    }

    /// <summary>
    /// The folders to watch for a worktree: its own folder, its git directory, and the Local Repository's refs. Only
    /// folders that exist are returned.
    /// </summary>
    internal static IReadOnlyList<(string Folder, bool IncludeSubdirectories)> FoldersToWatch(WorktreeCheck check)
    {
        var folders = new List<(string, bool)> { (check.Path, true) };
        if (check.GitDirectory is { } gitDirectory)
        {
            folders.Add((gitDirectory, true));

            // <repo>/.git/worktrees/<name> → <repo>/.git. Only the common directory's own entries are watched (e.g.
            // packed-refs), not its objects or other worktrees, which change often and don't affect this status.
            if (Path.GetDirectoryName(Path.GetDirectoryName(gitDirectory)) is { } commonDirectory)
            {
                folders.Add((commonDirectory, false));
                folders.Add((Path.Combine(commonDirectory, "refs"), true));
            }
        }

        return folders.Where(folder => Directory.Exists(folder.Item1)).ToArray();
    }

    private void QueueRead(RepositorySlot slot)
    {
        // One read waiting is enough: it sees every change made before it starts.
        if (_disposed || Interlocked.Exchange(ref slot.ReadQueued, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ReadAsync(slot, CancellationToken.None).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The workspace was closed.
            }
        });
    }

    private async Task ReadAsync(RepositorySlot slot, CancellationToken cancellationToken)
    {
        // Reads of one repository are one at a time, so an older read can't replace a newer one's result.
        await slot.ReadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            // Changes from now on need another read.
            Volatile.Write(ref slot.ReadQueued, 0);

            WorktreeCheck check = _worktrees.CheckWorktree(slot.Current.WorktreePath);
            UpdateWatches(slot, check);
            slot.Current = Read(slot.Current, check);
        }
        finally
        {
            slot.ReadLock.Release();
        }

        if (!_disposed)
        {
            Changed?.Invoke();
        }
    }

    private WorkspaceRepositoryStatus Read(WorkspaceRepositoryStatus current, WorktreeCheck check)
    {
        var loaded = current with { Health = check.Health, IsLoaded = true };
        if (check.IsBroken)
        {
            return loaded with { Status = null, Problem = null };
        }

        try
        {
            return loaded with { Status = _reader.ReadStatus(current.WorktreePath), Problem = null };
        }
        catch (Exception ex) when (ex is GitException or LibGit2SharpException or IOException
            or UnauthorizedAccessException)
        {
            return loaded with { Status = null, Problem = ex.Message };
        }
    }

    private void UpdateWatches(RepositorySlot slot, WorktreeCheck check)
    {
        var wanted = FoldersToWatch(check);
        lock (slot.WatchLock)
        {
            if (_disposed)
            {
                return;
            }

            var wantedFolders = wanted.Select(folder => folder.Folder).ToHashSet(FileSystemPaths.Comparer);
            foreach (string folder in slot.Watches.Keys.Where(folder => !wantedFolders.Contains(folder)).ToArray())
            {
                slot.Watches.Remove(folder, out var watch);
                watch!.Dispose();
            }

            foreach (var (folder, includeSubdirectories) in wanted)
            {
                if (slot.Watches.ContainsKey(folder))
                {
                    continue;
                }

                try
                {
                    slot.Watches[folder] = _watchService.Watch(
                        folder,
                        changes => OnChanged(slot, folder, changes),
                        new FileSystemWatchOptions { IncludeSubdirectories = includeSubdirectories });
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    // The folder went away, or can't be watched. The next read tries again.
                }
            }
        }
    }

    private void OnChanged(RepositorySlot slot, string folder, FileSystemChanges changes)
    {
        if (changes.Error is not null)
        {
            // Watching may have stopped. The read watches the folder again.
            lock (slot.WatchLock)
            {
                if (slot.Watches.Remove(folder, out var watch))
                {
                    watch.Dispose();
                }
            }
        }

        QueueRead(slot);
    }

    private sealed class RepositorySlot(WorkspaceRepositoryStatus initial)
    {
        public readonly SemaphoreSlim ReadLock = new(1, 1);
        public readonly Lock WatchLock = new();
        public readonly Dictionary<string, IDisposable> Watches = new(FileSystemPaths.Comparer);
        public int ReadQueued;
        private volatile WorkspaceRepositoryStatus _current = initial;

        public WorkspaceRepositoryStatus Current
        {
            get => _current;
            set => _current = value;
        }

        public void StopWatching()
        {
            lock (WatchLock)
            {
                foreach (var watch in Watches.Values)
                {
                    watch.Dispose();
                }

                Watches.Clear();
            }
        }
    }
}
