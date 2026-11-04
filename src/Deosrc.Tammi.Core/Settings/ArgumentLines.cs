namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// Shows a list of arguments as text with one argument on each line, as the Settings screen edits them.
/// </summary>
/// <remarks>
/// One argument per line needs no quoting: each line is passed as it is, spaces and quotes included.
/// </remarks>
public static class ArgumentLines
{
    /// <summary>
    /// Puts each argument on its own line.
    /// </summary>
    public static string Format(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Join('\n', arguments);
    }

    /// <summary>
    /// Reads one argument from each line. Blank lines are left out, and nothing else is changed.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? text) =>
        (text ?? string.Empty)
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
}
