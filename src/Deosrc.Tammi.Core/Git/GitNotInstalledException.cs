namespace TAMMi.Core.Git;

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
