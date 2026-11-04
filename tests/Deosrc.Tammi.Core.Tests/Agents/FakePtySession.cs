using Tammi.Core.Platform;

namespace Tammi.Core.Tests.Agents;

/// <summary>
/// A session with no process behind it. The test says when the "process" exits.
/// </summary>
internal sealed class FakePtySession : IPtySession
{
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The exit code the process gets when the session is disposed while it's running, as a hang-up gives on Linux.
    /// <c>null</c> makes the process ignore being disposed.
    /// </summary>
    public int? ExitCodeWhenDisposed { get; init; } = 129;

    public bool Disposed { get; private set; }

    public int ProcessId => 1234;

    public PtySize Size { get; private set; } = PtySize.Default;

    public Task<int> Exited => _exited.Task;

    public void Exit(int exitCode) => _exited.TrySetResult(exitCode);

    public void Crash() => _exited.TrySetException(new InvalidOperationException("The process couldn't be followed."));

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(0);

    public void Write(ReadOnlySpan<byte> data)
    {
    }

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
