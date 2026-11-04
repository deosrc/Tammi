using System.Text.Json;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Agents;

/// <summary>
/// An Agent: an agent CLI running in a pseudoterminal, in the context of a folder.
/// </summary>
/// <remarks>
/// The agent owns its session: show <see cref="Session"/> in a terminal, and stop the agent to end it.
/// <see cref="StateChanged"/> is raised on a background thread. While the process runs, an agent whose definition has
/// <see cref="AgentDefinition.Hooks"/> and that was started with a hook registration follows the events its hooks
/// send. Other agents stay <see cref="AgentState.Running"/>.
/// </remarks>
public sealed class Agent : IAsyncDisposable
{
    private readonly TaskCompletionSource<AgentState> _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _lock = new();
    private readonly AgentHookRegistration? _hooks;
    private Task? _stopped;
    private bool _stopRequested;
    private AgentState _state = AgentState.Running;
    private int? _exitCode;
    private string? _sessionId;

    /// <summary>
    /// Wraps a session that's running an agent, and starts following its process.
    /// </summary>
    /// <param name="definition">The definition the agent was started from.</param>
    /// <param name="workingDirectory">The folder the agent was started in.</param>
    /// <param name="session">The pseudoterminal session the agent runs in.</param>
    /// <param name="hooks">
    /// Where the agent's hooks send their events, or <c>null</c> if it was started without hooks. The agent takes it
    /// over, and disposes it once the process has ended.
    /// </param>
    /// <param name="sessionId">
    /// The Agent Session's ID, if TAMMi set it at launch. Otherwise <c>null</c>, until the agent's hooks report one.
    /// </param>
    public Agent(
        AgentDefinition definition,
        string workingDirectory,
        IPtySession session,
        AgentHookRegistration? hooks = null,
        string? sessionId = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _sessionId = sessionId;
        if (hooks is not null && definition.Hooks is not null)
        {
            _hooks = hooks;
            _hooks.EventReceived += OnHookEvent;
        }
        else
        {
            hooks?.Dispose();
        }

        _ = FollowProcessAsync();
    }

    /// <summary>
    /// Raised when <see cref="State"/> changes, on a background thread.
    /// </summary>
    public event Action<Agent>? StateChanged;

    /// <summary>
    /// The definition the agent was started from.
    /// </summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// The folder the agent was started in.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The pseudoterminal session the agent runs in.
    /// </summary>
    public IPtySession Session { get; }

    /// <summary>
    /// Raised when <see cref="SessionId"/> changes, on a background thread: when the agent's hooks first report it, or
    /// report that the agent has moved to another session (e.g. after Claude Code's <c>/clear</c>).
    /// </summary>
    public event Action<Agent>? SessionIdChanged;

    /// <summary>
    /// The ID of the Agent Session the agent is running, which can be resumed with it later (see
    /// <see cref="AgentLauncher.Resume"/>). It's <c>null</c> until it's known: TAMMi sets it at launch when the Agent
    /// Definition says how, and the agent's hooks report it when they can. It stays <c>null</c> for agents that can't
    /// resume sessions.
    /// </summary>
    public string? SessionId
    {
        get
        {
            lock (_lock)
            {
                return _sessionId;
            }
        }
    }

    /// <summary>
    /// The agent's current state.
    /// </summary>
    public AgentState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// The process's exit code once it has ended, or <c>null</c> while it's running or if the code isn't known.
    /// </summary>
    public int? ExitCode
    {
        get
        {
            lock (_lock)
            {
                return _exitCode;
            }
        }
    }

    /// <summary>
    /// Whether the user has asked for the agent to stop.
    /// </summary>
    public bool StopRequested
    {
        get
        {
            lock (_lock)
            {
                return _stopRequested;
            }
        }
    }

    /// <summary>
    /// Completes with the final state once the process has ended.
    /// </summary>
    public Task<AgentState> Ended => _ended.Task;

    /// <summary>
    /// Stops the agent: hangs up its process, and kills it if it doesn't exit. Completes once the process has ended
    /// and <see cref="State"/> has been updated. Stopping an agent that has already ended does nothing.
    /// </summary>
    public Task StopAsync()
    {
        lock (_lock)
        {
            if (_stopped is null)
            {
                // Set before the process is ended, so its exit is seen as a stop.
                _stopRequested = _state.IsRunning();
                _stopped = StopCoreAsync();
            }

            return _stopped;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task StopCoreAsync()
    {
        await Session.DisposeAsync().ConfigureAwait(false);

        // The session waits for the process to exit, but gives up if it can't be killed.
        await Task.WhenAny(Ended, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
    }

    private void UpdateSessionId(JsonElement hookEvent)
    {
        // Only IDs that can be resumed count, so a session that can't be resumed is never recorded.
        if (!Definition.CanResume || Definition.Hooks?.SessionIdFrom(hookEvent) is not { } id
            || !AgentSession.IsValidId(id))
        {
            return;
        }

        lock (_lock)
        {
            if (string.Equals(_sessionId, id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _sessionId = id;
        }

        SessionIdChanged?.Invoke(this);
    }

    private async Task FollowProcessAsync()
    {
        int? exitCode;
        try
        {
            exitCode = await Session.Exited.ConfigureAwait(false);
        }
        catch (Exception)
        {
            exitCode = null;
        }

        AgentState state;
        lock (_lock)
        {
            _exitCode = exitCode;
            _state = state = AgentStates.AfterExit(exitCode, _stopRequested);
        }

        if (_hooks is not null)
        {
            _hooks.EventReceived -= OnHookEvent;
            _hooks.Dispose();
        }

        _ended.TrySetResult(state);
        StateChanged?.Invoke(this);
    }

    private void OnHookEvent(JsonElement hookEvent)
    {
        UpdateSessionId(hookEvent);
        if (Definition.Hooks?.StateAfter(hookEvent) is not { } next || !next.IsRunning())
        {
            return;
        }

        lock (_lock)
        {
            // Once the process has ended, or the user has asked it to stop, its last events no longer count.
            if (!_state.IsRunning() || _stopRequested || _state == next)
            {
                return;
            }

            _state = next;
        }

        StateChanged?.Invoke(this);
    }
}
