using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A watch service that watches nothing, for reading a workspace's git status once.
/// </summary>
internal sealed class NoFileSystemWatches : IFileSystemWatchService
{
    public static readonly NoFileSystemWatches Instance = new();

    public IDisposable Watch(string directory, Action<FileSystemChanges> onChanged, FileSystemWatchOptions? options = null) =>
        NoWatch.Instance;

    private sealed class NoWatch : IDisposable
    {
        public static readonly NoWatch Instance = new();

        public void Dispose()
        {
        }
    }
}
