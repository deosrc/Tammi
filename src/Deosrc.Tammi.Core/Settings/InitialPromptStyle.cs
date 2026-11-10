namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// How an agent is given its initial prompt when it starts.
/// </summary>
public enum InitialPromptStyle
{
    /// <summary>
    /// The prompt is typed into the agent's input once it has started, and not sent, so the user can change it and
    /// send it themselves.
    /// </summary>
    Prefill,

    /// <summary>
    /// The prompt is passed to the agent as the Agent Definition says (see
    /// <see cref="Agents.AgentDefinition.PromptArguments"/>), so the agent acts on it straight away.
    /// </summary>
    Submit,
}
