namespace TAMMi.Core.Git;

/// <summary>
/// A <c>git</c> command exited with a non-zero exit code.
/// </summary>
public sealed class GitCommandException(GitCommandResult result)
    : GitException(BuildMessage(result))
{
    /// <summary>
    /// The full result of the failed command, including its output.
    /// </summary>
    public GitCommandResult Result { get; } = result;

    private static string BuildMessage(GitCommandResult result)
    {
        string error = result.StandardError.Trim();
        if (error.Length == 0)
        {
            error = result.StandardOutput.Trim();
        }

        string message = $"'git {string.Join(' ', result.Arguments)}' failed with exit code {result.ExitCode}.";
        return error.Length == 0 ? message : $"{message} {error}";
    }
}
