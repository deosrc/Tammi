namespace TAMMi.Core.Git;

/// <summary>
/// Base class for errors raised by TAMMi's git access.
/// </summary>
public class GitException : Exception
{
    public GitException(string message)
        : base(message)
    {
    }

    public GitException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The <c>git</c> executable couldn't be found or started. TAMMi needs a git install for all write operations.
/// </summary>
public sealed class GitNotInstalledException(string executablePath, Exception? innerException = null)
    : GitException(
        $"Git isn't installed, or '{executablePath}' couldn't be found on the PATH. "
            + "Install git from https://git-scm.com/downloads and restart TAMMi.",
        innerException)
{
    /// <summary>
    /// The executable TAMMi tried to run.
    /// </summary>
    public string ExecutablePath { get; } = executablePath;
}

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

/// <summary>
/// The given path isn't inside a git repository.
/// </summary>
public sealed class GitRepositoryNotFoundException(string path, Exception? innerException = null)
    : GitException($"'{path}' isn't a git repository.", innerException)
{
    /// <summary>
    /// The path that was opened.
    /// </summary>
    public string Path { get; } = path;
}
