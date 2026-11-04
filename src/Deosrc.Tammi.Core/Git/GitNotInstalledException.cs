namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// The <c>git</c> executable couldn't be found or started. Tammi needs a git install for all write operations.
/// </summary>
public sealed class GitNotInstalledException(string executablePath, Exception? innerException = null)
    : GitException(
        $"Git isn't installed, or '{executablePath}' couldn't be found on the PATH. "
            + "Install git from https://git-scm.com/downloads and restart Tammi.",
        innerException)
{
    /// <summary>
    /// The executable Tammi tried to run.
    /// </summary>
    public string ExecutablePath { get; } = executablePath;
}
