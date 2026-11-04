using TAMMi.Core.Terminals;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// A process running in a workspace's embedded terminal, shown as a tab in its Workspace View: a terminal or an agent.
/// </summary>
public interface IWorkspaceProcess
{
    /// <summary>
    /// The ID of the workspace the process belongs to.
    /// </summary>
    string WorkspaceId { get; }

    /// <summary>
    /// The name its tab shows, unique among the workspace's open processes of its kind.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The folder the process was started in.
    /// </summary>
    string WorkingDirectory { get; }

    /// <summary>
    /// The process's terminal session, with its output.
    /// </summary>
    TerminalSession Session { get; }

    /// <summary>
    /// Whether the process has ended.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Orders processes by when they started, across terminals and agents: a later process has a higher number.
    /// </summary>
    long StartOrder { get; }
}
