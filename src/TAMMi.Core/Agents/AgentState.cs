namespace TAMMi.Core.Agents;

/// <summary>
/// What an agent is doing, as shown on Home and in the Workspace View.
/// </summary>
/// <remarks>
/// These are the states that come from the process alone. Agents with hooks will add finer states while running
/// (see <c>docs/CONTEXT.md</c>).
/// </remarks>
public enum AgentState
{
    /// <summary>
    /// The process is running.
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
}
