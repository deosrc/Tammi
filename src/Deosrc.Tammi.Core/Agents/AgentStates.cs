namespace Deosrc.Tammi.Core.Agents;

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

    /// <summary>
    /// Whether the agent's process is still running in this state: <see cref="AgentState.Running"/>, or one of the
    /// states its hooks report.
    /// </summary>
    public static bool IsRunning(this AgentState state) =>
        state is AgentState.Running or AgentState.Working or AgentState.NeedsInput or AgentState.Idle
            or AgentState.Failed;
}
