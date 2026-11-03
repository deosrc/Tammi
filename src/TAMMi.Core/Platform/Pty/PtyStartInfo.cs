namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// A checked request to start a process in a pseudoterminal.
/// </summary>
/// <param name="FileName">The executable's full path.</param>
/// <param name="Arguments">The arguments, each passed to the process as-is.</param>
/// <param name="WorkingDirectory">The working directory's full path. It exists.</param>
/// <param name="Environment">The process's complete environment.</param>
/// <param name="Size">The terminal's starting size.</param>
public sealed record PtyStartInfo(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    PtySize Size);
