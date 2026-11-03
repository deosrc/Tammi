namespace TAMMi.Core.Platform;

/// <summary>
/// Watches folders for file changes, e.g. to keep the git status of an open workspace up to date.
/// </summary>
public interface IFileSystemWatchService
{
    /// <summary>
    /// Starts watching a folder.
    /// </summary>
    /// <param name="directory">The folder to watch. It must exist.</param>
    /// <param name="onChanged">
    /// Called with each batch of changes. It's called on a background thread, one batch at a time.
    /// </param>
    /// <param name="options">How to watch the folder, or <c>null</c> for the defaults.</param>
    /// <returns>The watch. Dispose it to stop watching.</returns>
    /// <exception cref="DirectoryNotFoundException">The folder doesn't exist.</exception>
    IDisposable Watch(string directory, Action<FileSystemChanges> onChanged, FileSystemWatchOptions? options = null);
}

/// <summary>
/// Options for <see cref="IFileSystemWatchService.Watch"/>.
/// </summary>
public sealed record FileSystemWatchOptions
{
    /// <summary>
    /// Whether to watch subfolders too. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeSubdirectories { get; init; } = true;

    /// <summary>
    /// How long the folder must be quiet before a batch of changes is reported. Defaults to 250 ms.
    /// </summary>
    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The longest a change waits before it's reported, even if the folder keeps changing (e.g. during a build).
    /// Defaults to 2 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// A batch of file changes in a watched folder.
/// </summary>
/// <param name="Paths">The full paths of the files and folders that were created, changed, deleted or renamed.</param>
/// <param name="RescanRequired">
/// Whether changes may have been missed (e.g. because there were too many at once). The listener should then
/// treat everything in the folder as changed.
/// </param>
/// <param name="Error">
/// The watcher's error, if it had one. Watching may have stopped: if it matters, dispose the watch and start a new one.
/// </param>
public sealed record FileSystemChanges(
    IReadOnlySet<string> Paths,
    bool RescanRequired,
    Exception? Error = null);
