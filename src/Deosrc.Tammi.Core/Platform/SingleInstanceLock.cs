using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Stops a second copy of Tammi running at once, which would open the same agents again. The running copy holds an
/// exclusive lock on <c>instance.lock</c> in the app's data folder (<see cref="IAppPaths.DataDirectory"/>). The
/// operating system releases the lock when the process ends, even if it crashes.
/// </summary>
/// <remarks>
/// A copy that finds Tammi already running can ask it to show its window (see <see cref="SignalRunningInstance"/>),
/// which the running copy hears through <see cref="Activated"/>. They talk over a named pipe.
/// </remarks>
public sealed class SingleInstanceLock : IDisposable
{
    /// <summary>
    /// The lock file's name.
    /// </summary>
    public const string FileName = "instance.lock";

    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(2);

    private readonly FileStream _stream;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _listening;

    private SingleInstanceLock(FileStream stream, string pipeName)
    {
        _stream = stream;
        _pipeName = pipeName;
    }

    /// <summary>
    /// Raised, on a background thread, when another copy of Tammi was started and asked this one to show its window.
    /// </summary>
    public event Action? Activated;

    /// <summary>
    /// The message to show when <see cref="TryAcquire"/> found Tammi already running and it couldn't be asked to show
    /// its window.
    /// </summary>
    public static string AlreadyRunningMessage =>
        $"{AppInfo.Name} is already running. Only one copy can run at a time.";

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
            return new SingleInstanceLock(stream, PipeName(directory));
        }
        catch (IOException) when (File.Exists(path))
        {
            // The file exists and can't be shared, so another process has it.
            return null;
        }
    }

    /// <summary>
    /// Asks the copy of Tammi that holds the lock in <paramref name="directory"/> to show its window.
    /// </summary>
    /// <returns>Whether the running copy was reached.</returns>
    public static bool SignalRunningInstance(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName(directory), PipeDirection.Out);
            client.Connect((int)SignalTimeout.TotalMilliseconds);
            client.WriteByte(1);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts listening for other copies asking this one to show its window. Call it after subscribing to
    /// <see cref="Activated"/>. Calling it again does nothing.
    /// </summary>
    public void StartListening() => _listening ??= Task.Run(ListenAsync);

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Cancel();
        _stream.Dispose();
        _stopping.Dispose();
    }

    private static string PipeName(string directory)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory)));
        return "tammi-" + Convert.ToHexString(hash, 0, 8);
    }

    private async Task ListenAsync()
    {
        var token = _stopping.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                var message = new byte[1];
                _ = await server.ReadAsync(message, token).ConfigureAwait(false);
                Activated?.Invoke();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                // A client that left early, or a pipe that couldn't be made: try again shortly, unless closing.
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
