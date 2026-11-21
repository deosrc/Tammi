using System.Text;

namespace Deosrc.Tammi.Core.Terminals;

/// <summary>
/// Finds the title that a program sets for its terminal in its output: the escape sequences <c>OSC 0 ; title</c> (icon
/// name and title) and <c>OSC 2 ; title</c> (title), ended by BEL or <c>ESC \</c>.
/// </summary>
/// <remarks>
/// Output arrives in chunks that can split a sequence, so this keeps its place between calls. It only reads the
/// output; it doesn't change it. It isn't thread safe.
/// </remarks>
public sealed class TerminalTitleParser
{
    /// <summary>
    /// The longest title kept, in bytes. Longer sequences are ignored.
    /// </summary>
    public const int MaxTitleBytes = 512;

    private const byte Escape = 0x1b;
    private const byte Bell = 0x07;
    private const byte Cancel = 0x18;
    private const byte Substitute = 0x1a;

    private enum State
    {
        Text,
        Escape,
        OscPayload,
        OscEscape,
    }

    private readonly List<byte> _payload = [];
    private State _state;
    private bool _overflowed;

    /// <summary>
    /// Reads output.
    /// </summary>
    /// <param name="output">The next output.</param>
    /// <returns>
    /// The titles that were set in it, in order. A title is empty when the program cleared it.
    /// </returns>
    public IReadOnlyList<string> Parse(ReadOnlySpan<byte> output)
    {
        List<string>? titles = null;
        foreach (var value in output)
        {
            switch (_state)
            {
                case State.Text:
                    if (value == Escape)
                    {
                        _state = State.Escape;
                    }

                    break;

                case State.Escape:
                    if (value == (byte)']')
                    {
                        _payload.Clear();
                        _overflowed = false;
                        _state = State.OscPayload;
                    }
                    else
                    {
                        _state = value == Escape ? State.Escape : State.Text;
                    }

                    break;

                case State.OscPayload:
                    if (value == Bell)
                    {
                        Finish(ref titles);
                    }
                    else if (value == Escape)
                    {
                        _state = State.OscEscape;
                    }
                    else if (value is Cancel or Substitute)
                    {
                        _state = State.Text;
                    }
                    else if (_payload.Count >= MaxTitleBytes + 8)
                    {
                        _overflowed = true;
                    }
                    else
                    {
                        _payload.Add(value);
                    }

                    break;

                case State.OscEscape:
                    if (value == (byte)'\\')
                    {
                        Finish(ref titles);
                    }
                    else
                    {
                        // Not a terminator, so the sequence was abandoned. The byte may start another one.
                        _state = value == Escape ? State.Escape : State.Text;
                    }

                    break;
            }
        }

        return titles ?? (IReadOnlyList<string>)[];
    }

    private void Finish(ref List<string>? titles)
    {
        _state = State.Text;
        if (_overflowed)
        {
            return;
        }

        var separator = _payload.IndexOf((byte)';');
        if (separator < 0)
        {
            return;
        }

        var command = Encoding.ASCII.GetString([.. _payload.Take(separator)]);
        if (command is not ("0" or "2"))
        {
            return;
        }

        var title = Sanitise(Encoding.UTF8.GetString([.. _payload.Skip(separator + 1)]));
        (titles ??= []).Add(title);
    }

    private static string Sanitise(string title)
    {
        var builder = new StringBuilder(title.Length);
        foreach (var character in title)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }
}
