namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// A process running in a pseudoterminal.
/// </summary>
/// <remarks>
/// Output is raw terminal bytes (UTF-8 text and escape sequences), ready for a terminal emulator such as xterm.js.
/// Disposing the session closes the pseudoterminal, which hangs up the process, and kills it if it doesn't exit.
/// </remarks>
public interface IPtySession : IAsyncDisposable
{
    /// <summary>
    /// The ID of the process that was started.
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// The terminal's current size.
    /// </summary>
    PtySize Size { get; }

    /// <summary>
    /// Completes with the exit code when the process exits. On Linux, a process ended by a signal gets
    /// 128 + the signal number, as in a shell.
    /// </summary>
    Task<int> Exited { get; }

    /// <summary>
    /// Reads the terminal's output. Output is buffered until it's read, so none is lost before the first read.
    /// </summary>
    /// <remarks>
    /// Only one read may be in progress at a time. Output ends once the process has exited and its output has been
    /// read.
    /// </remarks>
    /// <returns>The number of bytes read, or 0 when the output has ended.</returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends input to the terminal, e.g. typed keys encoded as UTF-8. Input is written in order on a background
    /// thread, so this doesn't block. Input sent after the session ends is dropped.
    /// </summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>
    /// Resizes the terminal. The process is told about the new size (e.g. with <c>SIGWINCH</c> on Linux).
    /// </summary>
    void Resize(PtySize size);
}
