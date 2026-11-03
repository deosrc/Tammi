namespace TAMMi.Core.Agents;

/// <summary>
/// What an agent is doing, as shown on Home and in the Workspace View.
/// </summary>
/// <remarks>
/// <see cref="Working"/>, <see cref="NeedsInput"/> and <see cref="Idle"/> come from the agent's hooks, so only agents
/// whose definition has <see cref="AgentDefinition.Hooks"/> use them. The others show <see cref="Running"/> while their
/// process runs (see <c>docs/CONTEXT.md</c>).
/// </remarks>
public enum AgentState
{
    /// <summary>
    /// The process is running, but finer detail isn't available: the agent has no hooks, or hasn't reported yet.
    /// </summary>
    Running,

    /// <summary>
    /// The process ended normally: it exited with code 0, or the user stopped it.
    /// </summary>
    Exited,

    /// <summary>
    /// The process crashed, was killed by something other than TAMMi, or exited with a non-zero code.
    /// </summary>
    Error,

    /// <summary>
    /// The agent is busy. Reported by its hooks.
    /// </summary>
    Working,

    /// <summary>
    /// The agent is waiting on the user, e.g. for a question or permission prompt. Reported by its hooks.
    /// </summary>
    NeedsInput,

    /// <summary>
    /// The agent has finished its turn. Reported by its hooks.
    /// </summary>
    Idle,
}
