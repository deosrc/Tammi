using System.Net.Sockets;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Agents;

/// <summary>
/// Starts agents from their definitions, in the embedded terminal's pseudoterminals.
/// </summary>
/// <param name="ptyService">Starts the agents' processes.</param>
/// <param name="hookServer">
/// Where agents with <see cref="AgentDefinition.Hooks"/> report their events, or <c>null</c> to start every agent
/// without hooks.
/// </param>
public sealed class AgentLauncher(IPtyService ptyService, AgentHookServer? hookServer = null)
{
    /// <summary>
    /// Starts an agent.
    /// </summary>
    /// <param name="definition">The agent to start.</param>
    /// <param name="workingDirectory">The folder to start it in, e.g. the Workspace Folder.</param>
    /// <param name="size">The terminal's starting size. The terminal resizes it to fit once it's shown.</param>
    /// <param name="initialPrompt">
    /// The initial prompt (see <see cref="InitialPrompt"/>), or <c>null</c> for none. It's passed as the definition
    /// says, and left out if the agent doesn't take one.
    /// </param>
    /// <remarks>
    /// An agent whose definition has hooks is given them in its arguments and environment, before the definition's own
    /// arguments. If the hook server can't be started, the agent starts without them.
    /// </remarks>
    /// <returns>The running agent. Stop it to end its process.</returns>
    /// <exception cref="FileNotFoundException">The agent's command can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory doesn't exist.</exception>
    /// <exception cref="ArgumentException">
    /// The definition has no command, or the prompt has a null character, or it has a line break and the agent's command is a Windows batch file, which
    /// can't be given one.
    /// </exception>
    public Agent Start(AgentDefinition definition, string workingDirectory, PtySize size, string? initialPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (string.IsNullOrWhiteSpace(definition.Command))
        {
            throw new ArgumentException(
                $"The Agent Definition '{definition.Name}' has no command. Set one in Settings.", nameof(definition));
        }

        if (initialPrompt?.Contains('\0', StringComparison.Ordinal) == true)
        {
            throw new ArgumentException("The initial prompt can't contain a null character.", nameof(initialPrompt));
        }

        var fullPath = Path.GetFullPath(workingDirectory);
        var arguments = definition.BuildArguments(initialPrompt);
        var definitionHooks = definition.Hooks;
        var hooks = definitionHooks is null ? null : TryRegisterHooks();
        var request = new ProcessRequest(definition.Command, arguments) { WorkingDirectory = fullPath };
        if (definitionHooks is not null && hooks is not null)
        {
            request = request with
            {
                Arguments = [.. definitionHooks.CreateArguments(hooks.Endpoint), .. arguments],
                Environment = hooks.Endpoint.Environment,
            };
        }

        IPtySession session;
        try
        {
            session = ptyService.Start(request, size);
        }
        catch
        {
            hooks?.Dispose();
            throw;
        }

        return new Agent(definition, fullPath, session, hooks);
    }

    private AgentHookRegistration? TryRegisterHooks()
    {
        try
        {
            return hookServer?.Register();
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            // Without hooks, the agent still runs, and shows Running.
            return null;
        }
    }
}
