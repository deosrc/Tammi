using System.Text;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// Builds a Windows command line from separate arguments, quoting them so that programs that parse their command
/// line the usual way (<c>CommandLineToArgvW</c> and the C runtime) get each argument back as it was.
/// </summary>
/// <remarks>
/// <c>cmd.exe</c> and batch files parse their command line differently, so special characters in their arguments
/// (e.g. <c>&amp;</c>) aren't protected.
/// </remarks>
internal static class WindowsCommandLine
{
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
}
