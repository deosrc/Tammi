using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.UI.Components;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Wording and tones for agent states, as the Workspace View shows them.
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
    /// How a card for an agent in the state looks: warning when the agent needs the user, danger when it went wrong,
    /// info while it works, and neutral otherwise.
    /// </summary>
    public static SidePanelCardTone CardTone(AgentState state) => state switch
    {
        AgentState.NeedsInput => SidePanelCardTone.Warning,
        AgentState.Error => SidePanelCardTone.Danger,
        AgentState.Working => SidePanelCardTone.Info,
        _ => SidePanelCardTone.Neutral,
    };
}
