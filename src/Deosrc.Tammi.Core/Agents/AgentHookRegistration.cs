using System.Text.Json;

namespace TAMMi.Core.Agents;

/// <summary>
/// An agent's place on the <see cref="AgentHookServer"/>: the endpoint its hooks send their events to, and the events
/// they've sent. Dispose it once the agent has ended, so its token stops working.
/// </summary>
public sealed class AgentHookRegistration : IDisposable
{
    private readonly AgentHookServer _server;

    internal AgentHookRegistration(AgentHookEndpoint endpoint, AgentHookServer server)
    {
        Endpoint = endpoint;
        _server = server;
    }

    /// <summary>
    /// Raised for each event the agent's hooks send, on a background thread. The event is only valid until the
    /// handler returns. The hook gets its response once the handlers have run, so events are handled in the order the
    /// agent sends them, and handlers should be quick.
    /// </summary>
    public event Action<JsonElement>? EventReceived;

    /// <summary>
    /// Where the agent's hooks send their events.
    /// </summary>
    public AgentHookEndpoint Endpoint { get; }

    /// <inheritdoc />
    public void Dispose() => _server.Unregister(this);

    internal void OnEventReceived(JsonElement hookEvent) => EventReceived?.Invoke(hookEvent);
}
