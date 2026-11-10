using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Wording and colours for agent states, as the Workspace View shows them.
/// </summary>
internal static class AgentStateText
{
    /// <summary>
    /// The state's name, e.g. "Needs input".
    /// </summary>
    public static string Label(AgentState state) => state switch
    {
        AgentState.Working => "Working",
        AgentState.NeedsInput => "Needs input",
        AgentState.Idle => "Idle",
        AgentState.Running => "Running",
        AgentState.Exited => "Exited",
        _ => "Error",
    };

    /// <summary>
    /// The CSS class that colours the state: warning (orange) when the agent needs the user, danger (red) when it went
    /// wrong, idle (grey) when it's waiting for its next prompt, and active (blue) while it works. Running (no finer detail available) looks like idle.
    /// </summary>
    public static string ToneClass(AgentState state) => state switch
    {
        AgentState.NeedsInput => "agent-state-warning",
        AgentState.Error => "agent-state-danger",
        AgentState.Idle or AgentState.Running => "agent-state-idle",
        AgentState.Working => "agent-state-active",
        _ => "agent-state-ended",
    };
}
