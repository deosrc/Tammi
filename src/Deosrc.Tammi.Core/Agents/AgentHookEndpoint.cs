namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// Where an agent's hooks send their events, and the token that says which agent sent them.
/// </summary>
/// <remarks>
/// The token is passed in the agent's environment, as <see cref="TokenVariable"/>, rather than in its arguments, so
/// other users can't read it from the process list. Hooks send it as a bearer token in the <c>Authorization</c> header.
/// </remarks>
/// <param name="Url">The URL that hooks POST their events to, on the loopback address.</param>
/// <param name="Token">The agent's token, unique to it and hard to guess.</param>
public sealed record AgentHookEndpoint(Uri Url, string Token)
{
    /// <summary>
    /// The environment variable that holds the agent's token.
    /// </summary>
    public const string TokenVariable = "TAMMI_AGENT_HOOK_TOKEN";

    /// <summary>
    /// The environment variables to start the agent with.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Environment =>
        new Dictionary<string, string?> { [TokenVariable] = Token };
}
