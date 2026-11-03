using System.Collections;

namespace TAMMi.Core.Platform.Pty;

/// <summary>
/// The parts of <see cref="IPtyService"/> that work the same on every platform: checking the request and building
/// the process's environment. Each platform starts the process in its own kind of pseudoterminal.
/// </summary>
public abstract class PtyServiceBase(IProcessLauncher processLauncher) : IPtyService
{
    /// <summary>
    /// The launcher used to find executables.
    /// </summary>
    protected IProcessLauncher ProcessLauncher { get; } = processLauncher;

    /// <inheritdoc />
    public abstract ProcessRequest GetDefaultShell();

    /// <inheritdoc />
    public IPtySession Start(ProcessRequest request, PtySize size)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileName = ProcessLauncher.FindExecutable(request.FileName)
            ?? throw new FileNotFoundException($"Couldn't find the executable '{request.FileName}'.", request.FileName);

        var workingDirectory = string.IsNullOrEmpty(request.WorkingDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Path.GetFullPath(request.WorkingDirectory);
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Couldn't find the folder '{workingDirectory}'.");
        }

        var environment = new Dictionary<string, string>(EnvironmentComparer);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            environment[(string)variable.Key] = (string?)variable.Value ?? string.Empty;
        }

        ConfigureEnvironment(environment);
        foreach (var (name, value) in request.Environment)
        {
            if (value is null)
            {
                environment.Remove(name);
            }
            else
            {
                environment[name] = value;
            }
        }

        return StartCore(new PtyStartInfo(fileName, request.Arguments, workingDirectory, environment, size));
    }

    /// <summary>
    /// How environment variable names are compared on this platform.
    /// </summary>
    protected virtual StringComparer EnvironmentComparer => StringComparer.Ordinal;

    /// <summary>
    /// Sets the platform's terminal variables (e.g. <c>TERM</c>) in the process's environment. Variables in the
    /// request are applied afterwards, so they win.
    /// </summary>
    /// <param name="environment">The environment, starting as a copy of the app's own.</param>
    protected virtual void ConfigureEnvironment(IDictionary<string, string> environment)
    {
    }

    /// <summary>
    /// Starts the process in a new pseudoterminal.
    /// </summary>
    protected abstract IPtySession StartCore(PtyStartInfo startInfo);
}

/// <summary>
/// A checked request to start a process in a pseudoterminal.
/// </summary>
/// <param name="FileName">The executable's full path.</param>
/// <param name="Arguments">The arguments, each passed to the process as-is.</param>
/// <param name="WorkingDirectory">The working directory's full path. It exists.</param>
/// <param name="Environment">The process's complete environment.</param>
/// <param name="Size">The terminal's starting size.</param>
public sealed record PtyStartInfo(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    PtySize Size);
