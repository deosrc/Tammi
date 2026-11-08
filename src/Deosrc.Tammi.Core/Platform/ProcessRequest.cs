namespace Deosrc.Tammi.Core.Platform;

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

    /// <summary>
    /// Whether a console process started with <see cref="IProcessLauncher.Start"/> gets no console window, e.g. a
    /// batch file that starts a desktop tool. Windows that the process opens itself are still shown.
    /// </summary>
    public bool HideWindow { get; init; }
}
