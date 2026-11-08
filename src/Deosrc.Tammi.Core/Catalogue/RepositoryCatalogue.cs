using System.Diagnostics;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Catalogue;

/// <summary>
/// Builds the Repository Catalogue from the local sources in the Repositories settings: the Monitored Folders (scanned with
/// <see cref="RepositoryScanner"/>) and the manually added local repositories.
/// </summary>
public sealed class RepositoryCatalogue : IRepositoryCatalogue, IDisposable
{
    /// <summary>
    /// How Monitored Folders are watched. Only their own entries are watched, not their subfolders: watching every
    /// folder in a large clone folder would use up the platform's watch limits.
    /// </summary>
    internal static readonly FileSystemWatchOptions WatchOptions = new()
    {
        IncludeSubdirectories = false,
        QuietPeriod = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(5),
    };

    private readonly ISettingsStore _settingsStore;
    private readonly IGitRepositoryReader _reader;
    private readonly IFileSystemWatchService _watchService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly Lock _watchLock = new();
    private readonly Dictionary<string, IDisposable> _watches = new(FileSystemPaths.Comparer);
    private volatile RepositoryCatalogueSnapshot? _current;
    private volatile GitSettings? _scannedSettings;
    private volatile bool _isScanning;
    private volatile bool _disposed;
    private int _requestedScans;
    private int _completedScans;

    /// <summary>
    /// Creates the catalogue. It isn't scanned until it's first asked for.
    /// </summary>
    public RepositoryCatalogue(
        ISettingsStore settingsStore,
        IGitRepositoryReader reader,
        IFileSystemWatchService watchService,
        TimeProvider timeProvider,
        ILogger<RepositoryCatalogue>? logger = null)
    {
        _settingsStore = settingsStore;
        _reader = reader;
        _watchService = watchService;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<RepositoryCatalogue>.Instance;
        _settingsStore.Changed += OnSettingsChanged;
    }

    /// <inheritdoc />
    public RepositoryCatalogueSnapshot Current => _current ?? RepositoryCatalogueSnapshot.Empty;

    /// <inheritdoc />
    public bool IsScanning => _isScanning;

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public Task<RepositoryCatalogueSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
        _current is { } current ? Task.FromResult(current) : RescanAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<RepositoryCatalogueSnapshot> RescanAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int request = Interlocked.Increment(ref _requestedScans);

        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A scan that started after this request was made has finished while it waited, so its result is
            // current enough.
            if (_current is { } current && Volatile.Read(ref _completedScans) >= request)
            {
                return current;
            }

            int scan = Volatile.Read(ref _requestedScans);
            GitSettings settings = _settingsStore.Current.Git;
            _isScanning = true;
            Changed?.Invoke();
            try
            {
                long started = Stopwatch.GetTimestamp();
                var snapshot = await Task.Run(() => Scan(settings, _reader, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
                snapshot = snapshot with { ScannedAt = _timeProvider.GetUtcNow() };
                _logger.LogInformation(
                    "Scanned the Repository Catalogue: {Count} repositories in {Elapsed:0} ms.",
                    snapshot.Repositories.Count,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                foreach (CatalogueProblem problem in snapshot.Problems)
                {
                    _logger.LogWarning("Couldn't catalogue {Path}: {Problem}", problem.Path, problem.Message);
                }

                _current = snapshot;
                _scannedSettings = settings;
                Volatile.Write(ref _completedScans, scan);
                UpdateWatches(settings.MonitoredFolders);
                return snapshot;
            }
            finally
            {
                _isScanning = false;
                Changed?.Invoke();
            }
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>
    /// Stops watching the Monitored Folders and the settings.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsStore.Changed -= OnSettingsChanged;
        lock (_watchLock)
        {
            foreach (var watch in _watches.Values)
            {
                watch.Dispose();
            }

            _watches.Clear();
        }
    }

    /// <summary>
    /// Builds the catalogue from the local sources in <paramref name="settings"/>.
    /// </summary>
    internal static RepositoryCatalogueSnapshot Scan(
        GitSettings settings,
        IGitRepositoryReader reader,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, CatalogueRepository>(FileSystemPaths.Comparer);
        var problems = new List<CatalogueProblem>();

        foreach (string folder in settings.MonitoredFolders)
        {
            if (!Directory.Exists(folder))
            {
                problems.Add(new CatalogueProblem(folder, "Couldn't find this folder."));
                continue;
            }

            foreach (string path in RepositoryScanner.FindRepositories(folder, cancellationToken))
            {
                var repository = found.GetValueOrDefault(path) ?? new CatalogueRepository { Path = path };
                found[path] = repository with { MonitoredFolders = [.. repository.MonitoredFolders, folder] };
            }
        }

        foreach (string path in settings.LocalRepositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CheckLocalRepository(path) is { } problem)
            {
                problems.Add(new CatalogueProblem(path, problem));
                continue;
            }

            var repository = found.GetValueOrDefault(path) ?? new CatalogueRepository { Path = path };
            found[path] = repository with { IsManuallyAdded = true };
        }

        var repositories = new List<CatalogueRepository>();
        foreach (var repository in found.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                repositories.Add(repository with { Remotes = reader.ReadRemotes(repository.Path) });
            }
            catch (Exception ex) when (ex is GitException or LibGit2SharpException or IOException
                or UnauthorizedAccessException)
            {
                problems.Add(new CatalogueProblem(
                    repository.Path,
                    $"Couldn't read this repository, so it isn't in the catalogue. {ex.Message}"));
            }
        }

        return new RepositoryCatalogueSnapshot
        {
            Repositories = repositories
                .OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(repository => repository.Path, FileSystemPaths.Comparer)
                .ToArray(),
            Problems = problems,
        };
    }

    /// <summary>
    /// Checks that <paramref name="path"/> can be added to the manually added local repositories in
    /// <paramref name="settings"/>. It must be a repository's top folder, and not inside a Monitored Folder: the scan
    /// already finds those.
    /// </summary>
    /// <returns>What's wrong with it, or <c>null</c> if it can be added.</returns>
    public static string? CheckNewLocalRepository(string path, GitSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);

        string? monitoredFolder = settings.MonitoredFolders
            .FirstOrDefault(folder => FileSystemPaths.IsSameOrInside(path, folder));
        return monitoredFolder is not null
            ? $"It's in the Monitored Folder '{monitoredFolder}', so it doesn't need adding."
            : CheckLocalRepository(path);
    }

    /// <summary>
    /// Lists the manually added local repositories in <paramref name="settings"/> that are in
    /// <paramref name="folder"/>, in the order they're in the settings. If <paramref name="folder"/> is added as a
    /// Monitored Folder, they should be removed from the manually added ones.
    /// </summary>
    public static IReadOnlyList<string> FindLocalRepositoriesIn(string folder, GitSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(settings);

        return settings.LocalRepositories
            .Where(repository => FileSystemPaths.IsSameOrInside(repository, folder))
            .ToArray();
    }

    /// <summary>
    /// Checks that <paramref name="path"/> is a repository's top folder, so it can be a manually added local
    /// repository.
    /// </summary>
    /// <returns>What's wrong with it, or <c>null</c> if it's fine.</returns>
    public static string? CheckLocalRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Directory.Exists(path))
        {
            return "Couldn't find this folder.";
        }

        return RepositoryScanner.Inspect(path) switch
        {
            RepositoryFolderKind.Repository => null,
            RepositoryFolderKind.LinkedWorktree =>
                "This folder is a git worktree. Add the repository it was created from instead.",
            _ => "This folder isn't the top folder of a git repository.",
        };
    }

    private void OnSettingsChanged()
    {
        // Until the catalogue is first asked for, there's nothing to keep up to date.
        if (_scannedSettings is not { } scanned)
        {
            return;
        }

        GitSettings settings = _settingsStore.Current.Git;
        if (!settings.MonitoredFolders.SequenceEqual(scanned.MonitoredFolders)
            || !settings.LocalRepositories.SequenceEqual(scanned.LocalRepositories))
        {
            RescanInBackground();
        }
    }

    private void OnMonitoredFolderChanged(string folder, FileSystemChanges changes)
    {
        if (changes.Error is not null)
        {
            // Watching may have stopped. The rescan watches the folder again.
            lock (_watchLock)
            {
                if (_watches.Remove(folder, out var watch))
                {
                    watch.Dispose();
                }
            }
        }

        RescanInBackground();
    }

    private void RescanInBackground()
    {
        if (_disposed)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RescanAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The app is closing.
            }
            catch (Exception ex)
            {
                // Nothing is waiting for this scan to report the failure to.
                _logger.LogError(ex, "Couldn't rescan the Repository Catalogue.");
            }
        });
    }

    private void UpdateWatches(IReadOnlyList<string> monitoredFolders)
    {
        lock (_watchLock)
        {
            if (_disposed)
            {
                return;
            }

            var wanted = new HashSet<string>(monitoredFolders, FileSystemPaths.Comparer);
            foreach (string folder in _watches.Keys.Where(folder => !wanted.Contains(folder)).ToArray())
            {
                _watches.Remove(folder, out var watch);
                watch!.Dispose();
            }

            foreach (string folder in wanted.Where(folder => !_watches.ContainsKey(folder)))
            {
                try
                {
                    _watches[folder] = _watchService.Watch(
                        folder,
                        changes => OnMonitoredFolderChanged(folder, changes),
                        WatchOptions);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _logger.LogDebug("Couldn't watch the Monitored Folder {Folder}: {Message}", folder, ex.Message);

                    // The folder is missing (reported as a problem) or can't be watched. It's watched again after
                    // the next scan, and can still be rescanned on request.
                }
            }
        }
    }
}
