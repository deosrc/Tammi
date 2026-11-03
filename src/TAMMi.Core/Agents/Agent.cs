using TAMMi.Core.Platform;

namespace TAMMi.Core.Agents;

/// <summary>
/// An Agent: an agent CLI running in a pseudoterminal, in the context of a folder.
/// </summary>
/// <remarks>
/// The agent owns its session: show <see cref="Session"/> in a terminal, and stop the agent to end it.
/// <see cref="StateChanged"/> is raised on a background thread.
/// </remarks>
public sealed class Agent : IAsyncDisposable
{
    private readonly TaskCompletionSource<AgentState> _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _lock = new();
    private Task? _stopped;
    private bool _stopRequested;
    private AgentState _state = AgentState.Running;
    private int? _exitCode;

    /// <summary>
    /// Wraps a session that's running an agent, and starts following its process.
    /// </summary>
    public Agent(AgentDefinition definition, string workingDirectory, IPtySession session)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        Session = session ?? throw new ArgumentNullException(nameof(session));
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
                _stopRequested = _state == AgentState.Running;
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

        _ended.TrySetResult(state);
        StateChanged?.Invoke(this);
    }
}
