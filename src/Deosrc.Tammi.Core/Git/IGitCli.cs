namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Runs the <c>git</c> CLI. Tammi uses the CLI for writes (fetch, pull, push, stage, commit) and for worktree and
/// submodule operations, so it respects the user's git config, credential helpers and hooks.
/// </summary>
public interface IGitCli
{
    /// <summary>
    /// Finds the git install.
    /// </summary>
    /// <returns>The install, or <see langword="null"/> if git can't be found.</returns>
    Task<GitInstallation?> DetectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs git with the given arguments. Prefer porcelain or <c>-z</c> output formats when parsing the result.
    /// </summary>
    /// <param name="workingDirectory">The directory to run git in.</param>
    /// <param name="arguments">The arguments, passed to git as-is without shell quoting.</param>
    /// <param name="options">Optional settings for this command.</param>
    /// <param name="cancellationToken">Cancelling kills the git process.</param>
    /// <exception cref="GitNotInstalledException">Git can't be found.</exception>
    /// <exception cref="GitCommandException">
    /// Git exited with a non-zero code, and <see cref="GitCommandOptions.ThrowOnError"/> is set.
    /// </exception>
    Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        GitCommandOptions? options = null,
        CancellationToken cancellationToken = default);
}
