namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// An <see cref="IFileSystemWatchService"/> built on <see cref="FileSystemWatcher"/>, which uses
/// <c>ReadDirectoryChangesW</c> on Windows and inotify on Linux.
/// </summary>
/// <remarks>
/// On Linux each watched folder uses an inotify watch. If the user's inotify limit is reached, the change listener
/// gets an error and a rescan request.
/// </remarks>
public sealed class FileSystemWatchService(TimeProvider timeProvider) : IFileSystemWatchService
{
    // The largest buffer Windows allows for watching network shares. A larger buffer makes overflows less likely.
    private const int BufferSize = 64 * 1024;

    /// <summary>
    /// Creates the service with the system clock.
    /// </summary>
    public FileSystemWatchService()
        : this(TimeProvider.System)
    {
    }

    /// <inheritdoc />
    public IDisposable Watch(
        string directory,
        Action<FileSystemChanges> onChanged,
        FileSystemWatchOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(onChanged);
        options ??= new FileSystemWatchOptions();

        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Couldn't find the folder '{directory}'.");
        }

        return new DirectoryWatch(directory, onChanged, options, timeProvider);
    }

    private sealed class DirectoryWatch : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly ChangeBatcher _batcher;

        public DirectoryWatch(
            string directory,
            Action<FileSystemChanges> onChanged,
            FileSystemWatchOptions options,
            TimeProvider timeProvider)
        {
            _batcher = new ChangeBatcher(options.QuietPeriod, options.MaxDelay, timeProvider, onChanged);
            _watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = options.IncludeSubdirectories,
                InternalBufferSize = BufferSize,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
            };

            _watcher.Created += OnChanged;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;

            try
            {
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _batcher.Dispose();
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => _batcher.AddPath(e.FullPath);

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            _batcher.AddPath(e.OldFullPath);
            _batcher.AddPath(e.FullPath);
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            _batcher.AddRescan(exception is InternalBufferOverflowException ? null : exception);
        }
    }
}
