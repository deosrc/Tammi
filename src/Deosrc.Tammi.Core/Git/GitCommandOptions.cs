namespace Deosrc.Tammi.Core.Git;

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

    /// <summary>
    /// Called with each line git writes to standard error as it's written, e.g. the progress of a fetch. Git
    /// rewrites a progress line with carriage returns, so each rewrite is reported as its own line. It's called on a
    /// background thread. The lines are still part of <see cref="GitCommandResult.StandardError"/>.
    /// </summary>
    public Action<string>? OnErrorLine { get; init; }
}
