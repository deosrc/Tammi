using TAMMi.Core.Platform;

namespace TAMMi.Core.Agents;

/// <summary>
/// Starts agents from their definitions, in the embedded terminal's pseudoterminals.
/// </summary>
public sealed class AgentLauncher(IPtyService ptyService)
{
    /// <summary>
    /// Starts an agent.
    /// </summary>
    /// <param name="definition">The agent to start.</param>
    /// <param name="workingDirectory">The folder to start it in, e.g. the Workspace Folder.</param>
    /// <param name="size">The terminal's starting size. The terminal resizes it to fit once it's shown.</param>
    /// <returns>The running agent. Stop it to end its process.</returns>
    /// <exception cref="FileNotFoundException">The agent's command can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory doesn't exist.</exception>
    public Agent Start(AgentDefinition definition, string workingDirectory, PtySize size)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var fullPath = Path.GetFullPath(workingDirectory);
        var request = new ProcessRequest(definition.Command, definition.Arguments) { WorkingDirectory = fullPath };
        return new Agent(definition, fullPath, ptyService.Start(request, size));
    }
}
