namespace Deosrc.Tammi.Core.Platform;

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
