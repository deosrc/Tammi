using System.ComponentModel;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Agents;

/// <summary>
/// Checks that a Supported Agent's CLI is installed and runs, by finding it and asking for its version.
/// </summary>
public sealed class SupportedAgentChecker
{
    /// <summary>
    /// How long the CLI has to report its version.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly IProcessLauncher _processLauncher;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Creates a checker that runs CLIs with <paramref name="processLauncher"/>.
    /// </summary>
    public SupportedAgentChecker(IProcessLauncher processLauncher)
        : this(processLauncher, DefaultTimeout)
    {
    }

    /// <summary>
    /// Creates a checker that gives CLIs <paramref name="timeout"/> to report their version.
    /// </summary>
    internal SupportedAgentChecker(IProcessLauncher processLauncher, TimeSpan timeout)
    {
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _timeout = timeout;
    }

    /// <summary>
    /// Checks an agent's CLI.
    /// </summary>
    /// <returns>Its path and version, or why it can't be used.</returns>
    /// <exception cref="OperationCanceledException">The check was cancelled.</exception>
    public async Task<AgentToolStatus> CheckAsync(SupportedAgent agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var command = agent.Definition.Command;
        var commandLine = string.Join(' ', [command, .. agent.VersionArguments]);
        if (_processLauncher.FindExecutable(command) is not { } path)
        {
            return new AgentToolStatus(null, null,
                $"Couldn't find '{command}' on the search path. Install {agent.Name}, then check again. If it's "
                + "installed, make sure its folder is on PATH, and restart TAMMi if you've just changed PATH.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        ProcessResult result;
        try
        {
            result = await _processLauncher.RunAsync(new ProcessRequest(path, agent.VersionArguments), timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AgentToolStatus(path, null,
                $"'{commandLine}' didn't finish within {_timeout.TotalSeconds:0.#} seconds.");
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException
            or UnauthorizedAccessException)
        {
            return new AgentToolStatus(path, null, $"Couldn't run '{path}': {ex.Message}");
        }

        if (!result.Succeeded)
        {
            var detail = FirstLine(result.StandardError) ?? FirstLine(result.StandardOutput);
            return new AgentToolStatus(path, null,
                $"'{commandLine}' failed with exit code {result.ExitCode}" + (detail is null ? "." : $": {detail}"));
        }

        return new AgentToolStatus(path, FirstLine(result.StandardOutput) ?? FirstLine(result.StandardError), null);
    }

    private static string? FirstLine(string text) =>
        text.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
}
