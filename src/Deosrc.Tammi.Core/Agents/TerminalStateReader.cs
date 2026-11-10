using System.Text;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// Reads the agent state that some CLIs (e.g. opencode) report in their terminal output, as the escape sequence
/// <c>ESC ] 7501 ; state=&lt;state&gt; : id=&lt;session id&gt; : … BEL</c> (ended by <c>BEL</c> or <c>ESC \</c>).
/// </summary>
/// <remarks>
/// Not a general terminal parser: it only looks for that sequence and ignores everything else, including sequences it
/// doesn't understand and ones that are too long. Output can be split anywhere, so one reader follows one agent's
/// output from the start. Not thread-safe.
/// </remarks>
internal sealed class TerminalStateReader
{
    private const byte Escape = 0x1B;
    private const byte Bell = 0x07;
    private const string Prefix = "7501;";

    // Longer than anything the CLIs send (a base64 title and an ID), so a stray ESC ] can't make Tammi keep buffering.
    private const int MaxLength = 1024;

    private readonly List<byte> _body = [];
    private Mode _mode = Mode.Text;

    private enum Mode
    {
        Text,
        AfterEscape,
        InOsc,
        InOscAfterEscape,
    }

    /// <summary>
    /// A report: the state the CLI named, and the Agent Session it said the state is for.
    /// </summary>
    public readonly record struct Report(string State, string? SessionId);

    /// <summary>
    /// Reads more output.
    /// </summary>
    /// <returns>The reports it contained, in order. They're usually none.</returns>
    public IReadOnlyList<Report> Read(ReadOnlySpan<byte> output)
    {
        List<Report>? reports = null;
        foreach (var value in output)
        {
            switch (_mode)
            {
                case Mode.Text:
                    if (value == Escape)
                    {
                        _mode = Mode.AfterEscape;
                    }

                    break;
                case Mode.AfterEscape:
                    if (value == (byte)']')
                    {
                        _body.Clear();
                        _mode = Mode.InOsc;
                    }
                    else
                    {
                        _mode = value == Escape ? Mode.AfterEscape : Mode.Text;
                    }

                    break;
                case Mode.InOsc:
                    if (value == Bell)
                    {
                        Finish(ref reports);
                    }
                    else if (value == Escape)
                    {
                        _mode = Mode.InOscAfterEscape;
                    }
                    else if (_body.Count >= MaxLength)
                    {
                        _mode = Mode.Text;
                    }
                    else
                    {
                        _body.Add(value);
                    }

                    break;
                case Mode.InOscAfterEscape:
                    if (value == (byte)'\\')
                    {
                        Finish(ref reports);
                    }
                    else
                    {
                        // Anything else abandons the sequence. An ESC might start the next one.
                        _mode = value == Escape ? Mode.AfterEscape : Mode.Text;
                    }

                    break;
            }
        }

        return reports ?? (IReadOnlyList<Report>)[];
    }

    private void Finish(ref List<Report>? reports)
    {
        _mode = Mode.Text;
        if (Parse(Encoding.UTF8.GetString(_body.ToArray())) is { } report)
        {
            (reports ??= []).Add(report);
        }
    }

    private static Report? Parse(string body)
    {
        if (!body.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string? state = null;
        string? sessionId = null;
        foreach (var field in body[Prefix.Length..].Split(':'))
        {
            var separator = field.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            switch (field[..separator])
            {
                case "state":
                    state = field[(separator + 1)..];
                    break;
                case "id":
                    sessionId = field[(separator + 1)..];
                    break;
            }
        }

        return string.IsNullOrEmpty(state) ? null : new Report(state, sessionId);
    }
}
