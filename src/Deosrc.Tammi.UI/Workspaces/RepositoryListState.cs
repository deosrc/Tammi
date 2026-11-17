using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// The Repositories page's rows. They're kept here, not in the page, so coming back to the tab shows what was read
/// last time straight away. They're read again when the user refreshes them or the catalogue is scanned again.
/// </summary>
public sealed class RepositoryListState : IDisposable
{
    private const int MaxParallelReads = 4;

    private readonly IRepositoryCatalogue _catalogue;
    private readonly IGitRepositoryReader _reader;
    private readonly IWorkspaceStore _workspaces;
    private readonly Lock _lock = new();
    private bool _started;

    public RepositoryListState(IRepositoryCatalogue catalogue, IGitRepositoryReader reader, IWorkspaceStore workspaces)
    {
        _catalogue = catalogue;
        _reader = reader;
        _workspaces = workspaces;
        _catalogue.Changed += OnCatalogueChanged;
    }

    /// <summary>
    /// The rows, sorted by name. Empty until the first read starts.
    /// </summary>
    public IReadOnlyList<RepositoryListRow> Rows { get; private set; } = [];

    /// <summary>
    /// Whether the rows are being read.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Raised when the rows change. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    public void Dispose() => _catalogue.Changed -= OnCatalogueChanged;

    /// <summary>
    /// Reads the rows if that hasn't been done yet.
    /// </summary>
    public Task EnsureLoadedAsync() => _started ? Task.CompletedTask : RefreshAsync();

    /// <summary>
    /// Reads every repository's state again. The rows already read stay until their new state arrives.
    /// </summary>
    public async Task RefreshAsync()
    {
        lock (_lock)
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            _started = true;
        }

        try
        {
            var snapshot = await _catalogue.GetAsync();
            var listings = await _workspaces.ListAsync();
            var previous = Rows.ToDictionary(row => row.Repository.Path);
            Rows =
            [
                .. snapshot.Repositories
                    .OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(repository => previous.TryGetValue(repository.Path, out var old)
                        ? old with { Repository = repository }
                        : new RepositoryListRow(repository, false, null, 0, null)),
            ];
            Changed?.Invoke();

            await Parallel.ForEachAsync(
                snapshot.Repositories,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelReads },
                async (repository, _) =>
                {
                    var row = await ReadAsync(repository, listings);
                    lock (_lock)
                    {
                        Rows = [.. Rows.Select(existing => existing.Repository.Path == row.Repository.Path ? row : existing)];
                    }

                    Changed?.Invoke();
                });
        }
        finally
        {
            IsLoading = false;
            Changed?.Invoke();
        }
    }

    private void OnCatalogueChanged()
    {
        if (_started && !_catalogue.IsScanning)
        {
            _ = RefreshAsync();
        }
    }

    private async Task<RepositoryListRow> ReadAsync(CatalogueRepository repository, IReadOnlyList<WorkspaceListing> listings)
    {
        var workspaces = listings.Count(listing => listing.Workspace?.Repositories.Any(
            used => string.Equals(used.LocalRepositoryPath, repository.Path, StringComparison.Ordinal)) == true);
        try
        {
            var status = await Task.Run(() => _reader.ReadStatus(repository.Path));
            return new RepositoryListRow(repository, true, status, workspaces, null);
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            return new RepositoryListRow(repository, true, null, workspaces, ex.Message);
        }
    }
}
