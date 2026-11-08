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
        AgentState.Failed => "Failed",
        AgentState.Running => "Running",
        AgentState.Exited => "Exited",
        _ => "Error",
    };

    /// <summary>
    /// The CSS class that colours the state: warning when the agent needs the user, danger when it went wrong or reported an error, success
    /// when it's waiting for its next prompt, and the accent while it works.
    /// </summary>
    public static string ToneClass(AgentState state) => state switch
    {
        AgentState.NeedsInput => "agent-state-warning",
        AgentState.Error or AgentState.Failed => "agent-state-danger",
        AgentState.Idle => "agent-state-success",
        AgentState.Working or AgentState.Running => "agent-state-active",
        _ => "agent-state-ended",
    };
}
