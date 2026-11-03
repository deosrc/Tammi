namespace TAMMi.Core.Catalogue;

/// <summary>
/// The Repository Catalogue: the app-level list of known repositories, collected from the repository sources.
/// </summary>
/// <remarks>
/// The catalogue is scanned the first time it's asked for (<see cref="GetAsync"/>). After that it's scanned again on
/// request (<see cref="RescanAsync"/>), when the Monitored Folders or manually added local repositories change in the
/// settings, and when folders are added to, removed from or renamed in a Monitored Folder.
/// </remarks>
public interface IRepositoryCatalogue
{
    /// <summary>
    /// The catalogue from the latest scan, or <see cref="RepositoryCatalogueSnapshot.Empty"/> before the first.
    /// </summary>
    RepositoryCatalogueSnapshot Current { get; }

    /// <summary>
    /// Whether a scan is running.
    /// </summary>
    bool IsScanning { get; }

    /// <summary>
    /// Raised when a scan starts and when it ends. It can be raised on any thread.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Returns the catalogue, scanning it first if it hasn't been scanned yet.
    /// </summary>
    Task<RepositoryCatalogueSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the repository sources again. If a scan is running, it waits for it and then scans again, so the
    /// result reflects the sources as they are now.
    /// </summary>
    Task<RepositoryCatalogueSnapshot> RescanAsync(CancellationToken cancellationToken = default);
}
