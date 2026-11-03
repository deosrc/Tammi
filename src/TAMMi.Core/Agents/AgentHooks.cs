using System.Text.Json;

namespace TAMMi.Core.Agents;

/// <summary>
/// How an Agent Definition's CLI reports what it's doing through its hooks, for the states Working, Needs input and
/// Idle.
/// </summary>
/// <remarks>
/// In keeping with the principles in <c>docs/CONTEXT.md</c>, the hooks are supplied when TAMMi launches the agent, in
/// its arguments and environment, never by changing files in its working directory or any repository. The hooks send
/// each event to TAMMi's <see cref="AgentHookServer"/> as JSON.
/// </remarks>
public abstract class AgentHooks
{
    /// <summary>
    /// The arguments that make the CLI send its hook events to <paramref name="endpoint"/>. They're passed before the
    /// definition's own arguments.
    /// </summary>
    public abstract IReadOnlyList<string> CreateArguments(AgentHookEndpoint endpoint);

    /// <summary>
    /// The state that a hook event moves the agent to.
    /// </summary>
    /// <param name="hookEvent">The event, as the hook sent it.</param>
    /// <returns>
    /// <see cref="AgentState.Working"/>, <see cref="AgentState.NeedsInput"/> or <see cref="AgentState.Idle"/>, or
    /// <c>null</c> when the event doesn't change the state.
    /// </returns>
    public abstract AgentState? StateAfter(JsonElement hookEvent);
}
