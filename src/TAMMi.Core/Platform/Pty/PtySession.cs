using System.Threading.Channels;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// The parts of <see cref="IPtySession"/> that work the same on every platform: buffering output and input, and
/// shutting the session down. Each platform reads, writes and closes its own pseudoterminal.
/// </summary>
/// <remarks>
/// Output is read by a dedicated thread, because reading a pseudoterminal blocks. It's queued for
/// <see cref="ReadAsync"/>, and the queue has a limit: when it's full the thread stops reading, so a process that
/// writes faster than the terminal can show its output is slowed down instead of filling memory. Input is queued and
/// written by a background task, so a process that isn't reading its input can't block the caller.
/// </remarks>
internal abstract class PtySession : IPtySession
{
    private const int ReadBufferSize = 16 * 1024;

    // With full 16 KiB chunks, up to 1 MiB of output waits to be read.
    private const int OutputQueueLength = 64;

    /// <summary>
    /// How long a hung-up process has to exit before it's killed.
    /// </summary>
    protected static readonly TimeSpan HangUpTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long a killed process has to exit before it's given up on.
    /// </summary>
    protected static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(2);

    private readonly Channel<byte[]> _output = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(OutputQueueLength)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _outputEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _disposeLock = new();

    private ReadOnlyMemory<byte> _pending;
    private Task _inputWritten = Task.CompletedTask;
    private Task? _disposed;

    protected PtySession(int processId, PtySize size)
    {
        ProcessId = processId;
        Size = size;
    }

    /// <inheritdoc />
    public int ProcessId { get; }

    /// <inheritdoc />
    public PtySize Size { get; private set; }

    /// <inheritdoc />
    public Task<int> Exited => _exited.Task;

    /// <summary>
    /// Whether the session is being disposed. Blocking reads and writes should check it regularly and stop.
    /// </summary>
    protected bool IsClosing => _closing.IsCancellationRequested;

    /// <summary>
    /// Completes when the output thread has stopped reading.
    /// </summary>
    protected Task OutputEnded => _outputEnded.Task;

    /// <summary>
    /// Completes when the input task has stopped writing.
    /// </summary>
    protected Task InputWritten => _inputWritten;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        if (_pending.IsEmpty)
        {
            byte[]? chunk;
            while (!_output.Reader.TryRead(out chunk))
            {
                if (!await _output.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }
            }

            _pending = chunk;
        }

        // Fill the buffer from as many queued chunks as are ready, so the caller gets fewer, larger reads.
        var total = 0;
        while (true)
        {
            var count = Math.Min(_pending.Length, buffer.Length - total);
            _pending.Span[..count].CopyTo(buffer.Span[total..]);
            _pending = _pending[count..];
            total += count;

            if (total == buffer.Length || !_pending.IsEmpty || !_output.Reader.TryRead(out var next))
            {
                return total;
            }

            _pending = next;
        }
    }

    /// <inheritdoc />
    public void Write(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
        {
            _input.Writer.TryWrite(data.ToArray());
        }
    }

    /// <inheritdoc />
    public void Resize(PtySize size)
    {
        if (size == Size || IsClosing)
        {
            return;
        }

        Size = size;
        ResizeCore(size);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            _disposed ??= DisposeCoreAsync();
            return new ValueTask(_disposed);
        }
    }

    /// <summary>
    /// Starts reading output and writing input. Call it once the pseudoterminal is ready.
    /// </summary>
    protected void StartPumps()
    {
        var outputThread = new Thread(ReadOutputLoop)
        {
            IsBackground = true,
            Name = $"PTY output ({ProcessId})",
        };
        outputThread.Start();
        _inputWritten = Task.Run(WriteInputLoopAsync);
    }

    /// <summary>
    /// Reports that the process has exited.
    /// </summary>
    protected void OnExited(int exitCode) => _exited.TrySetResult(exitCode);

    /// <summary>
    /// Reads output, blocking until some is available.
    /// </summary>
    /// <returns>The number of bytes read, or 0 when the output has ended or the session is closing.</returns>
    protected abstract int ReadOutput(Span<byte> buffer);

    /// <summary>
    /// Writes all of the input, blocking until it's written. It should give up once the session is closing.
    /// </summary>
    protected abstract void WriteInput(ReadOnlySpan<byte> data);

    /// <summary>
    /// Tells the pseudoterminal its new size.
    /// </summary>
    protected abstract void ResizeCore(PtySize size);

    /// <summary>
    /// Closes the pseudoterminal, which hangs up the process. Called once, when the session is disposed, after
    /// <see cref="IsClosing"/> becomes <c>true</c>. Wait for <see cref="OutputEnded"/> and <see cref="InputWritten"/>
    /// before releasing anything they use.
    /// </summary>
    protected abstract Task ClosePseudoterminalAsync();

    /// <summary>
    /// Kills the process, if it didn't exit after being hung up.
    /// </summary>
    protected abstract void Kill();

    /// <summary>
    /// Releases what's left once the process has exited, or been given up on.
    /// </summary>
    protected virtual void ReleaseProcess()
    {
    }

    private void ReadOutputLoop()
    {
        var buffer = new byte[ReadBufferSize];
        try
        {
            int count;
            while ((count = ReadOutput(buffer)) > 0)
            {
                // Output read while closing is dropped, but keep reading until it ends: on Windows, closing the
                // pseudoconsole can wait until its output has been read.
                if (!IsClosing)
                {
                    Enqueue(buffer.AsSpan(0, count).ToArray());
                }
            }
        }
        catch (Exception)
        {
            // An error ends the output, just as the end of the stream does. The thread mustn't throw, because an
            // exception on it would end the app.
        }
        finally
        {
            _output.Writer.TryComplete();
            _outputEnded.TrySetResult();
        }
    }

    private void Enqueue(byte[] chunk)
    {
        while (!_output.Writer.TryWrite(chunk))
        {
            try
            {
                // Wait for the reader to catch up. This thread does nothing else, so blocking it is fine.
                if (!_output.Writer.WaitToWriteAsync(_closing.Token).AsTask().GetAwaiter().GetResult())
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task WriteInputLoopAsync()
    {
        var failed = false;
        await foreach (var chunk in _input.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (failed || IsClosing)
            {
                continue;
            }

            try
            {
                WriteInput(chunk);
            }
            catch (Exception)
            {
                // The pseudoterminal has closed. Later input is dropped.
                failed = true;
            }
        }
    }

    private async Task DisposeCoreAsync()
    {
        _input.Writer.TryComplete();
        await _closing.CancelAsync().ConfigureAwait(false);

        try
        {
            await ClosePseudoterminalAsync().ConfigureAwait(false);

            if (!await WaitAsync(Exited, HangUpTimeout).ConfigureAwait(false))
            {
                Kill();
                await WaitAsync(Exited, KillTimeout).ConfigureAwait(false);
            }
        }
        finally
        {
            ReleaseProcess();
        }
    }

    /// <summary>
    /// Waits for a task, but no longer than the timeout.
    /// </summary>
    /// <returns><c>true</c> if the task completed in time.</returns>
    protected static async Task<bool> WaitAsync(Task task, TimeSpan timeout)
    {
        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
