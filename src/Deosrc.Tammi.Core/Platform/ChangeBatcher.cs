namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Collects file changes and reports them in batches: once the changes stop for a quiet period, or once the oldest
/// change has waited for the maximum delay.
/// </summary>
internal sealed class ChangeBatcher : IDisposable
{
    private readonly TimeSpan _quietPeriod;
    private readonly TimeSpan _maxDelay;
    private readonly TimeProvider _timeProvider;
    private readonly Action<FileSystemChanges> _onFlush;
    private readonly ITimer _timer;
    private readonly Lock _lock = new();
    private readonly Lock _flushLock = new();

    private HashSet<string> _paths = new(PathComparer);
    private bool _rescanRequired;
    private Exception? _error;
    private long? _batchStarted;
    private bool _disposed;

    public ChangeBatcher(
        TimeSpan quietPeriod,
        TimeSpan maxDelay,
        TimeProvider timeProvider,
        Action<FileSystemChanges> onFlush)
    {
        _quietPeriod = quietPeriod;
        _maxDelay = maxDelay < quietPeriod ? quietPeriod : maxDelay;
        _timeProvider = timeProvider;
        _onFlush = onFlush;
        _timer = timeProvider.CreateTimer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Paths are compared the way the platform's file system usually does.
    /// </summary>
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public void AddPath(string path)
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                _paths.Add(path);
                Schedule();
            }
        }
    }

    public void AddRescan(Exception? error = null)
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                _rescanRequired = true;
                _error ??= error;
                Schedule();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _timer.Dispose();
        }
    }

    /// <summary>
    /// Restarts the quiet period, without going past the oldest change's maximum delay. Call with the lock held.
    /// </summary>
    private void Schedule()
    {
        var now = _timeProvider.GetTimestamp();
        _batchStarted ??= now;
        var untilMaxDelay = _maxDelay - _timeProvider.GetElapsedTime(_batchStarted.Value, now);
        var dueTime = untilMaxDelay < _quietPeriod ? untilMaxDelay : _quietPeriod;
        _timer.Change(dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime, Timeout.InfiniteTimeSpan);
    }

    private void Flush()
    {
        // The flush lock keeps batches in order and stops them overlapping if the listener is slow.
        lock (_flushLock)
        {
            FileSystemChanges changes;
            lock (_lock)
            {
                if (_disposed || _batchStarted is null)
                {
                    return;
                }

                changes = new FileSystemChanges(_paths, _rescanRequired, _error);
                _paths = new HashSet<string>(PathComparer);
                _rescanRequired = false;
                _error = null;
                _batchStarted = null;
            }

            _onFlush(changes);
        }
    }
}
