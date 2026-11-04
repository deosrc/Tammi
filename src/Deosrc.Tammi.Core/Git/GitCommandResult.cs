namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// The outcome of running a <c>git</c> command.
/// </summary>
/// <param name="Arguments">The arguments passed to git.</param>
/// <param name="WorkingDirectory">The directory git ran in.</param>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything git wrote to standard output.</param>
/// <param name="StandardError">Everything git wrote to standard error.</param>
public sealed record GitCommandResult(
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    /// <summary>
    /// Whether git exited with code 0.
    /// </summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// Standard output split into lines, without the trailing empty line. Use for line-based porcelain formats.
    /// </summary>
    public IReadOnlyList<string> OutputLines =>
        StandardOutput.Length == 0
            ? []
            : StandardOutput.TrimEnd('\n').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

    /// <summary>
    /// Standard output split on NUL characters, without the trailing empty field. Use with <c>-z</c> output, which
    /// doesn't quote or escape paths.
    /// </summary>
    public IReadOnlyList<string> OutputFields =>
        StandardOutput.Length == 0 ? [] : StandardOutput.TrimEnd('\0').Split('\0');
}
