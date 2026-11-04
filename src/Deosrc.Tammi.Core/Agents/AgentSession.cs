namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// A recorded Agent Session: one conversation with an agent, which can be resumed after Tammi restarts with the
/// agent's own resume command (see <see cref="AgentLauncher.Resume"/>).
/// </summary>
/// <param name="Id">The session's ID, as the agent knows it.</param>
/// <param name="AgentName">The name of the Agent Definition the session was started with.</param>
/// <param name="WorkingDirectory">
/// The folder the agent was started in. Agents keep their sessions per folder, so it's resumed there too.
/// </param>
/// <param name="LastStartedAt">When the session was started, or last resumed.</param>
public sealed record AgentSession(string Id, string AgentName, string WorkingDirectory, DateTimeOffset LastStartedAt)
{
    /// <summary>
    /// Records the session an agent is running.
    /// </summary>
    /// <param name="agent">The agent, just started or resumed.</param>
    /// <param name="startedAt">When it was started or resumed.</param>
    /// <returns>The session, or <c>null</c> if the agent's session can't be resumed.</returns>
    public static AgentSession? Of(Agent agent, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return agent.SessionId is { } id
            ? new AgentSession(id, agent.Definition.Name, agent.WorkingDirectory, startedAt)
            : null;
    }

    /// <summary>
    /// Whether <paramref name="id"/> is an ID Tammi could have given a session: a UUID. Other IDs aren't resumed, so a
    /// changed file can't pass the agent other arguments in place of the ID.
    /// </summary>
    public static bool IsValidId(string? id) => Guid.TryParseExact(id, "D", out _);

    /// <summary>
    /// Finds the session's Agent Definition among <paramref name="definitions"/>, by name.
    /// </summary>
    /// <returns>The definition, or <c>null</c> if no agent has the session's name now.</returns>
    public AgentDefinition? FindDefinition(IEnumerable<AgentDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return definitions.FirstOrDefault(definition =>
            string.Equals(definition.Name?.Trim(), AgentName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
