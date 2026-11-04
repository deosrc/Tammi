namespace TAMMi.Core.Terminals;

/// <summary>
/// Reads a <see cref="TerminalSession"/>'s output from where it started, e.g. for one terminal showing the session.
/// </summary>
/// <remarks>
/// Each reader has its own place in the output. A reader that falls so far behind that its output is dropped carries
/// on from the oldest output kept. Only one read may be in progress at a time.
/// </remarks>
public sealed class TerminalOutputReader
{
    private readonly TerminalSession _session;
    private long _position;

    internal TerminalOutputReader(TerminalSession session, long position)
    {
        _session = session;
        _position = position;
    }

    /// <summary>
    /// Reads output, waiting until there is some.
    /// </summary>
    /// <returns>The number of bytes read, or 0 once the output has ended and all of it has been read.</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var (count, position) = await _session.ReadAsync(_position, buffer, cancellationToken).ConfigureAwait(false);
        _position = position;
        return count;
    }
}
