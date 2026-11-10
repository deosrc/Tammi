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
        state is AgentState.Running or AgentState.Working or AgentState.NeedsInput or AgentState.Idle;

    /// <summary>
    /// The state that stands for a group of agents, e.g. on a workspace's tab: the one that most needs the user's
    /// attention. In order, that's <see cref="AgentState.Error"/>, <see cref="AgentState.NeedsInput"/>,
    /// <see cref="AgentState.Working"/>, then <see cref="AgentState.Idle"/> or <see cref="AgentState.Running"/>.
    /// Agents that exited normally don't count.
    /// </summary>
    /// <returns>The state, or <c>null</c> if no agent counts.</returns>
    public static AgentState? Summarise(IEnumerable<AgentState> states)
    {
        AgentState? summary = null;
        var summaryRank = int.MaxValue;
        foreach (var state in states)
        {
            var rank = Rank(state);
            if (rank < summaryRank)
            {
                summary = state;
                summaryRank = rank;
            }
        }

        return summary;
    }

    // Lower is more important. Exited is last, and doesn't count.
    private static int Rank(AgentState state) => state switch
    {
        AgentState.Error => 0,
        AgentState.NeedsInput => 1,
        AgentState.Working => 2,
        AgentState.Idle or AgentState.Running => 3,
        _ => int.MaxValue,
    };
}
