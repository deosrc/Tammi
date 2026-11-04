namespace TAMMi.Core.Git;

/// <summary>
/// Settings for a single git command.
/// </summary>
public sealed record GitCommandOptions
{
    /// <summary>
    /// Throw a <see cref="GitCommandException"/> when git exits with a non-zero code. Defaults to
    /// <see langword="true"/>. Turn it off for commands that use exit codes to report results.
    /// </summary>
    public bool ThrowOnError { get; init; } = true;

    /// <summary>
    /// Text written to git's standard input. When <see langword="null"/>, standard input is closed straight away.
    /// </summary>
    public string? StandardInput { get; init; }
}
