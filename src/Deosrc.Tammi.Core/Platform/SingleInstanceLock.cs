namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Stops a second copy of Tammi running at once, which would open the same agents again. The running copy holds an
/// exclusive lock on <c>instance.lock</c> in the app's data folder (<see cref="IAppPaths.DataDirectory"/>). The
/// operating system releases the lock when the process ends, even if it crashes.
/// </summary>
public sealed class SingleInstanceLock : IDisposable
{
    /// <summary>
    /// The lock file's name.
    /// </summary>
    public const string FileName = "instance.lock";

    private readonly FileStream _stream;

    private SingleInstanceLock(FileStream stream) => _stream = stream;

    /// <summary>
    /// Takes the lock, unless another process holds it.
    /// </summary>
    /// <returns>The lock, to keep until the app exits, or <c>null</c> if Tammi is already running.</returns>
    /// <exception cref="IOException">The lock file couldn't be created for another reason.</exception>
    /// <exception cref="UnauthorizedAccessException">The lock file couldn't be created.</exception>
    public static SingleInstanceLock? TryAcquire(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstanceLock(stream);
        }
        catch (IOException) when (File.Exists(path))
        {
            // The file exists and can't be shared, so another process has it.
            return null;
        }
    }

    /// <summary>
    /// The message to show when <see cref="TryAcquire"/> found Tammi already running.
    /// </summary>
    public static string AlreadyRunningMessage =>
        $"{AppInfo.Name} is already running. Only one copy can run at a time.";

    /// <inheritdoc />
    public void Dispose() => _stream.Dispose();
}
