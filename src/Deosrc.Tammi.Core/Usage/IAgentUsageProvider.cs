namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// Reads the usage of one kind of AI agent.
/// </summary>
public interface IAgentUsageProvider
{
    /// <summary>
    /// The agent's name, e.g. "Claude Code". It identifies the agent in the settings.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Reads the agent's current usage.
    /// </summary>
    /// <exception cref="AgentUsageException">The usage couldn't be read, e.g. the user isn't signed in.</exception>
    Task<AgentUsage> GetUsageAsync(CancellationToken cancellationToken = default);
}
