using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Terminals;

/// <summary>
/// A pseudoterminal session whose recent output is kept, so a terminal that's closed and shown again can replay it.
/// </summary>
/// <remarks>
/// The session's output is read as soon as it arrives, whether or not a terminal is showing it, so the process never
/// waits for a terminal. Only the latest output is kept, up to a limit; older output is dropped. This doesn't own the
/// session: whoever started it ends it.
/// </remarks>
public sealed class TerminalSession
{
    /// <summary>
    /// How much output is kept by default: 1 MiB.
    /// </summary>
    public const int DefaultHistoryLimit = 1024 * 1024;

    private const int ReadBufferSize = 16 * 1024;

    private readonly Lock _lock = new();
    private readonly Queue<byte[]> _chunks = new();
    private readonly int _historyLimit;
    private readonly Action<ReadOnlySpan<byte>>? _observer;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Positions count every byte of output since the session started. _start is the first one still kept.
    private long _start;
    private long _end;
    private bool _ended;

    /// <summary>
    /// Starts reading a session's output. Nothing else may read it.
    /// </summary>
    /// <param name="pty">The session.</param>
    /// <param name="historyLimit">
    /// How many bytes of output to keep. A little more can be kept, because output is dropped a chunk at a time.
    /// </param>
    /// <param name="observer">
    /// Called with each chunk of output as it's read, in order and one at a time, on a background thread, or
    /// <c>null</c> for none. It must be quick and mustn't throw.
    /// </param>
    public TerminalSession(
        IPtySession pty,
        int historyLimit = DefaultHistoryLimit,
        Action<ReadOnlySpan<byte>>? observer = null)
    {
        ArgumentNullException.ThrowIfNull(pty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyLimit);

        Pty = pty;
        _historyLimit = historyLimit;
        _observer = observer;
        OutputEnded = ReadOutputAsync();
    }

    /// <summary>
    /// The pseudoterminal session, for its input, size and exit code.
    /// </summary>
    public IPtySession Pty { get; }

    /// <summary>
    /// Completes once the session's output has ended and all of it has been kept.
    /// </summary>
    public Task OutputEnded { get; }

    /// <summary>
    /// Opens a reader of the output, starting at the oldest output still kept.
    /// </summary>
    public TerminalOutputReader OpenReader()
    {
        lock (_lock)
        {
            return new TerminalOutputReader(this, _start);
        }
    }

    /// <summary>
    /// Copies output from a position, waiting until there is some.
    /// </summary>
    /// <returns>
    /// The number of bytes copied (0 once the output has ended), and the position after them. If the position's
    /// output has been dropped, copying starts at the oldest output kept.
    /// </returns>
    internal async ValueTask<(int Count, long Position)> ReadAsync(
        long position,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
        {
            return (0, position);
        }

        while (true)
        {
            Task changed;
            lock (_lock)
            {
                position = Math.Max(position, _start);
                if (position < _end)
                {
                    var count = CopyFrom(position, buffer.Span);
                    return (count, position + count);
                }

                if (_ended)
                {
                    return (0, position);
                }

                changed = _changed.Task;
            }

            await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private int CopyFrom(long position, Span<byte> buffer)
    {
        var chunkStart = _start;
        var total = 0;
        foreach (var chunk in _chunks)
        {
            var chunkEnd = chunkStart + chunk.Length;
            if (position < chunkEnd)
            {
                var offset = (int)(position + total - chunkStart);
                var count = Math.Min(chunk.Length - offset, buffer.Length - total);
                chunk.AsSpan(offset, count).CopyTo(buffer[total..]);
                total += count;
                if (total == buffer.Length)
                {
                    break;
                }
            }

            chunkStart = chunkEnd;
        }

        return total;
    }

    private async Task ReadOutputAsync()
    {
        // Let the constructor return before the first read.
        await Task.Yield();

        var buffer = new byte[ReadBufferSize];
        try
        {
            int count;
            while ((count = await Pty.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                Observe(buffer.AsSpan(0, count));
                Append(buffer.AsSpan(0, count).ToArray());
            }
        }
        catch (Exception)
        {
            // An error ends the output, as the end of the stream does.
        }
        finally
        {
            TaskCompletionSource changed;
            lock (_lock)
            {
                _ended = true;
                changed = _changed;
            }

            changed.TrySetResult();
        }
    }

    private void Observe(ReadOnlySpan<byte> output)
    {
        try
        {
            _observer?.Invoke(output);
        }
        catch (Exception)
        {
            // Watching the output must never stop it being kept.
        }
    }

    private void Append(byte[] chunk)
    {
        TaskCompletionSource changed;
        lock (_lock)
        {
            _chunks.Enqueue(chunk);
            _end += chunk.Length;

            // Always keep the newest chunk, even if it's over the limit on its own.
            while (_end - _start > _historyLimit && _chunks.Count > 1)
            {
                _start += _chunks.Dequeue().Length;
            }

            changed = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.TrySetResult();
    }
}
