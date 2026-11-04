using TAMMi.Core.Terminals;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Agents;

/// <summary>
/// An agent in a workspace: an Agent Definition running in the Workspace Folder or one of its repositories.
/// </summary>
/// <remarks>
/// It keeps running whether or not the workspace's tab is open. Stop or close it with <see cref="WorkspaceAgents"/>.
/// </remarks>
public sealed class WorkspaceAgent : IWorkspaceProcess
{
    private volatile string? _sessionProblem;

    internal WorkspaceAgent(string workspaceId, string name, Agent agent)
    {
        WorkspaceId = workspaceId;
        Name = name;
        Agent = agent;
        Session = new TerminalSession(agent.Session);
        StartOrder = WorkspaceProcessOrder.Next();
    }

    /// <inheritdoc />
    public string WorkspaceId { get; }

    /// <summary>
    /// The agent's name, unique among the workspace's open agents, e.g. "Claude Code 2" or "Claude Code (api)".
    /// </summary>
    public string Name { get; }

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
    /// Why the agent's Agent Session couldn't be recorded, so it can't be resumed after a restart, or <c>null</c> if
    /// it was recorded (or there's nothing to record).
    /// </summary>
    public string? SessionProblem
    {
        get => _sessionProblem;
        internal set => _sessionProblem = value;
    }
}
