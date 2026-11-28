using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// An agent in a workspace: an Agent Definition running in the Workspace Folder or one of its repositories.
/// </summary>
/// <remarks>
/// It keeps running whether or not the workspace's tab is open. Stop or close it with <see cref="WorkspaceAgents"/>.
/// </remarks>
public sealed class WorkspaceAgent : IWorkspaceProcess
{
    private readonly Lock _lock = new();
    private volatile string? _sessionProblem;
    private DateTimeOffset _sessionStartedAt;
    private DateTimeOffset? _sessionCreatedAt;
    private DateTimeOffset? _endedAt;
    private AgentSession? _recordedSession;
    private string? _lastTitle;
    private string? _recordedTitle;
    private int _titleRecordScheduled;

    /// <param name="workspaceId">The workspace's ID.</param>
    /// <param name="name">The agent's unique name.</param>
    /// <param name="agent">The running agent.</param>
    /// <param name="startedAt">When it was started or resumed.</param>
    /// <param name="resumed">The recorded Agent Session it resumes, or <c>null</c> for a new session.</param>
    internal WorkspaceAgent(
        string workspaceId,
        string name,
        Agent agent,
        DateTimeOffset startedAt,
        AgentSession? resumed = null)
    {
        WorkspaceId = workspaceId;
        Name = name;
        Agent = agent;
        _sessionStartedAt = startedAt;
        _sessionCreatedAt = resumed is null ? startedAt : resumed.CreatedAt;
        _lastTitle = resumed?.LastTitle;
        _recordedTitle = resumed?.LastTitle;
        Session = new TerminalSession(agent.Session);
        Session.TitleChanged += NoteTitle;
        NoteTitle();
        StartOrder = MainAreaTabOrder.Next();
    }

    /// <inheritdoc />
    public string WorkspaceId { get; }

    /// <summary>
    /// The agent's name, unique among the workspace's open agents, e.g. "Claude Code 2" or "Claude Code (api)".
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public string DisplayName => Session.Title ?? Name;

    /// <summary>
    /// The running agent, with its definition, state and Agent Session ID.
    /// </summary>
    public Agent Agent { get; }

    /// <inheritdoc />
    public string WorkingDirectory => Agent.WorkingDirectory;

    /// <inheritdoc />
    public TerminalSession Session { get; }

    /// <summary>
    /// The agent's current state.
    /// </summary>
    public AgentState State => Agent.State;

    /// <summary>
    /// Whether the agent's process has ended.
    /// </summary>
    public bool HasExited => !State.IsRunning();

    /// <inheritdoc />
    public long StartOrder { get; }

    /// <summary>
    /// When the agent's current Agent Session was started or resumed: when the agent started, or when it moved to
    /// another session (e.g. after Claude Code's <c>/clear</c>).
    /// </summary>
    public DateTimeOffset SessionStartedAt
    {
        get
        {
            lock (_lock)
            {
                return _sessionStartedAt;
            }
        }
    }

    /// <summary>
    /// When the agent's current Agent Session was first started, before any time it was resumed, or <c>null</c> if it
    /// isn't known (see <see cref="AgentSession.CreatedAt"/>).
    /// </summary>
    public DateTimeOffset? SessionCreatedAt
    {
        get
        {
            lock (_lock)
            {
                return _sessionCreatedAt;
            }
        }
    }

    /// <summary>
    /// The last title the agent set for its terminal in its current Agent Session, kept when it clears it, or
    /// <c>null</c> if it hasn't set one. A resumed session starts with the title recorded for it.
    /// </summary>
    public string? LastTitle
    {
        get
        {
            lock (_lock)
            {
                return _lastTitle;
            }
        }
    }

    /// <summary>
    /// Why the agent's Agent Session couldn't be recorded, so it can't be resumed after a restart, or <c>null</c> if
    /// it was recorded (or there's nothing to record).
    /// </summary>
    public string? SessionProblem
    {
        get => _sessionProblem;
        internal set => _sessionProblem = value;
    }

    /// <summary>
    /// Completes once the agent's Agent Session has been recorded as ended, after its process ends.
    /// </summary>
    internal Task ExitRecorded { get; set; } = Task.CompletedTask;

    /// <summary>
    /// The agent's current Agent Session, to record, or <c>null</c> if it can't be resumed. Its title is then noted as
    /// recorded.
    /// </summary>
    internal AgentSession? SessionToRecord()
    {
        lock (_lock)
        {
            var session = AgentSession.Of(Agent, _sessionStartedAt, _lastTitle);
            if (session is not null)
            {
                session = session with { CreatedAt = _sessionCreatedAt, LastEndedAt = _endedAt };
                _recordedTitle = _lastTitle;
                _recordedSession = session;
            }

            return session;
        }
    }

    /// <summary>
    /// Notes that the agent has moved to another Agent Session, started at <paramref name="startedAt"/>. The last title
    /// is the terminal's current one, if any.
    /// </summary>
    /// <returns>
    /// The session it was in, as ended then with its last title, to record, or <c>null</c> if none was recorded.
    /// </returns>
    internal AgentSession? NoteNewSession(DateTimeOffset startedAt)
    {
        lock (_lock)
        {
            var ended = _recordedSession is { } previous
                && !string.Equals(previous.Id, Agent.SessionId, StringComparison.OrdinalIgnoreCase)
                    ? previous with { LastEndedAt = startedAt, LastTitle = _lastTitle }
                    : null;
            _sessionStartedAt = startedAt;
            _sessionCreatedAt = startedAt;
            _lastTitle = Session.Title;
            _recordedSession = null;
            return ended;
        }
    }

    /// <summary>
    /// Notes that the agent's process ended at <paramref name="endedAt"/>, and so its Agent Session with it.
    /// </summary>
    internal void NoteEnded(DateTimeOffset endedAt)
    {
        lock (_lock)
        {
            _endedAt = endedAt;
        }
    }

    /// <summary>
    /// Whether <see cref="LastTitle"/> has changed since the session was last recorded.
    /// </summary>
    internal bool TitleNeedsRecording
    {
        get
        {
            lock (_lock)
            {
                return _lastTitle != _recordedTitle;
            }
        }
    }

    /// <summary>
    /// Notes that a title change is waiting to be recorded. Returns <c>false</c> if one already is.
    /// </summary>
    internal bool ScheduleTitleRecord() => Interlocked.Exchange(ref _titleRecordScheduled, 1) == 0;

    /// <summary>
    /// Notes that the waiting title change is being recorded, so the next change waits again.
    /// </summary>
    internal void EndTitleRecord() => Volatile.Write(ref _titleRecordScheduled, 0);

    private void NoteTitle()
    {
        if (Session.Title is not { } title)
        {
            return;
        }

        lock (_lock)
        {
            _lastTitle = title;
        }
    }
}
