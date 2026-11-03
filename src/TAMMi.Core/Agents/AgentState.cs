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

/// <summary>
/// Works out an agent's state from its process.
/// </summary>
public static class AgentStates
{
    /// <summary>
    /// The state of an agent whose process has ended.
    /// </summary>
    /// <param name="exitCode">
    /// The exit code, or <c>null</c> if it isn't known. On Linux, a process ended by a signal has 128 + the signal
    /// number.
    /// </param>
    /// <param name="stopped">
    /// Whether the user stopped the agent. Stopping ends the process with a hang-up or a kill, so its exit code says
    /// nothing about whether it went wrong.
    /// </param>
    public static AgentState AfterExit(int? exitCode, bool stopped) =>
        stopped || exitCode == 0 ? AgentState.Exited : AgentState.Error;
}
