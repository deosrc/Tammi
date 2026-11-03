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
