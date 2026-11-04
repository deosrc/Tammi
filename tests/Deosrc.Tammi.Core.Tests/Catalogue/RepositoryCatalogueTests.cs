using Microsoft.Extensions.Time.Testing;
using Tammi.Core.Catalogue;
using Tammi.Core.Git;
using Tammi.Core.Settings;
using Tammi.Core.Tests.Git;

namespace Tammi.Core.Tests.Catalogue;

public sealed class RepositoryCatalogueTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly TempRepositories _repos = new();
    private readonly SettingsStore _settings;
    private readonly FakeFileSystemWatchService _watchService = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly RepositoryCatalogue _catalogue;

    public RepositoryCatalogueTests()
    {
        Directory.CreateDirectory(Monitored);
        _settings = new SettingsStore(_repos.PathTo("config"), []);
        _catalogue = new RepositoryCatalogue(_settings, new GitRepositoryReader(), _watchService, _time);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private string Monitored => _repos.PathTo("monitored");

    public void Dispose()
    {
        _catalogue.Dispose();
        _repos.Dispose();
    }

    [Fact]
    public async Task GetAsync_ListsMonitoredAndManuallyAddedRepositories()
    {
        string remote = await _repos.CreateRemoteAsync("web");
        string web = await _repos.CloneAsync(remote, Path.Combine("monitored", "acme", "web"));
        string api = await InitAsync(Path.Combine("monitored", "api"));
        string tool = await InitAsync(Path.Combine("elsewhere", "tool"));
        await SetSourcesAsync([Monitored], [tool]);

        var catalogue = await _catalogue.GetAsync(CancellationToken);

        Assert.Equal(
            [
                new CatalogueRepository { Path = api, MonitoredFolders = [Monitored] },
                new CatalogueRepository { Path = tool, IsManuallyAdded = true },
                new CatalogueRepository
                {
                    Path = web,
                    MonitoredFolders = [Monitored],
                    Remotes = [new GitRemote("origin", remote)],
                },
            ],
            catalogue.Repositories,
            CatalogueRepositoryComparer.Instance);
        Assert.Empty(catalogue.Problems);
        Assert.Equal(_time.GetUtcNow(), catalogue.ScannedAt);
        Assert.Same(catalogue, _catalogue.Current);
    }

    [Fact]
    public async Task GetAsync_ListsRepositoryFoundBySeveralSourcesOnce()
    {
        string api = await InitAsync(Path.Combine("monitored", "github", "api"));
        string github = Path.Combine(Monitored, "github");
        await SetSourcesAsync([Monitored, github], [api]);

        var catalogue = await _catalogue.GetAsync(CancellationToken);

        var repository = Assert.Single(catalogue.Repositories);
        Assert.Equal(api, repository.Path);
        Assert.Equal("api", repository.Name);
        Assert.True(repository.IsManuallyAdded);
        Assert.Equal([Monitored, github], repository.MonitoredFolders);
    }

    [Fact]
    public async Task GetAsync_ReportsSourcesThatCanNotBeCatalogued()
    {
        string missingFolder = _repos.PathTo("missing");
        string missingRepository = _repos.PathTo("gone");
        string plain = _repos.PathTo("plain");
        Directory.CreateDirectory(plain);
        string api = await InitAsync("api");
        await _repos.CommitFileAsync(api, "README.md", "# api\n", "Initial commit");
        string worktree = _repos.PathTo("api-feature");
        await _repos.RunAsync(api, "worktree", "add", "-b", "feature", worktree);
        // A .git folder that isn't a repository.
        string broken = _repos.PathTo("broken");
        Directory.CreateDirectory(Path.Combine(broken, ".git"));
        await SetSourcesAsync([missingFolder], [missingRepository, plain, worktree, broken]);

        var catalogue = await _catalogue.GetAsync(CancellationToken);

        Assert.Empty(catalogue.Repositories);
        Assert.Equal(
            [missingFolder, missingRepository, plain, worktree, broken],
            catalogue.Problems.Select(problem => problem.Path));
        Assert.Contains("worktree", catalogue.Problems[3].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_AfterFirstScan_DoesNotScanAgain()
    {
        await SetSourcesAsync([Monitored], []);
        var first = await _catalogue.GetAsync(CancellationToken);
        await InitAsync(Path.Combine("monitored", "api"));

        var second = await _catalogue.GetAsync(CancellationToken);

        Assert.Same(first, second);
        Assert.Empty(second.Repositories);
    }

    [Fact]
    public async Task RescanAsync_FindsNewRepositories()
    {
        await SetSourcesAsync([Monitored], []);
        await _catalogue.GetAsync(CancellationToken);
        string api = await InitAsync(Path.Combine("monitored", "deep", "down", "api"));

        var catalogue = await _catalogue.RescanAsync(CancellationToken);

        Assert.Equal(api, Assert.Single(catalogue.Repositories).Path);
    }

    [Fact]
    public async Task RescanAsync_RaisesChangedAsScanStartsAndEnds()
    {
        var scanning = new List<bool>();
        _catalogue.Changed += () => scanning.Add(_catalogue.IsScanning);

        await _catalogue.RescanAsync(CancellationToken);

        Assert.Equal([true, false], scanning);
        Assert.False(_catalogue.IsScanning);
    }

    [Fact]
    public async Task RescanAsync_ConcurrentRequests_AllSeeTheLatestSources()
    {
        string api = await InitAsync(Path.Combine("monitored", "api"));
        await SetSourcesAsync([Monitored], []);

        var scans = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => _catalogue.RescanAsync(CancellationToken)));

        Assert.All(scans, scan => Assert.Equal(api, Assert.Single(scan.Repositories).Path));
    }

    [Fact]
    public async Task SettingsChange_RescansWithNewSources()
    {
        await _catalogue.GetAsync(CancellationToken);
        string api = await InitAsync(Path.Combine("monitored", "api"));

        var catalogue = await WaitForScanAsync(() => SetSourcesAsync([Monitored], []));

        Assert.Equal(api, Assert.Single(catalogue.Repositories).Path);
    }

    [Fact]
    public async Task SettingsChange_BeforeFirstScan_DoesNotScan()
    {
        bool changed = false;
        _catalogue.Changed += () => changed = true;

        await SetSourcesAsync([Monitored], []);
        await Task.Delay(100, CancellationToken);

        Assert.False(changed);
        Assert.Null(_catalogue.Current.ScannedAt);
    }

    [Fact]
    public async Task SettingsChange_ToOtherSettings_DoesNotRescan()
    {
        await SetSourcesAsync([Monitored], []);
        await _catalogue.GetAsync(CancellationToken);
        bool changed = false;
        _catalogue.Changed += () => changed = true;

        await _settings.UpdateAsync(
            settings => settings with { Git = settings.Git with { StatusRefreshInterval = TimeSpan.FromMinutes(9) } },
            CancellationToken);
        await Task.Delay(100, CancellationToken);

        Assert.False(changed);
    }

    [Fact]
    public async Task Scan_WatchesTheMonitoredFoldersThatExist()
    {
        string other = _repos.PathTo("other");
        Directory.CreateDirectory(other);
        await SetSourcesAsync([Monitored, other, _repos.PathTo("missing")], []);

        await _catalogue.GetAsync(CancellationToken);

        Assert.Equal([Monitored, other], _watchService.WatchedFolders.Order());
        Assert.False(_watchService.OptionsFor(Monitored).IncludeSubdirectories);

        await WaitForScanAsync(() => SetSourcesAsync([other], []));

        Assert.Equal([other], _watchService.WatchedFolders);
    }

    [Fact]
    public async Task MonitoredFolderChange_Rescans()
    {
        await SetSourcesAsync([Monitored], []);
        await _catalogue.GetAsync(CancellationToken);
        string api = await InitAsync(Path.Combine("monitored", "api"));

        var catalogue = await WaitForScanAsync(() =>
        {
            _watchService.RaiseChange(Monitored, api);
            return Task.CompletedTask;
        });

        Assert.Equal(api, Assert.Single(catalogue.Repositories).Path);
    }

    [Fact]
    public async Task MonitoredFolderWatchError_WatchesTheFolderAgain()
    {
        await SetSourcesAsync([Monitored], []);
        await _catalogue.GetAsync(CancellationToken);

        await WaitForScanAsync(() =>
        {
            _watchService.RaiseChange(Monitored, Monitored, new IOException("The watch stopped."));
            return Task.CompletedTask;
        });

        Assert.Equal([Monitored], _watchService.WatchedFolders);
    }

    [Fact]
    public async Task Dispose_StopsWatching()
    {
        await SetSourcesAsync([Monitored], []);
        await _catalogue.GetAsync(CancellationToken);

        _catalogue.Dispose();

        Assert.Empty(_watchService.WatchedFolders);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _catalogue.RescanAsync(CancellationToken));
    }

    [Fact]
    public async Task CheckLocalRepository_AcceptsOnlyRepositoryTopFolders()
    {
        string api = await InitAsync("api");
        Directory.CreateDirectory(Path.Combine(api, "src"));

        Assert.Null(RepositoryCatalogue.CheckLocalRepository(api));
        Assert.NotNull(RepositoryCatalogue.CheckLocalRepository(Path.Combine(api, "src")));
        Assert.NotNull(RepositoryCatalogue.CheckLocalRepository(_repos.PathTo("missing")));
    }

    [Fact]
    public async Task CheckNewLocalRepository_RejectsRepositoriesInMonitoredFolders()
    {
        string inside = await InitAsync(Path.Combine("monitored", "deep", "api"));
        string outside = await InitAsync(Path.Combine("monitored-other", "web"));
        var settings = new GitSettings { MonitoredFolders = [Monitored] }.Normalize();

        Assert.Contains(Monitored, RepositoryCatalogue.CheckNewLocalRepository(inside, settings), StringComparison.Ordinal);
        // A folder whose name starts with the Monitored Folder's isn't in it.
        Assert.Null(RepositoryCatalogue.CheckNewLocalRepository(outside, settings));
        Assert.NotNull(RepositoryCatalogue.CheckNewLocalRepository(Path.Combine(outside, "missing"), settings));
    }

    [Fact]
    public async Task CheckNewLocalRepository_RejectsMonitoredFolderThatIsARepository()
    {
        string repository = await InitAsync("solo");
        var settings = new GitSettings { MonitoredFolders = [repository] }.Normalize();

        Assert.NotNull(RepositoryCatalogue.CheckNewLocalRepository(repository, settings));
    }

    [Fact]
    public void FindLocalRepositoriesIn_ListsTheRepositoriesInTheFolder()
    {
        string api = Path.Combine(Monitored, "api");
        string web = Path.Combine(Monitored, "acme", "web");
        string other = _repos.PathTo("monitored-other");
        var settings = new GitSettings { LocalRepositories = [api, other, web, Monitored] }.Normalize();

        Assert.Equal([api, web, Monitored], RepositoryCatalogue.FindLocalRepositoriesIn(Monitored, settings));
        Assert.Equal(
            [api],
            RepositoryCatalogue.FindLocalRepositoriesIn(api + Path.DirectorySeparatorChar, settings));
        Assert.Empty(RepositoryCatalogue.FindLocalRepositoriesIn(_repos.PathTo("elsewhere"), settings));
    }

    private async Task<string> InitAsync(string relativePath)
    {
        string path = _repos.PathTo(relativePath);
        await _repos.RunAsync(_repos.Root, "init", path);
        return path;
    }

    private Task SetSourcesAsync(IReadOnlyList<string> monitoredFolders, IReadOnlyList<string> localRepositories) =>
        _settings.UpdateAsync(
            settings => settings with
            {
                Git = settings.Git with { MonitoredFolders = monitoredFolders, LocalRepositories = localRepositories },
            },
            CancellationToken);

    /// <summary>
    /// Does <paramref name="action"/>, then waits for the scan it starts in the background to finish.
    /// </summary>
    private async Task<RepositoryCatalogueSnapshot> WaitForScanAsync(Func<Task> action)
    {
        var scanned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged()
        {
            if (!_catalogue.IsScanning)
            {
                scanned.TrySetResult();
            }
        }

        _catalogue.Changed += OnChanged;
        try
        {
            await action();
            await scanned.Task.WaitAsync(Timeout, CancellationToken);
            return _catalogue.Current;
        }
        finally
        {
            _catalogue.Changed -= OnChanged;
        }
    }

    /// <summary>
    /// Compares catalogue repositories by value, including their lists.
    /// </summary>
    private sealed class CatalogueRepositoryComparer : IEqualityComparer<CatalogueRepository>
    {
        public static CatalogueRepositoryComparer Instance { get; } = new();

        public bool Equals(CatalogueRepository? x, CatalogueRepository? y) =>
            x is not null && y is not null
            && x.Path == y.Path
            && x.IsManuallyAdded == y.IsManuallyAdded
            && x.MonitoredFolders.SequenceEqual(y.MonitoredFolders)
            && x.Remotes.SequenceEqual(y.Remotes);

        public int GetHashCode(CatalogueRepository obj) => obj.Path.GetHashCode(StringComparison.Ordinal);
    }
}
