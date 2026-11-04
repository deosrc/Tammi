using System.Text.Json;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// How an Agent Definition's CLI reports what it's doing through its hooks, for the states Working, Needs input and
/// Idle.
/// </summary>
/// <remarks>
/// In keeping with the principles in <c>docs/CONTEXT.md</c>, the hooks are supplied when Tammi launches the agent, in
/// its arguments and environment, never by changing files in its working directory or any repository. The hooks send
/// each event to Tammi's <see cref="AgentHookServer"/> as JSON.
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

    /// <summary>
    /// Whether the hooks report the Agent Session's ID (see <see cref="SessionIdFrom"/>), so Tammi can resume the
    /// session without setting its ID at launch.
    /// </summary>
    public virtual bool ReportsSessionId => false;

    /// <summary>
    /// The Agent Session ID that a hook event reports, so Tammi follows the agent when it moves to another session.
    /// </summary>
    /// <param name="hookEvent">The event, as the hook sent it.</param>
    /// <returns>The ID of the session the agent is in, or <c>null</c> if the event doesn't say.</returns>
    public virtual string? SessionIdFrom(JsonElement hookEvent) => null;
}
