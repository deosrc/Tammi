namespace Deosrc.Tammi.Core.Platform;

/// <summary>
/// Starts external processes: command-line tools such as <c>git</c>, and desktop tools from the Workspace View's Tools
/// section.
/// </summary>
/// <remarks>
/// Agents and terminals don't use this service, because they need a PTY. They use <see cref="IPtyService"/>.
/// </remarks>
public interface IProcessLauncher
{
    /// <summary>
    /// Finds an executable by name, the way the platform's shell would.
    /// </summary>
    /// <param name="name">An executable name (e.g. <c>code</c>) or a path to one.</param>
    /// <returns>The executable's full path, or <c>null</c> if it can't be found.</returns>
    string? FindExecutable(string name);

    /// <summary>
    /// Runs a process to completion and captures its output.
    /// </summary>
    /// <remarks>
    /// The process gets no window and no input: its standard input is closed straight away. Cancelling kills the
    /// process and its children, and throws <see cref="OperationCanceledException"/>.
    /// </remarks>
    /// <exception cref="FileNotFoundException">The executable can't be found.</exception>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a process without waiting for it, e.g. to open a folder in an IDE.
    /// </summary>
    /// <returns>The ID of the started process.</returns>
    /// <exception cref="FileNotFoundException">The executable can't be found.</exception>
    int Start(ProcessRequest request);

    /// <summary>
    /// Starts a desktop tool without waiting for it, so that it keeps running after Tammi exits, e.g. to open a folder
    /// in an IDE. It doesn't share Tammi's terminal or output, if Tammi has them.
    /// </summary>
    /// <exception cref="FileNotFoundException">The executable can't be found.</exception>
    void StartDetached(ProcessRequest request);

    /// <summary>
    /// Opens a file, folder or URL with the system's default application, e.g. a folder in the file manager.
    /// </summary>
    void Open(string target);
}
