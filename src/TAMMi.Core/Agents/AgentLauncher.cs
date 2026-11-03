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
    /// <param name="initialPrompt">
    /// The initial prompt (see <see cref="InitialPrompt"/>), or <c>null</c> for none. It's passed as the definition
    /// says, and left out if the agent doesn't take one.
    /// </param>
    /// <returns>The running agent. Stop it to end its process.</returns>
    /// <exception cref="FileNotFoundException">The agent's command can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The working directory doesn't exist.</exception>
    /// <exception cref="ArgumentException">
    /// The prompt has a null character, or it has a line break and the agent's command is a Windows batch file, which
    /// can't be given one.
    /// </exception>
    public Agent Start(AgentDefinition definition, string workingDirectory, PtySize size, string? initialPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (initialPrompt?.Contains('\0', StringComparison.Ordinal) == true)
        {
            throw new ArgumentException("The initial prompt can't contain a null character.", nameof(initialPrompt));
        }

        var fullPath = Path.GetFullPath(workingDirectory);
        var request = new ProcessRequest(definition.Command, definition.BuildArguments(initialPrompt))
        {
            WorkingDirectory = fullPath,
        };
        return new Agent(definition, fullPath, ptyService.Start(request, size));
    }
}
