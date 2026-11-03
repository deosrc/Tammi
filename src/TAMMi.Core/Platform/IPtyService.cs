namespace TAMMi.Core.Platform;

/// <summary>
/// Starts processes in a pseudoterminal (PTY), for the embedded terminal that terminals and agents run in.
/// </summary>
/// <remarks>
/// Windows uses ConPTY. Linux uses a <c>/dev/ptmx</c> pseudoterminal (see <c>docs/CONTEXT.md</c>).
/// </remarks>
public interface IPtyService
{
    /// <summary>
    /// The platform's default shell: PowerShell on Windows (<c>pwsh</c> if installed) and <c>$SHELL</c> on Linux.
    /// </summary>
    /// <returns>A request to start the shell. Set its working directory before starting it.</returns>
    ProcessRequest GetDefaultShell();

    /// <summary>
    /// Starts a process in a new pseudoterminal.
    /// </summary>
    /// <param name="request">
    /// The process to start. Names are found with <see cref="IProcessLauncher.FindExecutable"/>. Without a working
    /// directory, the process starts in the user's home folder.
    /// </param>
    /// <param name="size">The terminal's starting size.</param>
    /// <returns>The session. Dispose it to end the process.</returns>
    /// <exception cref="FileNotFoundException">The executable can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory doesn't exist.</exception>
    IPtySession Start(ProcessRequest request, PtySize size);
}

/// <summary>
/// A terminal's size, in character cells.
/// </summary>
public readonly record struct PtySize
{
    /// <summary>
    /// Creates a size. Values below 1 are raised to 1.
    /// </summary>
    public PtySize(int columns, int rows)
    {
        Columns = Math.Clamp(columns, 1, short.MaxValue);
        Rows = Math.Clamp(rows, 1, short.MaxValue);
    }

    /// <summary>
    /// The classic 80 × 24 terminal.
    /// </summary>
    public static PtySize Default { get; } = new(80, 24);

    /// <summary>
    /// The number of columns.
    /// </summary>
    public int Columns { get; }

    /// <summary>
    /// The number of rows.
    /// </summary>
    public int Rows { get; }
}

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
