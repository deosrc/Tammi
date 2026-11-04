using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests.Catalogue;

/// <summary>
/// An <see cref="IFileSystemWatchService"/> whose watches report changes only when a test raises them.
/// </summary>
internal sealed class FakeFileSystemWatchService : IFileSystemWatchService
{
    private readonly Lock _lock = new();
    private readonly List<FakeWatch> _watches = [];

    /// <summary>
    /// The folders being watched now.
    /// </summary>
    public IReadOnlyList<string> WatchedFolders
    {
        get
        {
            lock (_lock)
            {
                return [.. _watches.Where(watch => !watch.IsDisposed).Select(watch => watch.Directory)];
            }
        }
    }

    public IDisposable Watch(string directory, Action<FileSystemChanges> onChanged, FileSystemWatchOptions? options = null)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Couldn't find the folder '{directory}'.");
        }

        var watch = new FakeWatch(directory, onChanged, options ?? new FileSystemWatchOptions());
        lock (_lock)
        {
            _watches.Add(watch);
        }

        return watch;
    }

    /// <summary>
    /// The options the current watch of <paramref name="directory"/> was started with.
    /// </summary>
    public FileSystemWatchOptions OptionsFor(string directory) => Current(directory).Options;

    /// <summary>
    /// Reports a change to the current watch of <paramref name="directory"/>.
    /// </summary>
    public void RaiseChange(string directory, string changedPath, Exception? error = null) =>
        Current(directory).OnChanged(new FileSystemChanges(new HashSet<string> { changedPath }, false, error));

    private FakeWatch Current(string directory)
    {
        lock (_lock)
        {
            return _watches.Last(watch => !watch.IsDisposed && watch.Directory == directory);
        }
    }

    private sealed class FakeWatch(string directory, Action<FileSystemChanges> onChanged, FileSystemWatchOptions options)
        : IDisposable
    {
        public string Directory { get; } = directory;

        public Action<FileSystemChanges> OnChanged { get; } = onChanged;

        public FileSystemWatchOptions Options { get; } = options;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
