namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// Shows a command and its arguments as one line of text, so the Settings screen can show what will be run.
/// </summary>
/// <remarks>
/// It's only for reading: Tammi passes each argument to the process as it is, without going through a shell. Arguments
/// that are empty or have spaces or quotes are shown in double quotes, with their quotes escaped.
/// </remarks>
public static class CommandLinePreview
{
    /// <summary>
    /// Shows a command and its arguments on one line.
    /// </summary>
    public static string Format(string command, IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);

        return string.Join(' ', new[] { command }.Concat(arguments).Select(Quote));
    }

    private static string Quote(string argument) =>
        argument.Length == 0 || argument.Any(c => char.IsWhiteSpace(c) || c == '"')
            ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument;
}
