using System.Text;
using System.Threading.Channels;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Tests.Terminals;

/// <summary>
/// A session with no process behind it, whose output the test writes.
/// </summary>
internal sealed class ScriptedPtySession : IPtySession
{
    private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>();
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ReadOnlyMemory<byte> _pending;

    public int ProcessId => 1234;

    public PtySize Size { get; private set; } = PtySize.Default;

    public Task<int> Exited => _exited.Task;

    /// <summary>
    /// Sends output, which is read in one piece.
    /// </summary>
    public void Output(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// Ends the output, and the process exits.
    /// </summary>
    public void Exit(int exitCode)
    {
        _output.Writer.TryComplete();
        _exited.TrySetResult(exitCode);
    }

    /// <summary>
    /// Ends the output with an error.
    /// </summary>
    public void Fail() => _output.Writer.TryComplete(new IOException("The pseudoterminal broke."));

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_pending.IsEmpty)
        {
            if (!await _output.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            _pending = await _output.Reader.ReadAsync(cancellationToken);
        }

        var count = Math.Min(buffer.Length, _pending.Length);
        _pending[..count].CopyTo(buffer);
        _pending = _pending[count..];
        return count;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
    }

    public void Resize(PtySize size) => Size = size;

    public ValueTask DisposeAsync()
    {
        Exit(129);
        return ValueTask.CompletedTask;
    }
}
