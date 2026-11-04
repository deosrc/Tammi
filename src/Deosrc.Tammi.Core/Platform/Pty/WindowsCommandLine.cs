using System.Text;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// Builds a Windows command line from separate arguments, quoting them so that programs that parse their command
/// line the usual way (<c>CommandLineToArgvW</c> and the C runtime) get each argument back as it was.
/// </summary>
/// <remarks>
/// Batch files (<c>.cmd</c> and <c>.bat</c>, e.g. CLIs installed with npm) are run through <c>cmd.exe</c>, which
/// reads its own special characters (e.g. <c>&amp;</c>, <c>|</c> and <c>%</c>) in the command line.
/// <see cref="Create"/> escapes those too, the same way Rust's standard library does. Batch files can't be given
/// arguments with line breaks.
/// </remarks>
internal static class WindowsCommandLine
{
    // ASCII characters that cmd.exe leaves alone outside quotes. Any other ASCII symbol makes an argument quoted.
    private const string UnquotedBatchSymbols = @"#$*+-./:?@\_";

    /// <summary>
    /// The executable to start and the command line to start it with.
    /// </summary>
    /// <param name="fileName">The executable's full path. It may be a batch file.</param>
    /// <param name="arguments">The arguments, each to be passed to the process as-is.</param>
    /// <param name="systemDirectory">The Windows system folder, where <c>cmd.exe</c> is.</param>
    /// <exception cref="ArgumentException">
    /// The executable is a batch file and an argument has a line break, which <c>cmd.exe</c> can't pass on.
    /// </exception>
    public static (string ApplicationName, string CommandLine) Create(
        string fileName,
        IReadOnlyList<string> arguments,
        string systemDirectory)
    {
        if (!IsBatchFile(fileName))
        {
            return (fileName, Build(fileName, arguments));
        }

        // Use the system's cmd.exe, rather than one that %ComSpec% or the search path could swap in.
        return (Path.Combine(systemDirectory, "cmd.exe"), BuildForBatchFile(fileName, arguments));
    }

    public static string Build(string fileName, IEnumerable<string> arguments)
    {
        var commandLine = new StringBuilder();
        AppendArgument(commandLine, fileName);
        foreach (var argument in arguments)
        {
            commandLine.Append(' ');
            AppendArgument(commandLine, argument);
        }

        return commandLine.ToString();
    }

    /// <summary>
    /// Builds the <c>cmd.exe</c> command line that runs a batch file with arguments.
    /// </summary>
    public static string BuildForBatchFile(string fileName, IEnumerable<string> arguments)
    {
        if (fileName.Contains('"', StringComparison.Ordinal) || fileName.EndsWith('\\'))
        {
            throw new ArgumentException($"'{fileName}' isn't a valid batch file path.", nameof(fileName));
        }

        // /e:ON turns on the command extensions that the % escape needs, /v:OFF stops ! being read, and /d skips
        // AutoRun commands. /c reads the rest of the line as a command, removing the outer quotes added here.
        var commandLine = new StringBuilder("cmd.exe /e:ON /v:OFF /d /c \"");
        commandLine.Append('"').Append(fileName).Append('"');
        foreach (var argument in arguments)
        {
            if (argument.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                throw new ArgumentException(
                    $"The batch file '{Path.GetFileName(fileName)}' can't be given an argument with a line break.",
                    nameof(arguments));
            }

            commandLine.Append(' ');
            AppendBatchArgument(commandLine, argument);
        }

        commandLine.Append('"');
        return commandLine.ToString();
    }

    private static bool IsBatchFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendArgument(StringBuilder commandLine, string argument)
    {
        if (argument.Length > 0 && argument.AsSpan().IndexOfAny(" \t\n\v\"") < 0)
        {
            commandLine.Append(argument);
            return;
        }

        commandLine.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            // Backslashes are only special before a quote: each one is then escaped, and so is the quote.
            if (character == '"')
            {
                commandLine.Append('\\', (backslashes * 2) + 1);
            }
            else
            {
                commandLine.Append('\\', backslashes);
            }

            backslashes = 0;
            commandLine.Append(character);
        }

        // The closing quote follows, so trailing backslashes must be escaped too.
        commandLine.Append('\\', backslashes * 2);
        commandLine.Append('"');
    }

    private static void AppendBatchArgument(StringBuilder commandLine, string argument)
    {
        // Inside quotes, cmd.exe leaves its special characters alone, except %. A trailing backslash is quoted too,
        // so a batch file that quotes the argument itself (e.g. "%~1") doesn't get an escaped closing quote.
        var quote = argument.Length == 0
            || argument.EndsWith('\\')
            || argument.Any(character => char.IsAscii(character)
                ? !char.IsAsciiLetterOrDigit(character) && !UnquotedBatchSymbols.Contains(character)
                : char.IsControl(character));

        if (quote)
        {
            commandLine.Append('"');
        }

        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                commandLine.Append(character);
                continue;
            }

            if (character == '"')
            {
                // Double the backslashes before a quote, and double the quote, which both cmd.exe and the C runtime
                // read as a literal quote inside quotes.
                commandLine.Append('\\', backslashes);
                commandLine.Append('"');
            }
            else if (character == '%')
            {
                // %cd:~,% expands to nothing, but stops cmd.exe from reading the % as the start of a variable.
                commandLine.Append("%%cd:~,");
            }

            backslashes = 0;
            commandLine.Append(character);
        }

        if (quote)
        {
            commandLine.Append('\\', backslashes);
            commandLine.Append('"');
        }
    }
}
