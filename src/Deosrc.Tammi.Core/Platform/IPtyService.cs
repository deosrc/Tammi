namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Starts processes in a pseudoterminal (PTY), for the embedded terminal that terminals and agents run in.
/// </summary>
/// <remarks>
/// Windows uses ConPTY. Linux uses a <c>/dev/ptmx</c> pseudoterminal (see <c>docs/CONTEXT.md</c>).
/// </remarks>
public interface IPtyService
{
    /// <summary>
    /// The platform's default shell: PowerShell on Windows (<c>pwsh</c> if installed) and <c>$SHELL</c> on Linux.
    /// </summary>
    /// <returns>A request to start the shell. Set its working directory before starting it.</returns>
    ProcessRequest GetDefaultShell();

    /// <summary>
    /// Starts a process in a new pseudoterminal.
    /// </summary>
    /// <param name="request">
    /// The process to start. Names are found with <see cref="IProcessLauncher.FindExecutable"/>. Without a working
    /// directory, the process starts in the user's home folder.
    /// </param>
    /// <param name="size">The terminal's starting size.</param>
    /// <returns>The session. Dispose it to end the process.</returns>
    /// <exception cref="FileNotFoundException">The executable can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory doesn't exist.</exception>
    IPtySession Start(ProcessRequest request, PtySize size);
}
