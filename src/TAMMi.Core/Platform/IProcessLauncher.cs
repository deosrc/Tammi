namespace TAMMi.Core.Platform;

/// <summary>
/// Starts external processes: command-line tools such as <c>git</c>, and desktop tools for "Open in…".
/// </summary>
/// <remarks>
/// Agents and terminals don't use this service, because they need a PTY.
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
    /// The process gets no input: its standard input is closed straight away. Cancelling kills the process and its
    /// children, and throws <see cref="OperationCanceledException"/>.
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
    /// Opens a file, folder or URL with the system's default application, e.g. a folder in the file manager.
    /// </summary>
    void Open(string target);
}

/// <summary>
/// A process to run.
/// </summary>
/// <param name="FileName">
/// The executable's name or path. Names are found with <see cref="IProcessLauncher.FindExecutable"/>.
/// </param>
/// <param name="Arguments">The arguments, each passed to the process as-is.</param>
public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>
    /// Creates a request from an executable and its arguments.
    /// </summary>
    public ProcessRequest(string fileName, params string[] arguments)
        : this(fileName, (IReadOnlyList<string>)arguments)
    {
    }

    /// <summary>
    /// The working directory, or <c>null</c> to use the app's current directory.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Environment variables to set for the process. A <c>null</c> value removes the variable.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();
}

/// <summary>
/// The outcome of a process run with <see cref="IProcessLauncher.RunAsync"/>.
/// </summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Whether the process exited with code 0.
    /// </summary>
    public bool Succeeded => ExitCode == 0;
}
