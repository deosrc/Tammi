using System.Threading.Channels;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests.Agents;

/// <summary>
/// A session with no process behind it. The test says when the "process" exits.
/// </summary>
internal sealed class FakePtySession : IPtySession
{
    private readonly System.Text.StringBuilder _input = new();
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();

    /// <summary>
    /// The exit code the process gets when the session is disposed while it's running, as a hang-up gives on Linux.
    /// <c>null</c> makes the process ignore being disposed.
    /// </summary>
    public int? ExitCodeWhenDisposed { get; init; } = 129;

    public bool Disposed { get; private set; }

    public int ProcessId => 1234;

    public PtySize Size { get; private set; } = PtySize.Default;

    public Task<int> Exited => _exited.Task;

    public void Exit(int exitCode)
    {
        _output.Writer.TryComplete();
        _exited.TrySetResult(exitCode);
    }

    public void Crash()
    {
        _output.Writer.TryComplete();
        _exited.TrySetException(new InvalidOperationException("The process couldn't be followed."));
    }

    /// <summary>
    /// Has the "process" write <paramref name="text"/> to the terminal.
    /// </summary>
    public void Output(string text) => _output.Writer.TryWrite(System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// Returns what <see cref="Output"/> was given, a chunk at a time, until the "process" exits.
    /// </summary>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!await _output.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)
            || !_output.Reader.TryRead(out var chunk))
        {
            return 0;
        }

        chunk.CopyTo(buffer);
        return chunk.Length;
    }

    /// <summary>
    /// Everything written to the terminal, as text.
    /// </summary>
    public string Input => _input.ToString();

    public void Write(ReadOnlySpan<byte> data) => _input.Append(System.Text.Encoding.UTF8.GetString(data));

    public void Resize(PtySize size) => Size = size;

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        if (ExitCodeWhenDisposed is { } exitCode)
        {
            Exit(exitCode);
        }

        return ValueTask.CompletedTask;
    }
}
